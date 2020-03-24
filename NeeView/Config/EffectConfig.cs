﻿using NeeLaboratory.ComponentModel;
using NeeView.Effects;
using NeeView.Windows.Property;

namespace NeeView
{
    public class EffectConfig : BindableBase
    {
        private bool _isEnabled;
        private EffectType _effectType = EffectType.Level;
        private bool _isHsvMode;

        /// <summary>
        /// エフェクト有効
        /// </summary>
        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetProperty(ref _isEnabled, value); }
        }

        /// <summary>
        /// 適用するエフェクトの種類
        /// </summary>
        public EffectType EffectType
        {
            get { return _effectType; }
            set { SetProperty(ref _effectType, value); }
        }

        /// <summary>
        /// 色をHSV表示
        /// </summary>
        [PropertyMember("@ParamImageEffectIsHsvMode", Tips = "@ParamImageEffectIsHsvModeTips")]
        public bool IsHsvMode
        {
            get { return _isHsvMode; }
            set { SetProperty(ref _isHsvMode, value); }
        }

        /// <summary>
        /// エフェクトコレクション
        /// </summary>
        public ImageEffects ImageEffects { get; set; } = new ImageEffects();
    }

    /// <summary>
    /// エフェクトコレクション
    /// </summary>
    public class ImageEffects
    {
        public LevelEffectUnit Level { get; set; } = new LevelEffectUnit();
        public HsvEffectUnit Hsv { get; set; } = new HsvEffectUnit();
        public ColorSelectEffectUnit ColorSelect { get; set; } = new ColorSelectEffectUnit();
        public BlurEffectUnit Blur { get; set; } = new BlurEffectUnit();
        public BloomEffectUnit Bloom { get; set; } = new BloomEffectUnit();
        public MonochromeEffectUnit Monochrome { get; set; } = new MonochromeEffectUnit();
        public ColorToneEffectUnit ColorTone { get; set; } = new ColorToneEffectUnit();
        public SharpenEffectUnit Sharpen { get; set; } = new SharpenEffectUnit();
        public EmbossedEffectUnit Embossed { get; set; } = new EmbossedEffectUnit();
        public PixelateEffectUnit Pixelate { get; set; } = new PixelateEffectUnit();
        public MagnifyEffectUnit Magnify { get; set; } = new MagnifyEffectUnit();
        public RippleEffectUnit Ripple { get; set; } = new RippleEffectUnit();
        public SwirlEffectUnit Swirl { get; set; } = new SwirlEffectUnit();
    }
}