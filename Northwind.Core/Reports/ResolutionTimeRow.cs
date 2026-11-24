using System.ComponentModel.DataAnnotations;
using Northwind.Core.Enums;

namespace Northwind.Core.Reports;

public sealed class ResolutionTimeRow
{
    [Display(Name = "Category")]
    public TicketCategory Category { get; set; }

    [Display(Name = "Avg. Resolution (Hours)")]
    public decimal AvgHours { get; set; }
}
