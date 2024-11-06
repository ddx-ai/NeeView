﻿using NeeView.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace NeeView
{
    public enum QueryScheme
    {
        [AliasName]
        File = 0,

        [AliasName]
        Root,

        [AliasName]
        Bookmark,

        [AliasName]
        QuickAccess,

        [AliasName]
        Script,
    }

    public static class QuerySchemeExtensions
    {
        static readonly Dictionary<QueryScheme, string> _map = new()
        {
            [QueryScheme.File] = "file:",
            [QueryScheme.Root] = "root:",
            [QueryScheme.Bookmark] = "bookmark:",
            [QueryScheme.QuickAccess] = "quickaccess:",
            [QueryScheme.Script] = "script:",
        };

        static Dictionary<QueryScheme, ImageSource>? _imageMap;

        static Dictionary<QueryScheme, ImageSource>? _thumbnailImageMap;


        [MemberNotNull(nameof(_imageMap))]
        private static void InitializeImageMap()
        {
            if (_imageMap != null) return;

            _imageMap = AppDispatcher.Invoke(() =>
            {
                return new Dictionary<QueryScheme, ImageSource>()
                {
                    [QueryScheme.File] = MainWindow.Current.Resources["ic_desktop_windows_24px"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.Root] = MainWindow.Current.Resources["ic_bookshelf"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.Bookmark] = MainWindow.Current.Resources["ic_grade_24px"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.QuickAccess] = MainWindow.Current.Resources["ic_lightning"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.Script] = MainWindow.Current.Resources["ic_javascript_24px"] as ImageSource ?? throw new DirectoryNotFoundException(),
                };
            });
        }

        [MemberNotNull(nameof(_thumbnailImageMap))]
        private static void InitializeThumbnailImageMap()
        {
            if (_thumbnailImageMap != null) return;

            _thumbnailImageMap = AppDispatcher.Invoke(() =>
            {
                return new Dictionary<QueryScheme, ImageSource>()
                {
                    [QueryScheme.File] = MainWindow.Current.Resources["ic_desktop_windows_24px_t"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.Root] = MainWindow.Current.Resources["ic_bookshelf"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.Bookmark] = MainWindow.Current.Resources["ic_grade_24px_t"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.QuickAccess] = MainWindow.Current.Resources["ic_lightning"] as ImageSource ?? throw new DirectoryNotFoundException(),
                    [QueryScheme.Script] = MainWindow.Current.Resources["ic_javascript_24px"] as ImageSource ?? throw new DirectoryNotFoundException(),
                };
            });
        }

        public static string ToSchemeString(this QueryScheme scheme)
        {
            return _map[scheme];
        }

        public static QueryScheme GetScheme(string path)
        {
            return _map.FirstOrDefault(e => path.StartsWith(e.Value, StringComparison.Ordinal)).Key;
        }

        public static ImageSource ToImage(this QueryScheme scheme)
        {
            InitializeImageMap();
            return _imageMap[scheme];
        }

        public static ImageSource ToThumbnailImage(this QueryScheme scheme)
        {
            InitializeThumbnailImageMap();
            return _thumbnailImageMap[scheme];
        }

        public static bool IsMatch(this QueryScheme scheme, string path)
        {
            return path.StartsWith(scheme.ToSchemeString(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// パスのクエリパラメータを分解する.
    /// immutable.
    /// </summary>
    [Serializable]
    public sealed class QueryPath : IEquatable<QueryPath>
    {
        public static QueryPath Empty { get; } = new QueryPath(QueryScheme.Root);

        static readonly string _querySearch = "?search=";

        public QueryPath(string? source)
        {
            var rest = source;
            rest = TakeQuerySearch(rest, out _search);
            rest = TakeScheme(rest, out _scheme);
            _path = GetValidatePath(rest, _scheme);
        }

        public QueryPath(string? source, string? search)
        {
            var rest = source;
            _search = string.IsNullOrWhiteSpace(search) ? null : search;
            rest = TakeScheme(rest, out _scheme);
            _path = GetValidatePath(rest, _scheme);
        }

        public QueryPath(QueryScheme scheme, string? path, string? search)
        {
            _search = string.IsNullOrWhiteSpace(search) ? null : search;
            _scheme = scheme;
            _path = GetValidatePath(path, _scheme);
        }

        public QueryPath(QueryScheme scheme, string? path)
        {
            _search = null;
            _scheme = scheme;
            _path = GetValidatePath(path, _scheme);
        }

        public QueryPath(QueryScheme scheme)
        {
            _search = null;
            _scheme = scheme;
            _path = null;
        }

        private QueryScheme _scheme;
        public QueryScheme Scheme
        {
            get { return _scheme; }
            private set { _scheme = value; }
        }

        private string? _path;
        public string? Path
        {
            get { return _path; }
            private set { _path = value; }
        }

        private string? _search;
        public string? Search
        {
            get { return _search; }
            private set { _search = value; }
        }

        public bool IsEmpty => _path is null;


        /// <summary>
        /// 完全クエリ
        /// </summary>
        public string FullQuery => FullPath + (_search != null ? _querySearch + _search : null);

        /// <summary>
        /// 簡略化したクエリ
        /// </summary>
        public string SimpleQuery => SimplePath + (_search != null ? _querySearch + _search : null);


        /// <summary>
        /// 完全パス
        /// </summary>
        public string FullPath => LoosePath.Combine(_scheme.ToSchemeString(), _path);

        /// <summary>
        /// 簡略化したパス
        /// </summary>
        public string SimplePath => _scheme == QueryScheme.File ? _path ?? "" : FullPath;


        public string FileName => LoosePath.GetFileName(_path);

        public string DispName => (_path == null) ? _scheme.ToAliasName() : FileName;

        public string DispPath => (_path == null) ? _scheme.ToAliasName() : SimplePath;


        private static string? TakeQuerySearch(string? source, out string? searchWord)
        {
            if (source != null)
            {
                var index = source.IndexOf(_querySearch, StringComparison.Ordinal);
                if (index >= 0)
                {
                    searchWord = source[(index + _querySearch.Length)..];
                    return source[..index];
                }
            }

            searchWord = null;
            return source;
        }

        private static string? TakeScheme(string? source, out QueryScheme scheme)
        {
            if (source != null)
            {
                scheme = QuerySchemeExtensions.GetScheme(source);
                var schemeString = scheme.ToSchemeString();
                if (source.StartsWith(schemeString, StringComparison.Ordinal))
                {
                    var length = schemeString.Length;
                    if (length < source.Length && (source[length] == '\\' || source[length] == '/'))
                    {
                        length++;
                    }
                    return source[length..];
                }
            }
            else
            {
                scheme = QueryScheme.File;
            }

            return source;
        }

        private static string? GetValidatePath(string? source, QueryScheme scheme)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return null;
            }

            var s = LoosePath.NormalizeSeparator(source).Trim(LoosePath.AsciiSpaces).Trim('\\');

            if (scheme == QueryScheme.File)
            {
                // is drive
                if (s.Length == 2 && s[1] == ':')
                {
                    return char.ToUpperInvariant(s[0]) + ":\\";
                }
            }

            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        public QueryPath ReplacePath(string? path)
        {
            var query = (QueryPath)this.MemberwiseClone();
            query.Path = string.IsNullOrWhiteSpace(path) ? null : path;
            return query;
        }

        public QueryPath ReplaceSearch(string? search)
        {
            var query = (QueryPath)this.MemberwiseClone();
            query.Search = string.IsNullOrWhiteSpace(search) ? null : search;
            return query;
        }

        public QueryPath GetParent()
        {
            if (_path == null)
            {
                //return null;
                return QueryPath.Empty;
            }

            var parent = LoosePath.GetDirectoryName(_path);
            return new QueryPath(this.Scheme, parent, null);
        }

        public bool Include(QueryPath target)
        {
            var pathX = this.FullPath;
            var pathY = target.FullPath;

            var lengthX = pathX.Length;
            var lengthY = pathY.Length;

            if (lengthX > lengthY)
            {
                return false;
            }
            else if (lengthX == lengthY)
            {
                return pathX == pathY;
            }
            else
            {
                return pathY.StartsWith(pathX, StringComparison.Ordinal) && pathY[lengthX] == '\\';
            }
        }

        public bool IsRoot(QueryScheme scheme)
        {
            return Scheme == scheme && Path == null && Search == null;
        }

        public override string ToString()
        {
            return FullQuery;
        }

        #region IEquatable Support

        public override int GetHashCode()
        {
            return _scheme.GetHashCode() ^ (_path == null ? 0 : _path.GetHashCode(StringComparison.Ordinal)) ^ (_search == null ? 0 : _search.GetHashCode(StringComparison.Ordinal));
        }

        public bool Equals(QueryPath? obj)
        {
            if (obj is null)
            {
                return false;
            }

            return _scheme == obj._scheme && _path == obj._path && _search == obj._search;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null || this.GetType() != obj.GetType())
            {
                return false;
            }

            return this.Equals((QueryPath?)obj);
        }


        public static bool Equals(QueryPath? a, QueryPath? b)
        {
            if ((object?)a == (object?)b)
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return a.Equals(b);
        }

        // HACK: 等号の再定義はあまりよろしくない。
        public static bool operator ==(QueryPath? x, QueryPath? y)
        {
            return Equals(x, y);
        }

        public static bool operator !=(QueryPath? x, QueryPath? y)
        {
            return !(Equals(x, y));
        }

        #endregion IEquatable Support
    }


    public static class QueryPathExtensions
    {
        /// <summary>
        /// 実体のパスに変換する
        /// </summary>
        /// <remarks>
        /// ショートカットならば実体のパスに変換する。
        /// スクリプトスキームならばファイルパスに変換する。
        /// 他のスキームは非対応
        /// </remarks>
        public static QueryPath ToEntityPath(this QueryPath source)
        {
            if (source.Path is null) return source;

            if (source.Scheme == QueryScheme.File)
            {
                if (FileShortcut.IsShortcut(source.SimplePath))
                {
                    var shortcut = new FileShortcut(source.SimplePath);
                    if (shortcut.IsValid)
                    {
                        return new QueryPath(shortcut.TargetPath);
                    }
                }
            }
            else if (source.Scheme == QueryScheme.Script)
            {
                return ToEntityPath(new QueryPath(QueryScheme.File, Path.Combine(Config.Current.Script.ScriptFolder, source.Path)));
            }

            return source;
        }
    }
}
