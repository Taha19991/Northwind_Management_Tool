using Northwind.Core.Enums;
using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface ITicketRepository
{
    Task<IReadOnlyList<SupportTicket>> ListAsync(TicketStatus? status, CancellationToken ct = default);
    Task<SupportTicket?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<SupportTicket>> ListByOrderAsync(int orderId, CancellationToken ct = default);
    Task<int> CreateAsync(SupportTicket t, CancellationToken ct = default);
    Task AssignAsync(int id, short? employeeId, CancellationToken ct = default);
    Task ChangeStatusAsync(int id, TicketStatus status, CancellationToken ct = default);
}
