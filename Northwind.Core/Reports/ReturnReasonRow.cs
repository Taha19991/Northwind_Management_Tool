using System.ComponentModel.DataAnnotations;
using Northwind.Core.Enums;

namespace Northwind.Core.Reports;

public sealed class ReturnReasonRow
{
    [Display(Name = "Return Reason")]
    public ReturnReason Reason { get; set; }

    [Display(Name = "Returns")]
    public int Returns { get; set; }

    [Display(Name = "Total Units")]
    public int Units { get; set; }

    [Display(Name = "Refund Amount ($)")]
    [DataType(DataType.Currency)]
    public decimal Refund { get; set; }
}
