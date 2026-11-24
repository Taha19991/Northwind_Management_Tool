using Northwind.Core.Enums;

namespace Northwind.Core.Models;

public sealed class SupportTicket
{
    public int TicketId { get; set; }
    public int? OrderId { get; set; }
    public string? CustomerId { get; set; }
    public short? AssignedEmployeeId { get; set; }
    public TicketCategory Category { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // v_ticket_overview display fields
    public string? CompanyName { get; set; }
    public string? AssignedTo { get; set; }

    public string CategoryText => EnumText.ToDisplay(Category);
    public string PriorityText => EnumText.ToDisplay(Priority);
    public string StatusText => EnumText.ToDisplay(Status);

    public decimal? ResolutionHours => ResolvedAt.HasValue
        ? Math.Round((decimal)(ResolvedAt.Value - CreatedAt).Ticks / TimeSpan.TicksPerHour, 1)
        : null;
}
