﻿using NeeLaboratory.ComponentModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// ArchivePageControl.xaml の相互作用ロジック
    /// </summary>
    public partial class ArchivePageControl : UserControl
    {
        public static readonly RoutedCommand OpenCommand = new("OpenCommand", typeof(ArchivePageControl));

        private readonly ArchivePageViewModel _vm;
        private readonly Stopwatch _doubleTapStopwatch = new();
        private Point _lastTapLocation;


        public ArchivePageControl(ArchiveViewData content)
        {
            InitializeComponent();

            _vm = new ArchivePageViewModel(content);
            this.Root.DataContext = _vm;

            this.FileCard.Icon = content.IconSource;
            this.FileCard.ArchiveEntry = content.Entry;
        }


        public SolidColorBrush DefaultBrush
        {
            get { return (SolidColorBrush)GetValue(DefaultBrushProperty); }
            set { SetValue(DefaultBrushProperty, value); }
        }

        public static readonly DependencyProperty DefaultBrushProperty =
            DependencyProperty.Register("DefaultBrush", typeof(SolidColorBrush), typeof(ArchivePageControl), new PropertyMetadata(Brushes.White, DefaultBrushChanged));

        private static void DefaultBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ArchivePageControl control)
            {
                control.Resources["DefaultBrush"] = e.NewValue;
            }
        }


        private void OpenBookButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //e.Handled = true;
        }

        private void OpenBookButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.StylusDevice == null && e.ClickCount == 2)
            {
                _vm.OpenBook();
            }
            //e.Handled = true;
        }

        private void OpenBookButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            //e.Handled = true;
        }
        private void OpenBookButton_MouseEnter(object sender, MouseEventArgs e)
        {
            MouseInput.IgnoreMouseCommand = true;
        }

        private void OpenBookButton_MouseLeave(object sender, MouseEventArgs e)
        {
            MouseInput.IgnoreMouseCommand = false;
        }


        // from https://stackoverflow.com/questions/27637295/double-click-touch-down-event-in-wpf
        private bool IsDoubleTap(StylusDownEventArgs e)
        {
            var points = e.GetStylusPoints(this);
            if (points.Count != 1)
            {
                return false;
            }

            Point currentTapPosition = points.First().ToPoint();
            bool isTapsAreCloseInDistance = (currentTapPosition - _lastTapLocation).LengthSquared < 40.0 * 40.0;
            _lastTapLocation = currentTapPosition;

            var elapsedMilliseconds = _doubleTapStopwatch.ElapsedMilliseconds;
            _doubleTapStopwatch.Restart();
            bool isTapsAreCloseInTime = (elapsedMilliseconds != 0 && elapsedMilliseconds < System.Windows.Forms.SystemInformation.DoubleClickTime);

            return isTapsAreCloseInDistance && isTapsAreCloseInTime;
        }

        private void OpenBookButton_PreviewStylusDown(object sender, StylusDownEventArgs e)
        {
            if (IsDoubleTap(e))
            {
                _vm.OpenBook();
                e.Handled = true;
            }
        }

        private void OpenBookButton_PreviewStylusUp(object sender, StylusEventArgs e)
        {
            e.Handled = true;
        }

    }


    /// <summary>
    /// ArchivePageControl ViewModel
    /// </summary>
    public class ArchivePageViewModel
    {
        private readonly ArchiveViewData _content;


        public ArchivePageViewModel(ArchiveViewData content)
        {
            _content = content;
        }


        public ImageSource? ImageSource => _content.ImageSource;

        public string? Name => _content.Entry.EntryName?.TrimEnd('\\').Replace("\\", " > ");


        public void OpenBook()
        {
            BookHub.Current.RequestLoad(this, _content.Entry.SystemPath, null, BookLoadOption.IsBook | BookLoadOption.SkipSamePlace, true);
        }
    }


    public class ArchiveThumbnailSizeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var width = (double)values[0];
            var height = (double)values[1];
            return CalcThumbnailSize(width, height);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static double CaptionHeight { get; } = 100.0;

        public static double CalcThumbnailSize(double width, double height)
        {
            var rate = 0.8;
            var size = Math.Max(Math.Min(Math.Min(width * rate, height * rate - CaptionHeight), 512), 128);
            return size;
        }
    }



#if false
    /// <summary>
    /// 入力値に-0.5をかけた値にする。
    /// Canvasのセンタリング計算用
    /// </summary>
    public class DoubleToMinusHalf : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (double)value * -0.5;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
#endif
}
