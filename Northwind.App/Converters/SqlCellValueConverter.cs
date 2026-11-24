using System.Globalization;
using System.Windows.Data;

namespace Northwind.App.Converters;

public sealed class SqlCellValueConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || value == DBNull.Value)
        {
            return "NULL";
        }

        return value switch
        {
            DateOnly d => d.ToString(AppFormats.IsoDate, CultureInfo.InvariantCulture),
            DateTime dt => dt.TimeOfDay == TimeSpan.Zero
                ? dt.ToString(AppFormats.IsoDate, CultureInfo.InvariantCulture)
                : dt.ToString(AppFormats.IsoDateTime, CultureInfo.InvariantCulture),
            decimal dec => dec.ToString("G29", CultureInfo.CurrentCulture),
            bool b => b ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class IsDbNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || value == DBNull.Value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
