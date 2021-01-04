﻿using System;
using System.Globalization;

namespace NeeView.Windows.Controls
{
    public class FormatValueConverter<T> : SafeValueConverter<T>
    {
        public string Format { get; set; }

        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Format is null ? value : string.Format(Format, value);
        }
    }
}
