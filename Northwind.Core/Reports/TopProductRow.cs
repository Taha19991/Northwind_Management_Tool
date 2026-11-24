using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class TopProductRow
{
    [Display(Name = "Product Name")]
    public string ProductName { get; set; } = string.Empty;

    [Display(Name = "Net Revenue ($)")]
    [DataType(DataType.Currency)]
    public decimal NetRevenue { get; set; }
}
