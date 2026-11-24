namespace Northwind.Core.Models;

public sealed class Territory
{
    public string TerritoryId { get; set; } = string.Empty;
    public string TerritoryDescription { get; set; } = string.Empty;
    public short RegionId { get; set; }

    // Optional JOIN display field
    public string? RegionDescription { get; set; }
}
