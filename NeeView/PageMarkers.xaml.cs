﻿// Copyright (c) 2016 Mitsuhiro Ito (nee)
//
// This software is released under the MIT License.
// http://opensource.org/licenses/mit-license.php

using NeeView.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NeeView
{
    /// <summary>
    /// PageMarkers.xaml の相互作用ロジック
    /// </summary>
    public partial class PageMarkers : UserControl
    {
        private PageMarkersVM _VM;

        public PageMarkers()
        {
            InitializeComponent();

            _VM = new PageMarkersVM(this.RootCanvas);
            this.DataContext = _VM;
        }

        public void Initialize(BookHub bookHub)
        {
            _VM.BookHub = bookHub;
        }

        #region Property: IsSliderDirectionReversed
        public bool IsSliderDirectionReversed
        {
            get { return _VM.IsSliderDirectionReversed; }
            set { _VM.IsSliderDirectionReversed = value; }
        }
        #endregion
    }

    /// <summary>
    /// ページマーク単体の表示情報
    /// </summary>
    public class PageMarker
    {
        public FrameworkElement Control { get; set; }

        private Book _book;
        public Page Page { get; set; }

        private double _position;

        public PageMarker(Book book, Page page)
        {
            _book = book;
            Page = page;

            var path = new Path();
            path.Data = Geometry.Parse("M0,0 L0,10 5,9 10,10 10,0 z");
            path.Fill = App.Current.Resources["NVStarMarkBrush"] as Brush;
            //path.Stroke = Brushes.DarkOrange;
            //path.StrokeThickness = 1.0;
            path.Width = 10;
            path.Height = 18;
            path.Stretch = Stretch.Fill;
            this.Control = path;
        }

        /// <summary>
        /// 座標更新
        /// </summary>
        public void Update()
        {
            int max = _book.Pages.Count - 1;
            if (max < 1) max = 1;
            _position = (double)Page.Index / (double)max;
        }

        /// <summary>
        /// 表示更新
        /// </summary>
        /// <param name="width"></param>
        /// <param name="isReverse"></param>
        public void UpdateControl(double width, bool isReverse)
        {
            const double tumbWidth = 12;

            var x = (width - tumbWidth) * (isReverse ? 1.0 - _position : _position) + (tumbWidth - Control.Width) * 0.5;
            Canvas.SetLeft(Control, x);
        }
    }

    /// <summary>
    /// PageMakers ViewModel
    /// </summary>
    public class PageMarkersVM : BindableBase
    {
        private Canvas _canvas;

        #region Property: BookHub
        private BookHub _bookHub;
        public BookHub BookHub
        {
            get { return _bookHub; }
            set
            {
                if (_bookHub != value)
                {
                    _bookHub = value;
                    BookHubChanged();
                }
            }
        }
        #endregion

        #region Property: IsSliderDirectionReversed
        private bool _isSliderDirectionReversed;
        public bool IsSliderDirectionReversed
        {
            get { return _isSliderDirectionReversed; }
            set { _isSliderDirectionReversed = value; UpdateControl(); }
        }
        #endregion


        //
        private List<PageMarker> _markers;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="canvas"></param>
        public PageMarkersVM(Canvas canvas)
        {
            _markers = new List<PageMarker>();

            _canvas = canvas;
            _canvas.SizeChanged += Canvas_SizeChanged;
        }

        /// <summary>
        /// サイズ変更されたらマーカー表示座標を更新する
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                UpdateControl();
            }
        }

        /// <summary>
        /// BookHub Changed
        /// </summary>
        private void BookHubChanged()
        {
            // TODO: これはModel化されるまでの仮処理です
            var bookOperation = BookOperation.Current;

            bookOperation.BookChanged += (s, e) => BookChanged();
            ////_bookHub.PagemarkChanged += (s, e) => UpdateInvoke();
            bookOperation.PagesSorted += (s, e) => UpdateInvoke();
            bookOperation.PageRemoved += (s, e) => UpdateInvoke();

            BookOperation.Current.PagemarkChanged +=
                (s, e) => UpdateInvoke();
        }

        /// <summary>
        /// マーカー表示更新 (表示スレッド)
        /// </summary>
        private void UpdateInvoke()
        {
            App.Current?.Dispatcher.Invoke(() => Update());
        }

        /// <summary>
        /// 本が変更された場合、全てを更新
        /// </summary>
        private void BookChanged()
        {
            // clear
            _canvas.Children.Clear();
            _markers.Clear();

            if (_bookHub.Book == null) return;

            // update first
            Update();
        }

        /// <summary>
        /// マーカー更新
        /// </summary>
        private void Update()
        {
            if (_bookHub.Book == null)
            {
                _canvas.Children.Clear();
                _markers.Clear();
                return;
            }

            // remove markers
            foreach (var marker in _markers.Where(e => !_bookHub.Book.Markers.Contains(e.Page)).ToList())
            {
                _canvas.Children.Remove(marker.Control);
                _markers.Remove(marker);
            }

            // add markers
            foreach (var key in _bookHub.Book.Markers.Where(e => _markers.All(m => m.Page != e)).ToList())
            {
                var marker = new PageMarker(_bookHub.Book, key);
                _canvas.Children.Add(marker.Control);
                _markers.Add(marker);
            }

            // update
            _markers.ForEach(e => e.Update());

            // update control
            UpdateControl();
        }

        /// <summary>
        /// マーカー更新(表示)
        /// </summary>
        private void UpdateControl()
        {
            _markers.ForEach(e => e.UpdateControl(_canvas.ActualWidth, IsSliderDirectionReversed));
        }
    }
}
