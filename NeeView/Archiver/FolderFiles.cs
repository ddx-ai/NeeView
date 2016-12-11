﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeeView
{
    /// <summary>
    /// アーカイバ：通常ファイル
    /// ディレクトリをアーカイブとみなしてアクセスする
    /// </summary>
    public class FolderFiles : Archiver
    {
        public override string ToString()
        {
            return "フォルダー";
        }

        //
        public override bool IsFileSystem { get; } = true;

        //
        private bool _isDisposed;

        // コンストラクタ
        public FolderFiles(string folderFileName)
        {
            FileName = folderFileName;
        }

        //
        public override void Dispose()
        {
            _isDisposed = true;
            base.Dispose();
        }


        // サポート判定
        public override bool IsSupported()
        {
            return true;
        }

        // リスト取得
        public override List<ArchiveEntry> GetEntries()
        {
            if (_isDisposed) throw new ApplicationException("Archive already colosed.");

            int prefixLen = FileName.Length;
            var list = new List<ArchiveEntry>();

            var directory = new DirectoryInfo(FileName);
            foreach (var info in directory.EnumerateFiles())
            {
                var name = info.FullName.Substring(prefixLen).TrimStart('\\', '/');
                list.Add(new ArchiveEntry()
                {
                    Archiver = this,
                    Id = list.Count,
                    EntryName = name,
                    FileSize = info.Length,
                    LastWriteTime = info.LastWriteTime,
                });
            }
            foreach (var info in directory.EnumerateDirectories())
            {
                var name = info.FullName.Substring(prefixLen).TrimStart('\\', '/') + "\\";
                list.Add(new ArchiveEntry()
                {
                    Archiver = this,
                    Id = list.Count,
                    EntryName = name,
                    FileSize = -1,
                    LastWriteTime = info.LastWriteTime,
                });
            }

            return list;
        }


        // ストリームを開く
        public override Stream OpenStream(ArchiveEntry entry)
        {
            if (_isDisposed) throw new ApplicationException("Archive already colosed.");

            return new FileStream(GetFileSystemPath(entry), FileMode.Open, FileAccess.Read);
        }

        // ファイルパス取得
        public override string GetFileSystemPath(ArchiveEntry entry)
        {
            return Path.Combine(FileName, entry.EntryName);
        }

        // ファイルパス取得
        public override void ExtractToFile(ArchiveEntry entry, string exportFileName, bool isOverwrite)
        {
            if (_isDisposed) throw new ApplicationException("Archive already colosed.");

            File.Copy(GetFileSystemPath(entry), exportFileName, isOverwrite);
        }
    }
}
