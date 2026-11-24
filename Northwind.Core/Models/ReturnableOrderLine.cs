namespace Northwind.Core.Models;

public sealed class ReturnableOrderLine
{
    public int OrderId { get; set; }
    public short ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public short OrderedQuantity { get; set; }
    public int AlreadyReturnedQuantity { get; set; }

    public int RemainingQuantity => Math.Max(0, OrderedQuantity - AlreadyReturnedQuantity);

    public string DisplayLabel =>
        $"{ProductName} — Ordered: {OrderedQuantity}, Returned: {AlreadyReturnedQuantity}, Remaining: {RemainingQuantity}";
}
