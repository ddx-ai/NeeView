﻿using NeeLaboratory.ComponentModel;
using NeeView.Windows.Property;
using System;
using System.Text.Json.Serialization;

namespace NeeView
{
    public class ViewConfig : BindableBaseFull
    {
        private PageStretchMode _stretchMode = PageStretchMode.Uniform;
        private PageStretchMode _validStretchMode = PageStretchMode.Uniform;
        private bool _allowStretchScaleUp = true;
        private bool _allowStretchScaleDown = true;
        private bool _allowFileContentAutoRotate;
        private bool _isLimitMove = true;
        private DragControlCenter _rotateCenter;
        private DragControlCenter _scaleCenter;
        private DragControlCenter _flipCenter;
        private bool _isKeepScale;
        private bool _isKeepAngle;
        private bool _isKeepFlip;
        private bool _isViewStartPositionCenter;
        private double _angleFrequency = 0;
        private bool _isBaseScaleEnabled;
        private double _baseScale = 1.0;
        private bool _isRotateStretchEnabled = true;
        private double _mainViewMargin;
        private bool _isKeepScaleBooks;
        private bool _isKeepAngleBooks;
        private bool _isKeepFlipBooks;
        private bool _isKeepPageTransform;
        private double _scrollDuration = 0.2;
        private double _pageMoveDuration = 0.0;
        private IHasAutoRotate? _autoRotateSource;


        // 回転の中心
        [PropertyMember]
        public DragControlCenter RotateCenter
        {
            get { return _rotateCenter; }
            set { SetProperty(ref _rotateCenter, value); }
        }

        // 拡大の中心
        [PropertyMember]
        public DragControlCenter ScaleCenter
        {
            get { return _scaleCenter; }
            set { SetProperty(ref _scaleCenter, value); }
        }

        // 反転の中心
        [PropertyMember]
        public DragControlCenter FlipCenter
        {
            get { return _flipCenter; }
            set { SetProperty(ref _flipCenter, value); }
        }

        // 拡大率キープ
        [PropertyMember]
        public bool IsKeepScale
        {
            get { return _isKeepScale; }
            set { SetProperty(ref _isKeepScale, value); }
        }

        // ブック間の拡大率キープ
        [PropertyMember]
        public bool IsKeepScaleBooks
        {
            get { return _isKeepScaleBooks; }
            set { SetProperty(ref _isKeepScaleBooks, value); }
        }

        // 回転キープ
        [PropertyMember]
        public bool IsKeepAngle
        {
            get { return _isKeepAngle; }
            set { SetProperty(ref _isKeepAngle, value); }
        }

        // ブック間の回転キープ
        [PropertyMember]
        public bool IsKeepAngleBooks
        {
            get { return _isKeepAngleBooks; }
            set { SetProperty(ref _isKeepAngleBooks, value); }
        }

        // 反転キープ
        [PropertyMember]
        public bool IsKeepFlip
        {
            get { return _isKeepFlip; }
            set { SetProperty(ref _isKeepFlip, value); }
        }

        // ブック間の反転キープ
        [PropertyMember]
        public bool IsKeepFlipBooks
        {
            get { return _isKeepFlipBooks; }
            set { SetProperty(ref _isKeepFlipBooks, value); }
        }

        // 表示開始時の基準
        [PropertyMember]
        public bool IsViewStartPositionCenter
        {
            get { return _isViewStartPositionCenter; }
            set { SetProperty(ref _isViewStartPositionCenter, value); }
        }

        // 回転スナップ。0で無効
        [PropertyMember]
        public double AngleFrequency
        {
            get { return _angleFrequency; }
            set { SetProperty(ref _angleFrequency, value); }
        }

        // ウィンドウ枠内の移動に制限する
        [PropertyMember]
        public bool IsLimitMove
        {
            get { return _isLimitMove; }
            set { SetProperty(ref _isLimitMove, value); }
        }

