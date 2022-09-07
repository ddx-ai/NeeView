﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NeeView
{
    /// <summary>
    /// 高速検索用BookMemento辞書
    /// 履歴、ブックマーク共有の辞書です
    /// SQL使いたくなってきた..
    /// </summary>
    public class BookMementoUnit : IHasPage
    {
        private Page? _archivePage;


        private BookMementoUnit(Book.Memento memento)
        {
            Memento = memento;
        }


        public Book.Memento Memento { get; set; }

        public string Path => Memento.Path;

        public override string? ToString()
        {
            var s = Memento?.Path;
            return string.IsNullOrEmpty(s) ? base.ToString() : s;
        }

        #region for Thumbnail

        /// <summary>
        /// ArchivePage Property.
        /// サムネイル用
        /// </summary>
        public Page ArchivePage
        {
            get
            {
                if (_archivePage == null)
                {
                    _archivePage = new Page("", new ArchiveContent(Memento.Path));
                    _archivePage.Thumbnail.IsCacheEnabled = true;
                    _archivePage.Thumbnail.Touched += Thumbnail_Touched;
                }
                return _archivePage;
            }
        }

        private void Thumbnail_Touched(object? sender, EventArgs e)
        {
            if (sender is not Thumbnail thumbnail) return;

            BookThumbnailPool.Current.Add(thumbnail);
        }

        #endregion

        #region IHasPage Support

        public Page GetPage()
        {
            return ArchivePage;
        }

        #endregion

        public static BookMementoUnit Create(Book.Memento memento)
        {
            return new BookMementoUnit(memento);
        }
    }
}
