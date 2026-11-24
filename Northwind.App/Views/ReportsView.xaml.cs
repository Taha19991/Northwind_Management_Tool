using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Northwind.Core.Enums;

namespace Northwind.App.Views;

public partial class ReportsView : UserControl
{
    private static readonly EnumDisplayValueConverter EnumConverter = new();

    private static readonly Style RightAlignedTextStyle = CreateRightAlignedStyle();

    public ReportsView()
    {
        InitializeComponent();
    }

    private void OnAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var descriptor = e.PropertyDescriptor as PropertyDescriptor;
        var displayAttr = descriptor?.Attributes[typeof(DisplayAttribute)] as DisplayAttribute;
        if (!string.IsNullOrWhiteSpace(displayAttr?.Name))
        {
            e.Column.Header = displayAttr.Name;
        }

        if (e.Column is not DataGridTextColumn textColumn)
        {
            return;
        }

        var dataTypeAttr = descriptor?.Attributes[typeof(DataTypeAttribute)] as DataTypeAttribute;
        var propType = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;

        if (textColumn.Binding is Binding binding)
        {
            if (propType == typeof(decimal))
            {
                var isCurrency = dataTypeAttr?.DataType == DataType.Currency;
                binding.StringFormat = isCurrency ? "{0:N2}" : "{0:N1}";
                binding.TargetNullValue = "-";
                textColumn.ElementStyle = RightAlignedTextStyle;
                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }
            else if (propType == typeof(int) || propType == typeof(short) || propType == typeof(long))
            {
                binding.StringFormat = "{0:N0}";
                binding.TargetNullValue = "-";
                textColumn.ElementStyle = RightAlignedTextStyle;
                e.Column.Width = new DataGridLength(0.85, DataGridLengthUnitType.Star);
            }
            else if (propType == typeof(DateOnly))
            {
                binding.StringFormat = string.Equals(e.PropertyName, "Month", StringComparison.Ordinal)
                    ? "{0:MMM yyyy}"
                    : AppFormats.IsoDateBinding;
                binding.TargetNullValue = "-";
                e.Column.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            }
            else if (propType.IsEnum)
            {
                binding.Converter = EnumConverter;
                e.Column.Width = new DataGridLength(1.2, DataGridLengthUnitType.Star);
            }
            else
            {
                binding.TargetNullValue = "-";
                e.Column.Width = new DataGridLength(1.4, DataGridLengthUnitType.Star);
            }
        }
    }

    private static Style CreateRightAlignedStyle()
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        return style;
    }

    private sealed class EnumDisplayValueConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
        {
            TicketCategory tc => EnumText.ToDisplay(tc),
            TicketStatus ts => EnumText.ToDisplay(ts),
            TicketPriority tp => EnumText.ToDisplay(tp),
            ReturnReason rr => EnumText.ToDisplay(rr),
            ReturnStatus rs => EnumText.ToDisplay(rs),
            OrderStatus os => EnumText.ToDisplay(os),
            UserRole ur => EnumText.ToDisplay(ur),
            _ => value?.ToString() ?? "-"
        };

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
