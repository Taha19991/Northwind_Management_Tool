namespace Northwind.Core.Models;

public sealed class Shipper
{
    public short ShipperId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}
