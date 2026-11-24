using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class MonthlyRevenueRow
{
    [Display(Name = "Month")]
    public DateOnly Month { get; set; }

    [Display(Name = "Net Revenue ($)")]
    [DataType(DataType.Currency)]
    public decimal NetRevenue { get; set; }
}
