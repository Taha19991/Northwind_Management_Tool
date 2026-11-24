using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Northwind.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        var invert = parameter is string p &&
                     string.Equals(p, "Invert", StringComparison.OrdinalIgnoreCase);

        if (invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is Visibility v && v == Visibility.Visible;
        var invert = parameter is string p &&
                     string.Equals(p, "Invert", StringComparison.OrdinalIgnoreCase);
        return invert ? !visible : visible;
    }
}
