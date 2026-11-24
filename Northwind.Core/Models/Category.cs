namespace Northwind.Core.Models;

public sealed class Category
{
    public short CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public byte[]? Picture { get; set; }
}
