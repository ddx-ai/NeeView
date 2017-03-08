﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace NeeView
{
    /// <summary>
    /// アーカイブコンテンツ
    /// 対象のサムネイルを作成
    /// </summary>
    public class ArchiveContent : BitmapContent
    {
        private string _entryName;

        /// <summary>
        /// コンスラクタ
        /// </summary>
        /// <param name="entry">対象アーカイブのエントリ</param>
        /// <param name="entryName">サムネイル指定ページ</param>
        public ArchiveContent(ArchiveEntry entry, string entryName) : base(entry)
        {
            _entryName = entryName;

            PageMessage = new PageMessage()
            {
                Icon = FilePageIcon.Alart,
                Message = "このページはサムネイル作成専用です",
            };
            IsLoaded = true;
        }

        /// <summary>
        /// コンテンツロードは非サポート
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public override Task LoadAsync(CancellationToken token)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// サムネイル初期化
        /// ページ指定があるため特殊
        /// </summary>
        public override void InitializeThumbnail()
        {
            Thumbnail.Initialize(Entry, _entryName);
        }

        /// <summary>
        /// サムネイルロード
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task LoadThumbnailAsync(CancellationToken token)
        {
            if (Thumbnail.IsValid) return;

            try
            {
                var bitmapSource = await LoadArchiveBitmapAsync(Entry, _entryName, token);
                Thumbnail.Initialize(bitmapSource);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                // 例外無効
                Debug.WriteLine($"LoadThumbnail: {e.Message}");
            }
        }


        /// <summary>
        /// サムネイル読込
        /// ページ指定がない場合は名前順で先頭のページ
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="entryName"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<BitmapSource> LoadArchiveBitmapAsync(ArchiveEntry entry, string entryName, CancellationToken token)
        {
            using (var archiver = ModelContext.ArchiverManager.CreateArchiver(entry.EntryName, null))
            {
                using (var collector = new EntryCollection(archiver, false))
                {
                    if (entryName != null)
                    {
                        await collector.SelectAsync(entryName, token);
                    }
                    else
                    {
                        await collector.FirstOneAsync(token);
                    }

                    var select = collector.Collection.FirstOrDefault();

                    if (select != null)
                    {
                        return await LoadBitmapAsync(select, token);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }
    }

}
