using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Northwind.Core.Enums;

namespace Northwind.App.Converters;

public sealed class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenBrush = CreateFrozenBrush(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush AmberBrush = CreateFrozenBrush(0xED, 0x6C, 0x02);
    private static readonly SolidColorBrush RedBrush = CreateFrozenBrush(0xC6, 0x28, 0x28);
    private static readonly SolidColorBrush BlueBrush = CreateFrozenBrush(0x15, 0x65, 0xC0);
    private static readonly SolidColorBrush GrayBrush = CreateFrozenBrush(0x60, 0x7D, 0x8B);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            OrderStatus.Shipped => GreenBrush,
            OrderStatus.Pending => AmberBrush,
            OrderStatus.Late => RedBrush,

            TicketStatus.Open => RedBrush,
            TicketStatus.InProgress => AmberBrush,
            TicketStatus.Resolved => GreenBrush,
            TicketStatus.Closed => GrayBrush,

            TicketPriority.Low => GreenBrush,
            TicketPriority.Medium => AmberBrush,
            TicketPriority.High => RedBrush,

            ReturnStatus.Requested => AmberBrush,
            ReturnStatus.Approved => GreenBrush,
            ReturnStatus.Rejected => RedBrush,
            ReturnStatus.Refunded => BlueBrush,

            string text => ConvertText(text),
            _ => GrayBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;

    private static SolidColorBrush ConvertText(string text) => text.Trim() switch
    {
        "Shipped" or "Resolved" or "Approved" or "Low" => GreenBrush,
        "Pending" or "In progress" or "Requested" or "Medium" => AmberBrush,
        "Late" or "Open" or "Rejected" or "High" => RedBrush,
        "Refunded" => BlueBrush,
        _ => GrayBrush
    };

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
