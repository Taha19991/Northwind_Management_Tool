namespace Northwind.Core.Models;

public sealed class OrderLine
{
    public int OrderId { get; set; }
    public short ProductId { get; set; }
    public decimal UnitPrice { get; set; }
    public short Quantity { get; set; }
    public decimal Discount { get; set; }

    // Optional JOIN display field
    public string? ProductName { get; set; }

    public decimal GrossAmount => UnitPrice * Quantity;

    public decimal DiscountAmount => UnitPrice * Quantity * Discount;

    public decimal NetAmount => UnitPrice * Quantity * (1m - Discount);
}
