﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using NeeView.ComponentModel;
using NeeView.Effects;
using System.ComponentModel;

namespace NeeView
{
    /// <summary>
    /// ImageEffect : ViewModel
    /// </summary>
    public class ImageEffectViewModel : BindableBase
    {
        /// <summary>
        /// Model property.
        /// </summary>
        public ImageEffect Model
        {
            get { return _model; }
            set { if (_model != value) { _model = value; RaisePropertyChanged(); } }
        }

        private ImageEffect _model;

        //
        public ImageEffectViewModel(ImageEffect model)
        {
            _model = model;
        }
    }

}
