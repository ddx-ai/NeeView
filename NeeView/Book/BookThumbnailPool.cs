﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeeView
{
    /// <summary>
    /// ThumbnailPool for Panel
    /// </summary>
    public class BookThumbnailPool : ThumbnailPool
    {
        private static BookThumbnailPool? _current;
        public static BookThumbnailPool Current
        {
            get
            {
                _current = _current ?? new BookThumbnailPool();
                return _current;
            }
        }

        public override int Limit => Config.Current.Thumbnail.ThumbnailBookCapacity;
    }
}
