using Northwind.Core.Enums;

namespace Northwind.Core.Models;

public sealed class ProductReturn
{
    public int ReturnId { get; set; }
    public int OrderId { get; set; }
    public short ProductId { get; set; }
    public int? TicketId { get; set; }
    public short? ProcessedBy { get; set; }
    public DateOnly ReturnDate { get; set; }
    public short Quantity { get; set; }
    public ReturnReason Reason { get; set; }
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    public bool Restocked { get; set; }

    // v_return_overview and joined display fields
    public string? ProductName { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? ProcessedByName { get; set; }

    public string ReasonText => EnumText.ToDisplay(Reason);
    public string StatusText => EnumText.ToDisplay(Status);
    public string RestockedText => Restocked ? "Yes" : "No";
}
