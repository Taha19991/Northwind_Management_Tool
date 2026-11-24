using Northwind.Core.Enums;

namespace Northwind.Core.Models;

public sealed class Order
{
    public int OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public short EmployeeId { get; set; }
    public DateOnly OrderDate { get; set; }
    public DateOnly? RequiredDate { get; set; }
    public DateOnly? ShippedDate { get; set; }
    public short ShipVia { get; set; }
    public decimal? Freight { get; set; }
    public string? ShipName { get; set; }
    public string? ShipAddress { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipRegion { get; set; }
    public string? ShipPostalCode { get; set; }
    public string? ShipCountry { get; set; }

    public List<OrderLine> Lines { get; set; } = [];

    // Optional JOIN / summary display fields
    public string? CustomerName { get; set; }
    public string? EmployeeName { get; set; }
    public string? ShipperName { get; set; }
    public decimal NetTotal { get; set; }

    public bool IsLate => ShippedDate > RequiredDate;

    public int? DelayDays => ShippedDate.HasValue && RequiredDate.HasValue && ShippedDate.Value > RequiredDate.Value
        ? ShippedDate.Value.DayNumber - RequiredDate.Value.DayNumber
        : null;

    public OrderStatus Status => ShippedDate is null
        ? OrderStatus.Pending
        : IsLate
            ? OrderStatus.Late
            : OrderStatus.Shipped;

    public string StatusText => EnumText.ToDisplay(Status);
}
