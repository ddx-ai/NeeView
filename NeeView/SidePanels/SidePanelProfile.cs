﻿using NeeLaboratory;
using NeeLaboratory.ComponentModel;
using NeeView.Windows.Property;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NeeView
{
    public class SidePanelProfile : BindableBase
    {
        public static SidePanelProfile Current { get; private set; }

        private static readonly PanelListItemProfile _defaultNormalItemProfile = new PanelListItemProfile(PanelListItemImageShape.Original, 0, false, true, false, 0.0);
        private static readonly PanelListItemProfile _defaultContentItemProfile = new PanelListItemProfile(PanelListItemImageShape.Original, 64, true, true, false, 0.5);
        private static readonly PanelListItemProfile _defaultBannertemProfile = new PanelListItemProfile(PanelListItemImageShape.Original, 200, false, true, false, 0.0);
        private static readonly PanelListItemProfile _defaultThumbnailItemProfile = new PanelListItemProfile(PanelListItemImageShape.Original, 128, false, true, false, 0.0);

        private double _opacity = 1.0;
        private SolidColorBrush _backgroundBrush;
        private SolidColorBrush _baseBrush;
        private SolidColorBrush _iconBackgroundBrush;
        private string _fontName = SystemFonts.MessageFontFamily.Source;
        private double _folderTreeFontSize = 12;
        private double _fontSize = 15.0;

        public SidePanelProfile()
        {
            Current = this;

            _normalItemProfile = _defaultNormalItemProfile.Clone();
            _contentItemProfile = _defaultContentItemProfile.Clone();
            _bannerItemProfile = _defaultBannertemProfile.Clone();
            _thumbnailItemProfile = _defaultThumbnailItemProfile.Clone();

            SetFontFamilyResource(_fontName);
            SetFontSizeResource(_fontSize);
            SetFolderTreeFontSizeResource(_folderTreeFontSize);

            MainWindowModel.Current.ThemeColorChanged += (s, e) => RefreshBrushes();
            MainWindowModel.Current.CanHidePanelChanged += (s, e) => RefreshBrushes();

            RefreshBrushes();
        }


        [PropertyMember("@ParamSidePanelIsLeftRightKeyEnabled", Tips = "@ParamSidePanelIsLeftRightKeyEnabledTips")]
        public bool IsLeftRightKeyEnabled { get; set; } = true;

        [PropertyMember("@ParamSidePanelHitTestMargin")]
        public double HitTestMargin { get; set; } = 32.0;

        [PropertyPercent("@ParamSidePanelOpacity", Tips = "@ParamSidePanelOpacityTips")]
        public double Opacity
        {
            get { return _opacity; }
            set
            {
                if (SetProperty(ref _opacity, value))
                {
                    RefreshBrushes();
                }
            }
        }

        public SolidColorBrush BackgroundBrush
        {
            get { return _backgroundBrush; }
            set { SetProperty(ref _backgroundBrush, value); }
        }

        public SolidColorBrush BaseBrush
        {
            get { return _baseBrush; }
            set { SetProperty(ref _baseBrush, value); }
        }

        public SolidColorBrush IconBackgroundBrush
        {
            get { return _iconBackgroundBrush; }
            set { SetProperty(ref _iconBackgroundBrush, value); }
        }




        [PropertyMember("@ParamListItemFontName")]
        public string FontName
        {
            get
            {
                return _fontName;
            }
            set
            {
                value = value ?? SystemFonts.MessageFontFamily.Source;
                if (_fontName != value)
                {
                    try
                    {
                        _fontName = value;
                        SetFontFamilyResource(_fontName);
                        RaisePropertyChanged();

                        _contentItemProfile.UpdateTextHeight();
                        _bannerItemProfile.UpdateTextHeight();
                        _thumbnailItemProfile.UpdateTextHeight();
                    }
                    catch
                    {
                        // nop.
                    }
                }
            }
        }


        [PropertyRange("@ParamListItemFontSize", 8, 24, TickFrequency = 0.5, IsEditable = true)]
        public double FontSize
        {
            get { return _fontSize; }
            set
            {
                value = Math.Max(1, value);
                if (_fontSize != value)
                {
                    _fontSize = value;
                    SetFontSizeResource(_fontSize);
                    RaisePropertyChanged();

                    _contentItemProfile.UpdateTextHeight();
                    _bannerItemProfile.UpdateTextHeight();
                    _thumbnailItemProfile.UpdateTextHeight();
                }
            }
        }

        [PropertyRange("@ParamListItemFolderTreeFontSize", 8, 24, TickFrequency = 0.5, IsEditable = true)]
        public double FolderTreeFontSize
        {
            get { return _folderTreeFontSize; }
            set
            {
                value = Math.Max(1, value);
                if (_folderTreeFontSize != value)
                {
                    _folderTreeFontSize = value;
                    SetFolderTreeFontSizeResource(_folderTreeFontSize);
                    RaisePropertyChanged();
                }
            }
        }


        private PanelListItemProfile _normalItemProfile;
        public PanelListItemProfile NormalItemProfile
        {
            get { return _normalItemProfile; }
            set { SetProperty(ref _normalItemProfile, value); }
        }


        private PanelListItemProfile _contentItemProfile;
        public PanelListItemProfile ContentItemProfile
        {
            get { return _contentItemProfile; }
            set { SetProperty(ref _contentItemProfile, value); }
        }


        private PanelListItemProfile _bannerItemProfile;
        public PanelListItemProfile BannerItemProfile
        {
            get { return _bannerItemProfile; }
            set { SetProperty(ref _bannerItemProfile, value); }
        }


        private PanelListItemProfile _thumbnailItemProfile;
        public PanelListItemProfile ThumbnailItemProfile
        {
            get { return _thumbnailItemProfile; }
            set { SetProperty(ref _thumbnailItemProfile, value); }
        }



        [PropertyRange("@ParamListItemContentImageWidth", 0, 256, TickFrequency = 8, Format = "{0}×{0}")]
        public int ContentItemImageWidth
        {
            get { return _contentItemProfile.ImageWidth; }
            set { _contentItemProfile.ImageWidth = MathUtility.Clamp(value, 0, 256); }
        }

        [PropertyMember("@ParamListItemContentImageShape")]
        public PanelListItemImageShape ContentItemImageShape
        {
            get { return _contentItemProfile.ImageShape; }
            set { _contentItemProfile.ImageShape = value; }
        }

        [PropertyMember("@ParamListItemContentImagePopup", Tips = "@ParamListItemContentImagePopupTips")]
        public bool ContentItemIsImagePopupEnabled
        {
            get { return _contentItemProfile.IsImagePopupEnabled; }
            set { _contentItemProfile.IsImagePopupEnabled = value; }
        }


        [PropertyMember("@ParamListItemContentIsTextWrapped")]
        public bool ContentItemIsTextWrapped
        {
            get { return _contentItemProfile.IsTextWrapped; }
            set { _contentItemProfile.IsTextWrapped = value; }
        }

        [PropertyRange("@ParamListItemContentNoteOpacity", 0.0, 1.0)]
        public double ContentItemNoteOpacity
        {
            get { return _contentItemProfile.NoteOpacity; }
            set { _contentItemProfile.NoteOpacity = value; }
        }


        [PropertyRange("@ParamListItemBannerImageWidth", 0, 512, TickFrequency = 8, Tips = "@ParamListItemBannerImageWidthTips")]
        public int BannerItemImageWidth
        {
            get { return _bannerItemProfile.ImageWidth; }
            set { _bannerItemProfile.ImageWidth = MathUtility.Clamp(value, 0, 512); }
        }

        [PropertyMember("@ParamListItemBannerIsTextWrapped")]
        public bool BannerItemIsTextWrapped
        {
            get { return _bannerItemProfile.IsTextWrapped; }
            set { _bannerItemProfile.IsTextWrapped = value; }
        }


        [PropertyRange("@ParamListItemThumbnailImageWidth", 64, 256, TickFrequency = 8, Format = "{0}×{0}")]
        public int ThumbnailItemImageWidth
        {
            get { return _thumbnailItemProfile.ImageWidth; }
            set { _thumbnailItemProfile.ImageWidth = MathUtility.Clamp(value, 64, 256); }
        }

        [PropertyMember("@ParamListItemThumbnailImageShape")]
        public PanelListItemImageShape ThumbnailItemImageShape
        {
            get { return _thumbnailItemProfile.ImageShape; }
            set { _thumbnailItemProfile.ImageShape = value; }
        }

        [PropertyMember("@ParamListItemThumbnailNameVisibled")]
        public bool ThumbnailItemIsTextVisibled
        {
            get { return _thumbnailItemProfile.IsTextVisibled; }
            set { _thumbnailItemProfile.IsTextVisibled = value; }
        }

        [PropertyMember("@ParamListItemThumbnailIsTextWrapped")]
        public bool ThumbnailItemIsTextWrapped
        {
            get { return _thumbnailItemProfile.IsTextWrapped; }
            set { _thumbnailItemProfile.IsTextWrapped = value; }
        }


        private void RefreshBrushes()
        {
            var opacity = MainWindowModel.Current.CanHidePanel ? _opacity : 1.0;

            BackgroundBrush = CreatePanelBrush((SolidColorBrush)App.Current.Resources["NVBackground"], opacity);
            BaseBrush = CreatePanelBrush((SolidColorBrush)App.Current.Resources["NVBaseBrush"], opacity);
            IconBackgroundBrush = CreatePanelBrush((SolidColorBrush)App.Current.Resources["NVPanelIconBackground"], opacity);
        }

        private SolidColorBrush CreatePanelBrush(SolidColorBrush source, double opacity)
        {
            if (opacity < 1.0)
            {
                var color = source.Color;
                color.A = (byte)NeeLaboratory.MathUtility.Clamp((int)(opacity * 0xFF), 0x00, 0xFF);
                return new SolidColorBrush(color);
            }
            else
            {
                return source;
            }
        }


        // リソースにFontFamily適用
        private void SetFontFamilyResource(string fontName)
        {
            var fontFamily = fontName != null ? new FontFamily(fontName) : SystemFonts.MessageFontFamily;
            App.Current.Resources["PanelFontFamily"] = fontFamily;
        }

        // リソースにFontSize適用
        private void SetFontSizeResource(double fontSize)
        {
            App.Current.Resources["PanelFontSize"] = fontSize;
        }

        // リソースにFolderTreeFontSize適用
        private void SetFolderTreeFontSizeResource(double fontSize)
        {
            App.Current.Resources["FolderTreeFontSize"] = fontSize;
        }


        public void ValidatePanelListItemProfile()
        {
            _contentItemProfile.UpdateTextHeight();
            _bannerItemProfile.UpdateTextHeight();
            _thumbnailItemProfile.UpdateTextHeight();
        }

        #region Memento

        [DataContract]
        public class Memento
        {
            [DataMember, DefaultValue(true)]
            public bool IsLeftRightKeyEnabled { get; set; }

            [DataMember, DefaultValue(32.0)]
            public double HitTestMargin { get; set; }

            [DataMember, DefaultValue(1.0)]
            public double Opacity { get; set; }

            [DataMember]
            public string FontName { get; set; }

            [DataMember, DefaultValue(15.0)]
            public double FontSize { get; set; }

            [DataMember, DefaultValue(12.0)]
            public double FolderTreeFontSize { get; set; }

            [DataMember]
            public PanelListItemProfile ContentItemProfile { get; set; }

            [DataMember]
            public PanelListItemProfile BannerItemProfile { get; set; }

            [DataMember]
            public PanelListItemProfile ThumbnailItemProfile { get; set; }

            [OnDeserializing]
            private void Deserializing(StreamingContext c)
            {
                this.InitializePropertyDefaultValues();
            }
        }

        //
        public Memento CreateMemento()
        {
            var memento = new Memento();

            memento.IsLeftRightKeyEnabled = this.IsLeftRightKeyEnabled;
            memento.HitTestMargin = this.HitTestMargin;
            memento.Opacity = this.Opacity;
            memento.FontName = this.FontName;
            memento.FontSize = this.FontSize;
            memento.FolderTreeFontSize = this.FolderTreeFontSize;
            memento.ContentItemProfile = this.ContentItemProfile;
            memento.BannerItemProfile = this.BannerItemProfile;
            memento.ThumbnailItemProfile = this.ThumbnailItemProfile;

            return memento;
        }

        //
        public void Restore(Memento memento)
        {
            if (memento == null) return;

            this.IsLeftRightKeyEnabled = memento.IsLeftRightKeyEnabled;
            this.HitTestMargin = memento.HitTestMargin;
            this.Opacity = memento.Opacity;
            this.FontName = memento.FontName;
            this.FontSize = memento.FontSize;
            this.FolderTreeFontSize = memento.FolderTreeFontSize;
            this.ContentItemProfile = memento.ContentItemProfile ?? _defaultContentItemProfile.Clone();
            this.BannerItemProfile = memento.BannerItemProfile ?? _defaultBannertemProfile.Clone();
            this.ThumbnailItemProfile = memento.ThumbnailItemProfile ?? _defaultThumbnailItemProfile.Clone();

            ValidatePanelListItemProfile();
        }

        #endregion
    }

    // TODO: 定義位置
    public static class ObjectExtensions
    {
        public static T DeepCopy<T>(T source)
        {
            var serializer = new DataContractSerializer(typeof(T));
            using (var mem = new MemoryStream())
            {
                serializer.WriteObject(mem, source);
                mem.Position = 0;
                return (T)serializer.ReadObject(mem);
            }
        }
    }
}
