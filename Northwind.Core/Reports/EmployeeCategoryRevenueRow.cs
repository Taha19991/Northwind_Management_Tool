using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class EmployeeCategoryRevenueRow
{
    [Display(Name = "Employee")]
    public string Employee { get; set; } = string.Empty;

    [Display(Name = "Category")]
    public string CategoryName { get; set; } = string.Empty;

    [Display(Name = "Net Revenue ($)")]
    [DataType(DataType.Currency)]
    public decimal NetRevenue { get; set; }
}
