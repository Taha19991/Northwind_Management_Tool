using System.ComponentModel.DataAnnotations;
using Northwind.Core.Enums;

namespace Northwind.Core.Reports;

public sealed class TicketStatusRow
{
    [Display(Name = "Category")]
    public TicketCategory Category { get; set; }

    [Display(Name = "Status")]
    public TicketStatus Status { get; set; }

    [Display(Name = "Tickets")]
    public int Tickets { get; set; }
}
