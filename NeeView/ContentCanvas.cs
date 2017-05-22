﻿using NeeView.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace NeeView
{

    // 自動回転タイプ
    public enum AutoRotateType
    {
        Right,
        Left,
    }


    //
    public class ContentCanvas : BindableBase
    {
        public static ContentCanvas Current { get; private set; }

        // 空フォルダー通知表示のON/OFF
        private bool _isVisibleEmptyPageMessage = false;
        public bool IsVisibleEmptyPageMessage
        {
            get { return _isVisibleEmptyPageMessage; }
            set { if (_isVisibleEmptyPageMessage != value) { _isVisibleEmptyPageMessage = value; RaisePropertyChanged(); } }
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

        public bool ToggleAutoRotate()
        {
            return IsAutoRotate = !IsAutoRotate;
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
        #region Property: StretchMode
        private PageStretchMode _stretchModePrev = PageStretchMode.Uniform;
        private PageStretchMode _stretchMode = PageStretchMode.Uniform;
        public PageStretchMode StretchMode
        {
            get { return _stretchMode; }
            set
            {
                if (_stretchMode != value)
                {
                    _stretchModePrev = _stretchMode;
                    _stretchMode = value;
                    RaisePropertyChanged();
                    UpdateContentSize();
                    ResetTransform(true);
                }
            }
        }

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


        private ContentCanvasTransform _transform;
        private BookHub _bookHub; // TODO: BookOperation?

        public ContentCanvas(ContentCanvasTransform transform, BookHub bookHub)
        {
            Current = this;

            _transform = transform;
            _transform.TransformChanged += Transform_TransformChanged;

            _bookHub = bookHub;

            // Contents
            Contents = new ObservableCollection<ViewContent>();
            Contents.Add(new ViewContent());
            Contents.Add(new ViewContent());

            MainContent = Contents[0];

            // TODO: BookOperationから？
            _bookHub.ViewContentsChanged +=
                OnViewContentsChanged;
        }

        //
        private void Transform_TransformChanged(object sender, TransformEventArgs e)
        {
            UpdateContentScalingMode();

            _transform.ShowMessage(e.ActionType, MainContent);
        }

        //
        public event EventHandler ContentChanged;


        // コンテンツ
        public ObservableCollection<ViewContent> Contents { get; private set; }

        // 見開き時のメインとなるコンテンツ
        private ViewContent _mainContent;
        public ViewContent MainContent
        {
            get { return _mainContent; }
            set { if (_mainContent != value) { _mainContent = value; RaisePropertyChanged(); } }
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
        public double ContentsSpace
        {
            get { return _contentSpace; }
            set { _contentSpace = value; RaisePropertyChanged(); }
        }



        // コンテンツカラー
        public Color GetContentColor()
        {
            return Contents[Contents[1].IsValid ? 1 : 0].Color;
        }


        /// <summary>
        /// 表示コンテンツ更新
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnViewContentsChanged(object sender, ViewSource e)
        {
            var contents = new List<ViewContent>();

            // ViewContent作成
            if (e?.Sources != null)
            {
                foreach (var source in e.Sources)
                {
                    if (source != null)
                    {
                        var old = Contents[contents.Count];
                        var content = new ViewContent(source, old);
                        contents.Add(content);
                    }
                }
            }

            // ページが存在しない場合、専用メッセージを表示する
            IsVisibleEmptyPageMessage = e != null && contents.Count == 0;

            // メインとなるコンテンツを指定
            MainContent = contents.Count > 0 ? (contents.First().Position < contents.Last().Position ? contents.First() : contents.Last()) : null;

            // ViewModelプロパティに反映
            for (int index = 0; index < 2; ++index)
            {
                Contents[index] = index < contents.Count ? contents[index] : new ViewContent();
            }

            // 自動回転...
            var angle = GetAutoRotateAngle();

            // コンテンツサイズ更新
            UpdateContentSize(angle);

            // 座標初期化
            ResetTransform(false, e != null ? e.Direction : 0, NextViewOrigin);
            NextViewOrigin = DragViewOrigin.None;

            ContentChanged?.Invoke(this, null);

            // GC
            MemoryControl.Current.GarbageCollect();
        }


        /// <summary>
        /// 次のページ更新時の表示開始位置
        /// TODO: ちゃんとBookから情報として上げるようにするべき
        /// </summary>
        public DragViewOrigin NextViewOrigin { get; set; }

        //
        public void ResetTransform(bool isForce)
        {
            ResetTransform(isForce, 0, DragViewOrigin.None);
        }

        // 座標系初期化
        // TODO: ルーペ操作との関係
        public void ResetTransform(bool isForce, int pageDirection, DragViewOrigin viewOrigin)
        {
            // ルーペ解除。ここ？
            if (Preference.Current.loupe_pagechange_reset)
            {
                MouseInputManager.Current.IsLoupeMode = false;
            }

            // ルーペでない場合は初期化
            if (!MouseInputManager.Current.IsLoupeMode)
            {
                // 
                _transform.SetMouseDragSetting(pageDirection, viewOrigin, _bookHub.BookMemento.BookReadOrder);

                // リセット
                var angle = _isAutoRotate ? GetAutoRotateAngle() : double.NaN;
                _transform.Reset(isForce, angle);
            }
        }




        /// <summary>
        /// ページ開始時の回転
        /// </summary>
        /// <returns></returns>
        public double GetAutoRotateAngle()
        {
            var parameter = (AutoRotateCommandParameter)CommandTable.Current[CommandType.ToggleIsAutoRotate].Parameter;

            double angle = this.IsAutoRotateCondition()
                        ? parameter.AutoRotateType == AutoRotateType.Left ? -90.0 : 90.0
                        : 0.0;

            return angle;
        }


        #region ContentSize

        // ビューエリアサイズ
        private double _viewWidth;
        private double _viewHeight;

        // ビューエリアサイズを更新
        public void SetViewSize(double width, double height)
        {
            _viewWidth = width;
            _viewHeight = height;

            UpdateContentSize();
        }


        //
        public void UpdateContentSize(double angle)
        {
            _transform.ContentAngle = angle;
            UpdateContentSize();
        }

        // コンテンツ表示サイズを更新
        public void UpdateContentSize()
        {
            if (!Contents.Any(e => e.IsValid)) return;

            var dpi = App.Config.Dpi;

            // 2ページ表示時は重なり補正を行う
            double offsetWidth = 0;
            if (Contents[0].Size.Width > 0.5 && Contents[1].Size.Width > 0.5)
            {
                offsetWidth = ContentsSpace / dpi.DpiScaleX;
                ContentsMargin = new Thickness(offsetWidth, 0, 0, 0);
            }
            else
            {
                ContentsMargin = new Thickness(0);
            }

            var sizes = CalcContentSize(_viewWidth * dpi.DpiScaleX - offsetWidth, _viewHeight * dpi.DpiScaleY, _transform.ContentAngle);

            for (int i = 0; i < 2; ++i)
            {
                Contents[i].Width = sizes[i].Width / dpi.DpiScaleX;
                Contents[i].Height = sizes[i].Height / dpi.DpiScaleY;
            }

            UpdateContentScalingMode();
        }


        // コンテンツスケーリングモードを更新
        private void UpdateContentScalingMode()
        {
            var dpiScaleX = App.Config.RawDpi.DpiScaleX;
            foreach (var content in Contents)
            {
                if (content.View != null && content.View.Element is Rectangle)
                {
                    double diff = Math.Abs(content.Size.Width - content.Width * dpiScaleX);
                    if (App.Config.IsDpiSquare && diff < 0.1 && _transform.Angle == 0.0 && Math.Abs(_transform.FinalScale - 1.0) < 0.001)
                    {
                        content.BitmapScalingMode = BitmapScalingMode.NearestNeighbor;
                    }
                    else
                    {
                        content.BitmapScalingMode = (IsEnabledNearestNeighbor && content.Size.Width < content.Width * dpiScaleX * _transform.FinalScale) ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality;
                    }
                }
            }
        }

        //
        public bool IsAutoRotateCondition()
        {
            if (!IsAutoRotate) return false;

            var margin = 0.1;
            var viewRatio = GetViewAreaAspectRatio();
            var contentRatio = GetContentAspectRatio();
            return viewRatio >= 1.0 ? contentRatio < (1.0 - margin) : contentRatio > (1.0 + margin);
        }

        //
        public double GetViewAreaAspectRatio()
        {
            return _viewWidth / _viewHeight;
        }

        //
        public double GetContentAspectRatio()
        {
            var size = GetContentSize();
            return size.Width / size.Height;
        }

        //
        private Size GetContentSize()
        {
            var c0 = Contents[0].Size;
            var c1 = Contents[1].Size;

            double rate0 = 1.0;
            double rate1 = 1.0;

            // 2ページ合わせたコンテンツサイズを求める
            if (!Contents[1].IsValid)
            {
                return c0;
            }
            // オリジナルサイズ
            else if (this.StretchMode == PageStretchMode.None)
            {
                return new Size(c0.Width + c1.Width, Math.Max(c0.Height, c1.Height));
            }
            else
            {
                // どちらもImageでない
                if (c0.Width < 0.1 && c1.Width < 0.1)
                {
                    return new Size(1.0, 1.0);
                }

                if (c0.Width == 0) c0 = c1;
                if (c1.Width == 0) c1 = c0;

                // 高さを 高い方に合わせる
                if (c0.Height > c1.Height)
                {
                    rate1 = c0.Height / c1.Height;
                }
                else
                {
                    rate0 = c1.Height / c0.Height;
                }

                // 高さをあわせたときの幅の合計
                return new Size(c0.Width * rate0 + c1.Width * rate1, c0.Height * rate0);
            }
        }


        // ストレッチモードに合わせて各コンテンツのスケールを計算する
        private Size[] CalcContentSize(double width, double height, double angle)
        {
            var c0 = Contents[0].Size;
            var c1 = Contents[1].Size;

            // オリジナルサイズ
            if (this.StretchMode == PageStretchMode.None)
            {
                return new Size[] { c0, c1 };
            }

            double rate0 = 1.0;
            double rate1 = 1.0;

            // 2ページ合わせたコンテンツの表示サイズを求める
            Size content;
            if (!Contents[1].IsValid)
            {
                content = c0;
            }
            else
            {
                // どちらもImageでない
                if (c0.Width < 0.1 && c1.Width < 0.1)
                {
                    return new Size[] { c0, c1 };
                }

                if (c0.Width == 0) c0 = c1;
                if (c1.Width == 0) c1 = c0;

                // 高さを 高い方に合わせる
                if (c0.Height > c1.Height)
                {
                    rate1 = c0.Height / c1.Height;
                }
                else
                {
                    rate0 = c1.Height / c0.Height;
                }

                // 高さをあわせたときの幅の合計
                content = new Size(c0.Width * rate0 + c1.Width * rate1, c0.Height * rate0);
            }

            // 回転反映
            {
                //var angle = 45.0;
                var rect = new Rect(content);
                var m = new Matrix();
                m.Rotate(angle);
                rect.Transform(m);

                content = new Size(rect.Width, rect.Height);
            }


            // ビューエリアサイズに合わせる場合のスケール
            double rateW = width / content.Width;
            double rateH = height / content.Height;

            // 拡大はしない
            if (this.StretchMode == PageStretchMode.Inside)
            {
                if (rateW > 1.0) rateW = 1.0;
                if (rateH > 1.0) rateH = 1.0;
            }
            // 縮小はしない
            else if (this.StretchMode == PageStretchMode.Outside)
            {
                if (rateW < 1.0) rateW = 1.0;
                if (rateH < 1.0) rateH = 1.0;
            }

            // 面積をあわせる
            if (this.StretchMode == PageStretchMode.UniformToSize)
            {
                var viewSize = width * height;
                var contentSize = content.Width * content.Height;
                var rate = Math.Sqrt(viewSize / contentSize);
                rate0 *= rate;
                rate1 *= rate;
            }
            // 高さを合わせる
            else if (this.StretchMode == PageStretchMode.UniformToVertical)
            {
                rate0 *= rateH;
                rate1 *= rateH;
            }
            // 枠いっぱいに広げる
            else if (this.StretchMode == PageStretchMode.UniformToFill)
            {
                if (rateW > rateH)
                {
                    rate0 *= rateW;
                    rate1 *= rateW;
                }
                else
                {
                    rate0 *= rateH;
                    rate1 *= rateH;
                }
            }
            // 枠に収めるように広げる
            else
            {
                if (rateW < rateH)
                {
                    rate0 *= rateW;
                    rate1 *= rateW;
                }
                else
                {
                    rate0 *= rateH;
                    rate1 *= rateH;
                }
            }

            var s0 = new Size(c0.Width * rate0, c0.Height * rate0);
            var s1 = new Size(c1.Width * rate1, c1.Height * rate1);
            return new Size[] { s0, s1 };
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
        }

        //
        public Memento CreateMemento()
        {
            var memento = new Memento();
            memento.StretchMode = this.StretchMode;
            memento.IsEnabledNearestNeighbor = this.IsEnabledNearestNeighbor;
            memento.ContentsSpace = this.ContentsSpace;
            memento.IsAutoRotate = this.IsAutoRotate;
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

            //ResetTransform(true); // 不要？
            //UpdateContentSize(); // 不要？
        }

        #endregion
    }
}
