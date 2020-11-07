﻿using System;
using NeeView.Runtime.LayoutPanel;

namespace NeeView
{
    /// <summary>
    /// パネルドロップイベント
    /// </summary>
    public class LayoutPanelDropedEventArgs : EventArgs
    {
        public LayoutPanelDropedEventArgs(LayoutPanel panel, int index)
        {
            Panel = panel;
            Index = index;
        }

        /// <summary>
        /// ドロップされたパネル
        /// </summary>
        public LayoutPanel Panel { get; set; }

        /// <summary>
        /// 挿入位置
        /// </summary>
        public int Index { get; set; }
    }
}
