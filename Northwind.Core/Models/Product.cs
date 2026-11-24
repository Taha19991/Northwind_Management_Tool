namespace Northwind.Core.Models;

public sealed class Product
{
    public short ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public short SupplierId { get; set; }
    public short CategoryId { get; set; }
    public string? QuantityPerUnit { get; set; }
    public decimal? UnitPrice { get; set; }
    public short? UnitsInStock { get; set; }
    public short? UnitsOnOrder { get; set; }
    public short? ReorderLevel { get; set; }
    public bool Discontinued { get; set; }

    // Optional JOIN display fields for listing (not persisted)
    public string? CategoryName { get; set; }
    public string? SupplierName { get; set; }

    public bool IsLowStock => UnitsInStock <= ReorderLevel && !Discontinued;
}
