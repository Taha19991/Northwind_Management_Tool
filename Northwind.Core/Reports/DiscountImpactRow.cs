using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class DiscountImpactRow
{
    [Display(Name = "Line Type")]
    public string LineType { get; set; } = string.Empty;

    [Display(Name = "Order Lines")]
    public int Lines { get; set; }

    [Display(Name = "Gross Amount ($)")]
    [DataType(DataType.Currency)]
    public decimal Gross { get; set; }

    [Display(Name = "Discount Given ($)")]
    [DataType(DataType.Currency)]
    public decimal Discount { get; set; }

    [Display(Name = "Net Revenue ($)")]
    [DataType(DataType.Currency)]
    public decimal Net { get; set; }
}
