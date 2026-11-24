using Northwind.Core.Enums;

namespace Northwind.Core.Filters;

public sealed class OrderFilter
{
    public string? CustomerId { get; set; }
    public short? EmployeeId { get; set; }
    public short? ShipVia { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public OrderStatus? Status { get; set; }
    public string? SearchText { get; set; }
}
