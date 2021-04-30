﻿using NeeLaboratory.ComponentModel;
using NeeView.Collections;
using System;

namespace NeeView
{
    public class PlaylistListBoxItem : BindableBase, IHasPage, IHasName
    {
        private PlaylistItem _item;
        private string _place;
        private Page _archivePage;


        public PlaylistListBoxItem(string path)
        {
            _item = new PlaylistItem(path);
        }

        public PlaylistListBoxItem(PlaylistItem item)
        {
            _item = new PlaylistItem(item.Path, item.Name);
        }


        public string Path
        {
            get { return _item.Path; }
            private set
            {
                if (_item.Path != value)
                {
                    _item.Path = value;
                    RaisePropertyChanged(nameof(Name));
                }
            }
        }

        public string Name
        {
            get { return _item.Name; }
            set 
            {
                var oldName = _item.Name;
                _item.Name = value;
                if (_item.Name != oldName)
                {
                    RaisePropertyChanged();
                }
            }
        }

        public string Place
        {
            get
            {
                if (_place is null)
                {
                    if (FileIO.Exists(Path))
                    {
                        _place = LoosePath.GetDirectoryName(Path);
                    }
                    else
                    {
                        _place = ArchiveEntryUtility.GetExistEntryName(Path);
                    }
                }
                return _place;
            }
        }

        public string DispPlace
        {
            get { return SidePanelProfile.GetDecoratePlaceName(Place); }
        }

        public bool IsArchive
        {
            get { return ArchiverManager.Current.IsSupported(Path) || System.IO.Directory.Exists(Path); }
        }



        public Page ArchivePage
        {
            get
            {
                if (_archivePage == null)
                {
                    _archivePage = new Page("", new ArchiveContent(Path));
                    _archivePage.Thumbnail.IsCacheEnabled = true;
                    _archivePage.Thumbnail.Touched += Thumbnail_Touched;
                }
                return _archivePage;
            }
        }

        private void Thumbnail_Touched(object sender, EventArgs e)
        {
            var thumbnail = (Thumbnail)sender;
            BookThumbnailPool.Current.Add(thumbnail);
        }

        public void UpdateDispPlace()
        {
            RaisePropertyChanged(nameof(DispPlace));
        }

        public Page GetPage()
        {
            return ArchivePage;
        }

        public PlaylistItem ToPlaylistItem()
        {
            return new PlaylistItem(Path, Name);
        }

        public override string ToString()
        {
            return Name ?? base.ToString();
        }
    }
}
