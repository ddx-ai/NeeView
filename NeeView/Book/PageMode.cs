﻿using System;
using System.Diagnostics;

namespace NeeView
{
    // ページ表示モード
    public enum PageMode
    {
        [AliasName]
        SinglePage,

        [AliasName]
        WidePage,

        [AliasName]
        Panorama,
    }


    public static class PageModeExtension
    {
        public static PageMode GetToggle(this PageMode mode, int direction)
        {
            Debug.Assert(direction == -1 || direction == +1);
            var length = Enum.GetNames(typeof(PageMode)).Length;
            return (PageMode)(((int)mode + length + direction) % length);
        }

        public static int Size(this PageMode mode)
        {
            return mode == PageMode.WidePage ? 2 : 1;
        }

        public static PageMode Validate(this PageMode mode)
        {
            if (mode < PageMode.SinglePage) return PageMode.SinglePage;
            if (mode > PageMode.Panorama) return PageMode.Panorama;
            return mode;
        }

        public static bool IsStaticFrame(this PageMode mode)
        {
            return mode != PageMode.Panorama;
        }
    }
}
