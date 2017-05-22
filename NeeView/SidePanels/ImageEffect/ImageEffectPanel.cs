﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using NeeView.ComponentModel;
using NeeView.Effects;
using NeeView.Windows.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NeeView
{
    /// <summary>
    /// ImageEffect : Panel
    /// </summary>
    public class ImageEffectPanel : BindableBase, IPanel
    {
        public string TypeCode => nameof(ImageEffectPanel);

        public ImageSource Icon { get; private set; }

        public Thickness IconMargin { get; private set; }

        public string IconTips => "エフェクト";

        public FrameworkElement View { get; private set; }

        public bool IsVisibleLock => false;


        //
        public ImageEffectPanel(ImageEffect model)
        {
            View = new ImageEffectView(model);

            Icon = App.Current.MainWindow.Resources["pic_toy_24px"] as ImageSource;
            IconMargin = new Thickness(8);
        }
    }
}
