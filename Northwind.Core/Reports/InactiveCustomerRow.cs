using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class InactiveCustomerRow
{
    [Display(Name = "Customer ID")]
    public string CustomerId { get; set; } = string.Empty;

    [Display(Name = "Company Name")]
    public string CompanyName { get; set; } = string.Empty;

    [Display(Name = "Country")]
    public string? Country { get; set; }

    [Display(Name = "Last Order Date")]
    public DateOnly? LastOrder { get; set; }
}
