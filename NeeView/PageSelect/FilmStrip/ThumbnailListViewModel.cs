﻿using NeeLaboratory.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Data;

namespace NeeView
{
    /// <summary>
    /// ThumbnailList : ViewModel
    /// </summary>
    public class ThumbnailListViewModel : BindableBase
    {
        private ThumbnailList _model;


        public ThumbnailListViewModel(ThumbnailList model)
        {
            _model = model ?? throw new InvalidOperationException();

            _model.CollectionChanging +=
                (s, e) => CollectionChanging?.Invoke(s, e);

            _model.CollectionChanged +=
                (s, e) => CollectionChanged?.Invoke(s, e);

            _model.ViewItemsChanged +=
                (s, e) => AppDispatcher.Invoke(() => ViewItemsChanged?.Invoke(s, e));
        }


        public event EventHandler? CollectionChanging;
        public event EventHandler? CollectionChanged;
        public event EventHandler<ViewItemsChangedEventArgs>? ViewItemsChanged;


        public ThumbnailList Model
        {
            get { return _model; }
            set { if (_model != value) { _model = value; RaisePropertyChanged(); } }
        }


        public void MoveSelectedIndex(int delta)
        {
            _model.MoveSelectedIndex(delta);
        }

        public void RequestThumbnail(int start, int count, int margin, int direction)
        {
            _model.RequestThumbnail(start, count, margin, direction);
        }

        public void CancelThumbnailRequest()
        {
            _model.CancelThumbnailRequest();
        }

        internal void FlushSelectedIndex()
        {
            _model.FlushSelectedIndex();
        }
    }
}
