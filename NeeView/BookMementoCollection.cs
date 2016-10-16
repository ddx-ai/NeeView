﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeeView
{
    // BookMementoCollectionChangedイベントの種類
    public enum BookMementoCollectionChangedType
    {
        Load,
        Clear,
        Add,
        Update,
        Remove,
    }

    // BookMementoCollectionChangedイベントの引数
    public class BookMementoCollectionChangedArgs : EventArgs
    {
        public BookMementoCollectionChangedType HistoryChangedType { get; set; }
        public string Key { get; set; }

        public BookMementoCollectionChangedArgs(BookMementoCollectionChangedType type, string key)
        {
            HistoryChangedType = type;
            Key = key;
        }
    }


    /// <summary>
    /// BookMementoUnit用ノード
    /// </summary>
    public class BookMementoUnitNode : IHasPage
    {
        public BookMementoUnit Value { get; set; }

        public BookMementoUnitNode(BookMementoUnit value)
        {
            Value = value;
        }

        public Page GetPage()
        {
            return Value?.ArchivePage;
        }
    }

    /// <summary>
    /// 高速検索用BookMemento辞書
    /// 履歴、ブックマーク共有の辞書です
    /// SQL使いたくなってきた..
    /// </summary>
    public class BookMementoUnit : IHasPage
    {
        // 履歴用リンク
        public LinkedListNode<BookMementoUnit> HistoryNode { get; set; }

        // ブックマーク用リンク
        public BookMementoUnitNode BookmarkNode { get; set; }

        // ページマーク用リンク
        public BookMementoUnitNode PagemarkNode { get; set; }

        // 本体
        public Book.Memento Memento { get; set; }

        //
        public override string ToString()
        {
            return Memento?.Place ?? base.ToString();
        }

        public static event EventHandler<Page> ThumbnailChanged;

        /// <summary>
        /// ArchivePage Property.
        /// サムネイル用。保存しません
        /// </summary>
        private ArchivePage _archivePage;
        public ArchivePage ArchivePage
        {
            get
            {
                if (_archivePage == null && Memento != null)
                {
                    _archivePage = new ArchivePage(Memento.Place);
                    _archivePage.ThumbnailChanged += (s, e) => ThumbnailChanged?.Invoke(this, _archivePage);
                }
                return _archivePage;
            }
            set { _archivePage = value; }
        }

        //
        public Page GetPage()
        {
            return ArchivePage;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public class BookMementoCollection
    {
        public Dictionary<string, BookMementoUnit> Items { get; set; } = new Dictionary<string, BookMementoUnit>();

        private BookMementoUnit _LastFindUnit;

        //
        public void Add(BookMementoUnit unit)
        {
            Debug.Assert(unit != null);
            Debug.Assert(unit.Memento != null);
            Debug.Assert(unit.Memento.Place != null);
            Debug.Assert(unit.HistoryNode != null || unit.BookmarkNode != null || unit.PagemarkNode != null);

            Items.Add(unit.Memento.Place, unit);
        }

        //
        public BookMementoUnit Find(string place)
        {
            if (place == null) return null;

            // 最後に検索されたユニットは再度検索される時に高速にする
            if (place == _LastFindUnit?.Memento.Place) return _LastFindUnit;

            BookMementoUnit unit;
            Items.TryGetValue(place, out unit);
            _LastFindUnit = unit;
            return unit;
        }
    }
}
