using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Northwind.App.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is string text
            ? !string.IsNullOrWhiteSpace(text)
            : value is not null;

        var invert = parameter is string p &&
                     string.Equals(p, "Invert", StringComparison.OrdinalIgnoreCase);

        if (invert)
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
