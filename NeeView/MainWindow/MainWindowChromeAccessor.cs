﻿using NeeView.Windows;
using System.Windows;

namespace NeeView
{
    public class MainWindowChromeAccessor : WindowChromeAccessor
    {
        public MainWindowChromeAccessor(Window window) : base(window)
        {
            // NOTE: スライダーを操作しやすいように下辺のみリサイズ領域を狭める
            this.WindowChrome.ResizeBorderThickness = new Thickness(8, 8, 8, 4);
        }
    }
}
