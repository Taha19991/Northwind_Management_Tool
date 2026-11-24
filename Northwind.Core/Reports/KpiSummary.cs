using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class KpiSummary
{
    [Display(Name = "Total Net Revenue ($)")]
    [DataType(DataType.Currency)]
    public decimal TotalNetRevenue { get; set; }

    [Display(Name = "Total Orders")]
    public int TotalOrders { get; set; }

    [Display(Name = "Open Orders")]
    public int OpenOrders { get; set; }

    [Display(Name = "Open Tickets")]
    public int OpenTickets { get; set; }

    [Display(Name = "Low-Stock Products")]
    public int LowStockProducts { get; set; }
}
