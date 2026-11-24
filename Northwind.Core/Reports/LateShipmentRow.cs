using System.ComponentModel.DataAnnotations;

namespace Northwind.Core.Reports;

public sealed class LateShipmentRow
{
    [Display(Name = "Shipper")]
    public string Shipper { get; set; } = string.Empty;

    [Display(Name = "Shipped Orders")]
    public int Shipped { get; set; }

    [Display(Name = "Late Orders")]
    public int Late { get; set; }

    [Display(Name = "Late %")]
    public decimal? LatePct { get; set; }
}
