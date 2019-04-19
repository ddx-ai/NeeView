﻿using NeeLaboratory.ComponentModel;
using NeeView.Effects;
using NeeView.Properties;
using NeeView.Windows.Property;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace NeeView
{

    // 自動回転タイプ
    public enum AutoRotateType
    {
        Right,
        Left,
    }

    /// <summary>
    /// ページ表示コンテンツ管理
    /// </summary>
    public class ContentCanvas : BindableBase, IDisposable
    {
        static ContentCanvas() => Current = new ContentCanvas();
        public static ContentCanvas Current { get; }

        #region Fields

        // コンテンツサイズ計算機
        private ContentSizeCalcurator _contentSizeCalcurator;

        private DragTransform _dragTransform;
        private DragTransformControl _dragTransformControl;

        private PageStretchMode _stretchModePrev = PageStretchMode.Uniform;

        private object _lock = new object();

        #endregion

        #region Constructors

        private ContentCanvas()
        {
            _contentSizeCalcurator = new ContentSizeCalcurator(this);

            _dragTransform = DragTransform.Current;
            _dragTransformControl = DragTransformControl.Current;

            DragTransform.Current.TransformChanged += Transform_TransformChanged;
            LoupeTransform.Current.TransformChanged += Transform_TransformChanged;

            // Contents
            Contents = new ObservableCollection<ViewContent>();
            Contents.Add(new ViewContent());
            Contents.Add(new ViewContent());

            MainContent = Contents[0];

            BookHub.Current.BookChanging +=
                (s, e) => IgnoreViewContentsReservers();

            // TODO: BookOperationから？
            BookHub.Current.ViewContentsChanged +=
                OnViewContentsChanged;
            BookHub.Current.NextContentsChanged +=
                OnNextContentsChanged;

            BookHub.Current.EmptyMessage +=
                (s, e) => EmptyPageMessage = e.Message;
        }

        #endregion

        #region Events

        //
        public event EventHandler ContentChanged;

        #endregion

        #region Properties

        // 空フォルダー通知表示のON/OFF
        private bool _isVisibleEmptyPageMessage = false;
        public bool IsVisibleEmptyPageMessage
        {
            get { return _isVisibleEmptyPageMessage; }
            set { if (_isVisibleEmptyPageMessage != value) { _isVisibleEmptyPageMessage = value; RaisePropertyChanged(); } }
        }

        // 空フォルダー通知表示の詳細テキスト
        private string _emptyPageMessage;
        public string EmptyPageMessage
        {
            get { return _emptyPageMessage; }
            set { _emptyPageMessage = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// IsAutoRotate property.
        /// </summary>
        private bool _isAutoRotate;
        public bool IsAutoRotate
        {
            get { return _isAutoRotate; }
            set
            {
                if (_isAutoRotate != value)
                {
                    _isAutoRotate = value;
                    RaisePropertyChanged();
                    UpdateContentSize(GetAutoRotateAngle());
                    ResetTransform(true);
                }
            }
        }

        // ドットのまま拡大
        private bool _isEnabledNearestNeighbor;
        public bool IsEnabledNearestNeighbor
        {
            get { return _isEnabledNearestNeighbor; }
            set
            {
                if (_isEnabledNearestNeighbor != value)
                {
                    _isEnabledNearestNeighbor = value;
                    RaisePropertyChanged();
                    UpdateContentScalingMode();
                }
            }
        }

        // スケールモード
        private PageStretchMode _stretchMode = PageStretchMode.Uniform;
        public PageStretchMode StretchMode
        {
            get { return _stretchMode; }
            set
            {
                _stretchModePrev = _stretchMode;
                _stretchMode = value;
                RaisePropertyChanged();
                UpdateContentSize();
                ResetTransform(true);
            }
        }

        // ビューエリアサイズ
        public Size ViewSize { get; private set; }

        // コンテンツ
        public ObservableCollection<ViewContent> Contents { get; private set; }

        // コンテンツ複製。処理時のコレクション変更の例外を避けるため。
        public List<ViewContent> CloneContents
        {
            get
            {
                lock (_lock)
                {
                    return Contents.ToList();
                }
            }
        }

        // 見開き時のメインとなるコンテンツ
        private ViewContent _mainContent;
        public ViewContent MainContent
        {
            get { return _mainContent; }
            set
            {
                if (_mainContent != value)
                {
                    _mainContent = value;
                    RaisePropertyChanged();

                    this.IsMediaContent = _mainContent is MediaViewContent;
                }
            }
        }

        // メインコンテンツがメディアコンテンツ？
        private bool _isMediaContent;
        public bool IsMediaContent
        {
            get { return _isMediaContent; }
            set { if (_isMediaContent != value) { _isMediaContent = value; RaisePropertyChanged(); } }
        }


        // コンテンツマージン
        private Thickness _contentsMargin;
        public Thickness ContentsMargin
        {
            get { return _contentsMargin; }
            set { _contentsMargin = value; RaisePropertyChanged(); }
        }

        // 2ページコンテンツの隙間
        private double _contentSpace = -1.0;
        [DefaultValue(-1.0)]
        [PropertyRange("@ParamContentCanvasContentsSpace", -32, 32, TickFrequency = 1, Tips = "@ParamContentCanvasContentsSpaceTips")]
        public double ContentsSpace
        {
            get { return _contentSpace; }
            set { _contentSpace = value; RaisePropertyChanged(); UpdateContentSize(); }
        }

        /// <summary>
        /// 次のページ更新時の表示開始位置
        /// TODO: ちゃんとBookから情報として上げるようにするべき
        /// </summary>
        public DragViewOrigin NextViewOrigin { get; set; }

        /// <summary>
        /// ContentAngle property.
        /// </summary>
        private double _contentAngle;
        public double ContentAngle
        {
            get { return _contentAngle; }
            set { if (_contentAngle != value) { _contentAngle = value; RaisePropertyChanged(); } }
        }

        // メインコンテンツのオリジナル表示スケール
        public double MainContentScale => MainContent != null ? MainContent.Scale * Config.Current.Dpi.DpiScaleX : 0.0;

        //
        public GridLine GridLine { get; private set; } = new GridLine();

        #endregion

        #region Methods

        // トランスフォーム変更イベント処理
        private void Transform_TransformChanged(object sender, TransformEventArgs e)
        {
            UpdateContentScalingMode();
            MouseInput.Current.ShowMessage(e.ActionType, MainContent);
        }

        // コンテンツカラー
        public Color GetContentColor()
        {
            return Contents[Contents[1].IsValid ? 1 : 0].Color;
        }

        // 現在のビューコンテンツのリザーバーを無効化
        private void IgnoreViewContentsReservers()
        {
            foreach (var content in CloneContents)
            {
                content.IgnoreReserver = true;
            }
        }

        /// <summary>
        /// 表示コンテンツ更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewContentsChanged(object sender, ViewContentSourceCollectionChangedEventArgs e)
        {
            // 画像リサイズ
            ////ResizeConten(e?.ViewPageCollection);

            var contents = new List<ViewContent>();

            // ViewContent作成
            if (e?.ViewPageCollection?.Collection != null)
            {
                foreach (var source in e.ViewPageCollection.Collection)
                {
                    if (source != null)
                    {
                        var old = Contents[contents.Count];
                        var content = ViewContentFactory.Create(source, old);
                        contents.Add(content);
                    }
                }
            }

            // ページが存在しない場合、専用メッセージを表示する
            IsVisibleEmptyPageMessage = e?.ViewPageCollection != null && contents.Count == 0;

            // メインとなるコンテンツを指定
            MainContent = contents.Count > 0 ? (contents.First().Position < contents.Last().Position ? contents.First() : contents.Last()) : null;

            // ViewModelプロパティに反映
            lock (_lock)
            {
                for (int index = 0; index < 2; ++index)
                {
                    Contents[index] = index < contents.Count ? contents[index] : new ViewContent();
                }
            }

            // コンテンツサイズ更新
            UpdateContentSize(GetAutoRotateAngle());

            // ルーペ解除
            if (MouseInput.Current.Loupe.IsResetByPageChanged)
            {
                MouseInput.Current.IsLoupeMode = false;
            }

            // 座標初期化
            ResetTransform(false, e != null ? e.ViewPageCollection.Range.Direction : 0, NextViewOrigin);
            NextViewOrigin = DragViewOrigin.None;

            ContentChanged?.Invoke(this, null);

            // GC
            MemoryControl.Current.GarbageCollect();

            ////DebugTimer.Check("UpdatedContentCanvas");
        }

        // 先読みコンテンツ更新
        // 表示サイズを確定し、フィルター適用時にリサイズ処理を行う
        private void OnNextContentsChanged(object sender, ViewContentSourceCollectionChangedEventArgs source)
        {
            if (source?.ViewPageCollection?.Collection == null) return;

            if (!source.IsForceResize)
            {
                // ルーペモードでかつ継続される設定の場合、先読みではリサイズしない
                if (LoupeTransform.Current.IsEnabled && !MouseInput.Current.Loupe.IsResetByPageChanged) return;
            }

            ResizeConten(source.ViewPageCollection, source.CancellationToken);
        }


        /// <summary>
        /// コンテンツリサイズ
        /// </summary>
        private void ResizeConten(ViewContentSourceCollection viewPageCollection, CancellationToken token)
        {
            if (viewPageCollection?.Collection == null) return;

            token.ThrowIfCancellationRequested();


            var sizes = viewPageCollection.Collection.Select(e => e.Size).ToList();
            while (sizes.Count() < 2)
            {
                sizes.Add(SizeExtensions.Zero);
            }

            // 表示サイズ計算
            var result = MainContent is MediaViewContent
                ? _contentSizeCalcurator.GetFixedContentSize(sizes, 0.0)
                : _contentSizeCalcurator.GetFixedContentSize(sizes);

            // スケール維持？
            var scale = _dragTransformControl.IsKeepScale ? _dragTransform.Scale : 1.0;

            // リサイズ
            for (int i = 0; i < 2; ++i)
            {
                var size0 = sizes[i];
                if (size0.IsZero()) continue;

                var size1 = result.ContentSizeList[i].Multi(scale);
                if (i < viewPageCollection.Collection.Count && viewPageCollection.Collection[i].IsHalf) // 分割前サイズでリサイズ
                {
                    size1 = new Size(size1.Width * 2.0, size1.Height);
                }
                ////Debug.WriteLine($"{i}: {size0} => {size1.Truncate()}");


                var content = viewPageCollection.Collection[i].Content;
                try
                {
                    if (content.PageMessage == null && content.CanResize && content is BitmapContent bitmapContent)
                    {
                        var resized = bitmapContent.Picture?.CreateBitmapSource(bitmapContent.GetRenderSize(size1), token);
                        if (resized == true)
                        {
                            viewPageCollection.Collection[i].Page.DebugRaiseContentPropertyChanged();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("OnNextContentChanged: " + ex.Message);
                    content.SetPageMessage(ex);
                }
            }
        }

        //
        public void ResetTransform(bool isForce)
        {
            ResetTransform(isForce, 0, DragViewOrigin.None);
        }

        // 座標系初期化
        // TODO: ルーペ操作との関係
        public void ResetTransform(bool isForce, int pageDirection, DragViewOrigin viewOrigin)
        {
            // ルーペでない場合は初期化
            if (!MouseInput.Current.IsLoupeMode)
            {
                _dragTransformControl.SetMouseDragSetting(pageDirection, viewOrigin, BookSettingPresenter.Current.LatestSetting.BookReadOrder);

                // リセット
                var angle = _isAutoRotate ? GetAutoRotateAngle() : double.NaN;
                _dragTransformControl.Reset(isForce, angle);
            }
        }

        /// <summary>
        /// ページ開始時の回転
        /// </summary>
        /// <returns></returns>
        public double GetAutoRotateAngle()
        {
            if (MainContent is MediaViewContent)
            {
                return 0.0;
            }
            else
            {
                return _contentSizeCalcurator.GetAutoRotateAngle(GetContentSizeList());
            }
        }

        /// <summary>
        /// 有効な表示コンテンツサイズのリストを取得
        /// </summary>
        /// <returns></returns>
        private List<Size> GetContentSizeList()
        {
            return CloneContents.Select(e => (e.Source?.Size ?? SizeExtensions.Zero).EmptyOrZeroCoalesce(GetViewContentSize(e))).ToList();
        }

        // TODO: ViewContent.Size の廃止
        private Size GetViewContentSize(ViewContent viewContent)
        {
            return viewContent.Size;
        }

        // ビューエリアサイズを更新
        public void SetViewSize(double width, double height)
        {
            this.ViewSize = new Size(width, height);

            UpdateContentSize();

            ContentRebuild.Current.Request();
        }


        //
        public void UpdateContentSize(double angle)
        {
            this.ContentAngle = angle;
            UpdateContentSize();
        }

        // コンテンツ表示サイズを更新
        public void UpdateContentSize()
        {
            if (!CloneContents.Any(e => e.IsValid)) return;

            var result = _contentSizeCalcurator.GetFixedContentSize(GetContentSizeList(), this.ContentAngle);

            this.ContentsMargin = result.ContentsMargin;

            for (int i = 0; i < 2; ++i)
            {
                Contents[i].Width = result.ContentSizeList[i].Width;
                Contents[i].Height = result.ContentSizeList[i].Height;
            }

            UpdateContentScalingMode();

            this.GridLine.SetSize(result.Width, result.Height);
        }


        // コンテンツスケーリングモードを更新
        public void UpdateContentScalingMode(ViewContent target = null)
        {
            double finalScale = _dragTransform.Scale * LoupeTransform.Current.FixedScale * Config.Current.RawDpi.DpiScaleX;

            foreach (var content in CloneContents)
            {
                if (target != null && target != content) continue;

                if (content.View != null && content.IsBitmapScalingModeSupported())
                {
                    var bitmapContent = content as BitmapViewContent;
                    if (bitmapContent == null) continue;

                    var bitmap = bitmapContent.GetViewBitmap();
                    if (bitmap == null) continue;

                    var pixelHeight = bitmap.PixelHeight;
                    var pixelWidth = bitmap.PixelWidth;
                    var viewHeight = content.Height * finalScale;
                    var viewWidth = content.Width * finalScale;

                    var diff = Math.Abs(pixelHeight - viewHeight) + Math.Abs(pixelWidth - viewWidth);
                    var diffAngle = Math.Abs(_dragTransform.Angle % 90.0);
                    if (Config.Current.IsDpiSquare && diff < 1.1 && diffAngle < 0.1)
                    {
                        content.BitmapScalingMode = BitmapScalingMode.NearestNeighbor;
                        content.SetViewMode(ContentViewMode.Pixeled, finalScale);
                    }
                    else
                    {
                        content.BitmapScalingMode = (IsEnabledNearestNeighbor && pixelHeight < viewHeight && pixelWidth < viewWidth) ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;
                        content.SetViewMode(ContentViewMode.Scale, finalScale);
                    }

                    // ##
                    DebugInfo.Current?.SetMessage($"{content.BitmapScalingMode}: s={pixelHeight}: v={viewHeight:0.00}: a={_dragTransform.Angle:0.00}");

                    if (bitmapContent.IsDarty())
                    {
                        ContentRebuild.Current.Request();
                    }
                }
            }
        }


        #region スケールモード

        // トグル
        public PageStretchMode GetToggleStretchMode(ToggleStretchModeCommandParameter param)
        {
            PageStretchMode mode = StretchMode;
            int length = Enum.GetNames(typeof(PageStretchMode)).Length;
            int count = 0;
            do
            {
                var next = (int)mode + 1;
                if (!param.IsLoop && next >= length) return StretchMode;
                mode = (PageStretchMode)(next % length);
                if (param.StretchModes[mode]) return mode;
            }
            while (count++ < length);
            return StretchMode;
        }

        // 逆トグル
        public PageStretchMode GetToggleStretchModeReverse(ToggleStretchModeCommandParameter param)
        {
            PageStretchMode mode = StretchMode;
            int length = Enum.GetNames(typeof(PageStretchMode)).Length;
            int count = 0;
            do
            {
                var prev = (int)mode - 1;
                if (!param.IsLoop && prev < 0) return StretchMode;
                mode = (PageStretchMode)((prev + length) % length);
                if (param.StretchModes[mode]) return mode;
            }
            while (count++ < length);
            return StretchMode;
        }


        //
        public void SetStretchMode(PageStretchMode mode, bool isToggle)
        {
            StretchMode = GetFixedStretchMode(mode, isToggle);
        }

        //
        public bool TestStretchMode(PageStretchMode mode, bool isToggle)
        {
            return mode == GetFixedStretchMode(mode, isToggle);
        }

        //
        private PageStretchMode GetFixedStretchMode(PageStretchMode mode, bool isToggle)
        {
            if (isToggle && StretchMode == mode)
            {
                return (mode == PageStretchMode.None) ? _stretchModePrev : PageStretchMode.None;
            }
            else
            {
                return mode;
            }
        }

        #endregion

        #region 回転コマンド

        //
        public bool ToggleAutoRotate()
        {
            return IsAutoRotate = !IsAutoRotate;
        }

        //
        public void ViewRotateLeft(ViewRotateCommandParameter parameter)
        {
            if (parameter.IsStretch) _dragTransformControl.ResetDefault();
            _dragTransformControl.Rotate(-parameter.Angle);
            if (parameter.IsStretch)
            {
                ContentCanvas.Current.UpdateContentSize(_dragTransform.Angle);
                ContentRebuild.Current.Request();
                DragTransformControl.Current.SnapView();
            }
        }

        //
        public void ViewRotateRight(ViewRotateCommandParameter parameter)
        {
            if (parameter.IsStretch) _dragTransformControl.ResetDefault();
            _dragTransformControl.Rotate(+parameter.Angle);
            if (parameter.IsStretch)
            {
                ContentCanvas.Current.UpdateContentSize(_dragTransform.Angle);
                ContentRebuild.Current.Request();
                DragTransformControl.Current.SnapView();
            }
        }

        #endregion

        #region クリップボード関連

        //
        private BitmapSource CurrentBitmapSource
        {
            get { return (this.MainContent?.Content as BitmapContent)?.BitmapSource; }
        }

        //
        public bool CanCopyImageToClipboard()
        {
            return CurrentBitmapSource != null;
        }

        // クリップボードに画像をコピー
        public void CopyImageToClipboard()
        {
            try
            {
                if (CanCopyImageToClipboard())
                {
                    ClipboardUtility.CopyImage(CurrentBitmapSource);
                }
            }
            catch (Exception e)
            {
                new MessageDialog($"{Resources.WordCause}: {e.Message}", Resources.DialogCopyImageErrorTitle).ShowDialog();
            }
        }

        #endregion

        #region 印刷

        /// <summary>
        /// 印刷可能判定
        /// </summary>
        /// <returns></returns>
        public bool CanPrint()
        {
            return this.MainContent != null && this.MainContent.IsValid;
        }

        /// <summary>
        /// 印刷
        /// </summary>
        public void Print(Window owner, FrameworkElement element, Transform transform, double width, double height)
        {
            if (!CanPrint()) return;

            // 掃除しておく
            GC.Collect();

            var contents = this.Contents;
            var mainContent = this.MainContent;

            // スケールモード退避
            var scaleModeMemory = contents.ToDictionary(e => e, e => e.BitmapScalingMode);

            // アニメーション停止
            foreach (var content in contents)
            {
                content.AnimationImageVisibility = Visibility.Visible;
                content.AnimationPlayerVisibility = Visibility.Collapsed;
            }

            // 読み込み停止
            BookHub.Current.IsEnabled = false;

            // スライドショー停止
            SlideShow.Current.PauseSlideShow();

            try
            {
                var context = new PrintContext();
                context.MainContent = mainContent;
                context.Contents = contents;
                context.View = element;
                context.ViewTransform = transform;
                context.ViewWidth = width;
                context.ViewHeight = height;
                context.ViewEffect = ImageEffect.Current.Effect;
                context.Background = ContentCanvasBrush.Current.CreateBackgroundBrush();
                context.BackgroundFront = ContentCanvasBrush.Current.CreateBackgroundFrontBrush(new DpiScale(1, 1));

                var dialog = new PrintWindow(context);
                dialog.Owner = owner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                dialog.ShowDialog();
            }
            finally
            {
                // スケールモード、アニメーション復元
                foreach (var content in contents)
                {
                    content.BitmapScalingMode = scaleModeMemory[content];
                    content.AnimationImageVisibility = Visibility.Collapsed;
                    content.AnimationPlayerVisibility = Visibility.Visible;
                }

                // 読み込み再会
                BookHub.Current.IsEnabled = true;

                // スライドショー再開
                SlideShow.Current.ResumeSlideShow();
            }
        }

        #endregion

        #endregion

        #region IDisposable Support

        private bool _disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (this.Contents != null)
                    {
                        foreach (var content in this.Contents)
                        {
                            content.Dispose();
                        }
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion


        #region Memento
        [DataContract]
        public class Memento
        {
            [DataMember]
            public PageStretchMode StretchMode { get; set; }
            [DataMember]
            public bool IsEnabledNearestNeighbor { get; set; }
            [DataMember]
            public double ContentsSpace { get; set; }
            [DataMember]
            public bool IsAutoRotate { get; set; }
            [DataMember]
            public GridLine.Memento GridLine { get; set; }
        }

        //
        public Memento CreateMemento()
        {
            var memento = new Memento();
            memento.StretchMode = this.StretchMode;
            memento.IsEnabledNearestNeighbor = this.IsEnabledNearestNeighbor;
            memento.ContentsSpace = this.ContentsSpace;
            memento.IsAutoRotate = this.IsAutoRotate;
            memento.GridLine = this.GridLine.CreateMemento();
            return memento;
        }

        //
        public void Restore(Memento memento)
        {
            if (memento == null) return;
            this.StretchMode = memento.StretchMode;
            this.IsEnabledNearestNeighbor = memento.IsEnabledNearestNeighbor;
            this.ContentsSpace = memento.ContentsSpace;
            this.IsAutoRotate = memento.IsAutoRotate;
            this.GridLine.Restore(memento.GridLine);

            //ResetTransform(true); // 不要？
            //UpdateContentSize(); // 不要？
        }

        #endregion
    }
}
