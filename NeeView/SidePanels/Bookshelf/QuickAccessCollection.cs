﻿using NeeLaboratory.ComponentModel;
using NeeLaboratory.Linq;
using NeeView.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NeeView
{
    public class QuickAccessCollection : TreeCollection<QuickAccessEntry>
    {
        static QuickAccessCollection() => Current = new QuickAccessCollection();
        public static QuickAccessCollection Current { get; }


        public QuickAccessCollection() : base(new TreeListNode<QuickAccessEntry>(new QuickAccessRoot()))
        {
            Root.IsExpanded = true;
        }


        public TreeListNode<QuickAccessEntry>? FindNode(QueryPath query)
        {
            if (query.Scheme != QueryScheme.QuickAccess)
            {
                throw new ArgumentException("Not a QuickAccess scheme path.", nameof(query));
            }

            if (query.Path == null)
            {
                return Root;
            }

            return FindNode(Root, query.Path);
        }

        // TODO: TreeCollection に移動
        private TreeListNode<QuickAccessEntry>? FindNode(TreeListNode<QuickAccessEntry> node, string path)
        {
            return FindNode(node, path.Split(LoosePath.Separators));
        }

        private TreeListNode<QuickAccessEntry>? FindNode(TreeListNode<QuickAccessEntry> node, IEnumerable<string> pathTokens)
        {
            if (pathTokens == null)
            {
                return null;
            }

            if (!pathTokens.Any())
            {
                return node;
            }

            var name = pathTokens.First();
            var child = node.FirstOrDefault(e => e.Value.Name == name);
            if (child != null)
            {
                return FindNode(child, pathTokens.Skip(1));
            }

            return null;
        }


        public TreeListNode<QuickAccessEntry>? AddNewFolder(TreeListNode<QuickAccessEntry> parent, string? name)
        {
            return InsertNewFolder(parent, parent.Count, name);
        }

        public TreeListNode<QuickAccessEntry>? InsertNewFolder(TreeListNode<QuickAccessEntry> parent, int index, string? name)
        {
            if (parent.Value is not QuickAccessFolder) return null;

            var ignoreNames = parent.Where(e => e.Value is QuickAccessEntry).Select(e => e.Value.Name).WhereNotNull();
            var validName = GetValidateFolderName(ignoreNames, name, Properties.TextResources.GetString("Word.NewFolder"));
            var node = new TreeListNode<QuickAccessEntry>(new QuickAccessFolder() { Name = validName });

            parent.Insert(index, node);
            parent.IsExpanded = true;
            return node;
        }

        private static string GetValidateFolderName(IEnumerable<string> names, string? name, string defaultName)
        {
            name = BookmarkTools.GetValidateName(name);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = defaultName;
            }
            if (names.Contains(name))
            {
                int count = 1;
                string newName;
                do
                {
                    newName = $"{name} ({++count})";
                }
                while (names.Contains(newName));
                name = newName;
            }

            return name;
        }


        #region Memento
        [Memento]
        public class Memento
        {
            public List<QuickAccessTreeNode>? Items { get; set; }
        }

        public Memento CreateMemento()
        {
            var memento = new Memento();
            memento.Items = QuickAccessTreeNodeConverter.ConvertFrom(Root).Children;
            return memento;
        }

        public void Restore(Memento? memento)
        {
            if (memento == null) return;
            if (memento.Items is null) return;

            var items = memento.Items.Select(e => QuickAccessTreeNodeConverter.ConvertToTreeListNode(e)).WhereNotNull().ToList();
            Root.Reset(items);
        }

        #endregion

    }



    public class QuickAccessTreeNode
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Path { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<QuickAccessTreeNode>? Children { get; set; }

        public bool IsFolder => Children != null;
    }


    public static class QuickAccessTreeNodeConverter
    {
        public static QuickAccessTreeNode ConvertFrom(TreeListNode<QuickAccessEntry> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var node = new QuickAccessTreeNode();

            if (source.Value is QuickAccessFolder folder)
            {
                node.Name = folder.Name;
                node.Children = new List<QuickAccessTreeNode>();
                foreach (var child in source)
                {
                    node.Children.Add(ConvertFrom(child));
                }
            }
            else if (source.Value is QuickAccess quickAccess)
            {
                node.Name = quickAccess.RawName;
                node.Path = quickAccess.Path;
            }
            else
            {
                throw new NotSupportedException();
            }

            return node;
        }

        public static TreeListNode<QuickAccessEntry>? ConvertToTreeListNode(QuickAccessTreeNode source)
        {
            if (source.IsFolder)
            {
                var folder = new QuickAccessFolder()
                {
                    Name = source.Name
                };

                var node = new TreeListNode<QuickAccessEntry>(folder);
                if (source.Children is not null)
                {
                    foreach (var child in source.Children)
                    {
                        var childNode = ConvertToTreeListNode(child);
                        if (childNode is not null)
                        {
                            node.Add(childNode);
                        }
                    }
                }
                return node;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(source.Path))
                {
                    return null;
                }
                var quickAccess = new QuickAccess(source.Path)
                {
                    Name = source.Name ?? "",
                };
                var node = new TreeListNode<QuickAccessEntry>(quickAccess);
                return node;
            }
        }
    }
}
