﻿using NeeLaboratory.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NeeView
{
    public class QuickAccessNode : FolderTreeNodeBase
    {
        public static event EventHandler? NameChanged;

        public QuickAccessNode(QuickAccess source, RootQuickAccessNode? parent)
        {
            Source = source;
            Parent = parent;
        }

        public QuickAccess QuickAccessSource => (QuickAccess?)Source ?? throw new InvalidOperationException();

        public override string Name { get => QuickAccessSource.Name; set { } }

        public override string DispName { get => Name; set { } }

        public override IImageSourceCollection Icon => PathToPlaceIconConverter.Convert(new QueryPath(QuickAccessSource.Path));

        public override string GetRenameText()
        {
            return this.Name;
        }

        public override bool CanRename()
        {
            return true;
        }

        public override async Task<bool> RenameAsync(string name)
        {
            if (this.Name == name) return false;

            QuickAccessSource.Name = name;
            RaisePropertyChanged(nameof(Name));
            RaisePropertyChanged(nameof(DispName));
            NameChanged?.Invoke(this, EventArgs.Empty);
            return await Task.FromResult(true);
        }
    }
}