        // スケールモード
        [PropertyMember]
        public PageStretchMode StretchMode
        {
            get { return _stretchMode; }
            set
            {
                if (_stretchMode != value)
                {
                    _stretchMode = value;
                    _validStretchMode = _stretchMode != PageStretchMode.None ? value : _validStretchMode;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ValidStretchMode));
                }
            }
        }

        // 有効なスケールモード
        // StretchMode.None のトグルに使用する
        public PageStretchMode ValidStretchMode => _validStretchMode;

        // スケールモード・拡大許可
        [PropertyMember]
        public bool AllowStretchScaleUp
        {
            get { return _allowStretchScaleUp; }
            set { SetProperty(ref _allowStretchScaleUp, value); }
        }

        // スケールモード・縮小許可
        [PropertyMember]
        public bool AllowStretchScaleDown
        {
            get { return _allowStretchScaleDown; }
            set { SetProperty(ref _allowStretchScaleDown, value); }
        }

        // 基底スケール有効
        [PropertyMember]
        public bool IsBaseScaleEnabled
        {
            get { return _isBaseScaleEnabled; }
            set { SetProperty(ref _isBaseScaleEnabled, value); }
        }

        // 基底スケール
        [PropertyPercent(0.1, 2.0, TickFrequency = 0.01)]
        public double BaseScale
        {
            get { return _baseScale; }
            set { SetProperty(ref _baseScale, Math.Max(value, 0.0)); }
        }


        // ファイルコンテンツの自動回転を許可する
        public bool AllowFileContentAutoRotate
        {
            get { return _allowFileContentAutoRotate; }
            set { SetProperty(ref _allowFileContentAutoRotate, value); }
        }

        // ナビゲーターボタンによる回転にストレッチを適用
        [PropertyMember]
        public bool IsRotateStretchEnabled
        {
            get { return _isRotateStretchEnabled; }
            set { SetProperty(ref _isRotateStretchEnabled, value); }
        }

        // ビューエリアの余白
        [PropertyRange(0.0, 100.0)]
        public double MainViewMargin
        {
            get { return _mainViewMargin; }
            set { SetProperty(ref _mainViewMargin, value); }
        }

        // ページトランスフォームの維持
        [PropertyMember]
        public bool IsKeepPageTransform
        {
            get { return _isKeepPageTransform; }
            set { SetProperty(ref _isKeepPageTransform, value); }
        }


        // スクロール時間 (秒)
        [PropertyRange(0.0, 1.0, TickFrequency = 0.1, IsEditable = true)]
        public double ScrollDuration
        {
            get { return _scrollDuration; }
            set { SetProperty(ref _scrollDuration, value); }
        }

        // ページ変更時間(秒)
        [PropertyRange(0.0, 1.0, TickFrequency = 0.1, IsEditable = true)]
        public double PageMoveDuration
        {
            get { return _pageMoveDuration; }
            set { SetProperty(ref _pageMoveDuration, value); }
        }

        #region Obsolete

        [Obsolete("Typo"), Alternative(nameof(MainViewMargin), 40, ScriptErrorLevel.Info)] // ver.40
        [JsonIgnore]
        public double MainViewMergin
        {
            get { return MainViewMargin; }
            set { MainViewMargin = value; }
        }

        [Obsolete("Typo json interface"), PropertyMapIgnore]
        [JsonPropertyName("MainViewMergin"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double MainViewMergin_Typo
        {
            get { return 0.0; }
            set { MainViewMargin = value; }
        }

        // スクリプト互換性用：自動回転左/右
        // 設定値は BookSetting のものを使用する
        [PropertyMember]
        [Obsolete("no used"), Alternative($"nv.Config.BookSetting.AutoRotate", 40, ScriptErrorLevel.Info)] // ver.40
        [JsonIgnore]
        public AutoRotateType AutoRotate
        {
            get { return _autoRotateSource?.AutoRotate ?? default; }
            set { if (_autoRotateSource is not null) _autoRotateSource.AutoRotate = value; }
        }


        /// <summary>
        /// AutoRotate プロパティを外部情報に依存させる
        /// </summary>
        /// <param name="autoRotateSource"></param>
        public void SetBookSettingSource(IHasAutoRotate autoRotateSource)
        {
            _autoRotateSource = autoRotateSource;
        }

        #endregion
    }
}
