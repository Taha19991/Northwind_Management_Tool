using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class ReorderRow
{
    [Display(Name = "Product ID")]
    public short ProductId { get; set; }

    [Display(Name = "Product Name")]
    public string ProductName { get; set; } = string.Empty;

    [Display(Name = "In Stock")]
    public short UnitsInStock { get; set; }

    [Display(Name = "On Order")]
    public short? UnitsOnOrder { get; set; }

    [Display(Name = "Reorder Level")]
    public short? ReorderLevel { get; set; }
}
