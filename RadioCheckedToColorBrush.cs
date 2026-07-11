using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SnakeScreensaver;

public class RadioCheckedToColorBrush : IMultiValueConverter
{
    private static readonly SolidColorBrush CheckedFill = new SolidColorBrush(Color.FromRgb(0x3C, 0xE3, 0x7C));
    private static readonly SolidColorBrush UncheckedFill = new SolidColorBrush(Color.FromArgb(0, 0xFF, 0xFF, 0xFF));

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 1 && values[0] is bool b && b) return CheckedFill;
        return UncheckedFill;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
