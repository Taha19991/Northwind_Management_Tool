using Northwind.Core.Enums;
using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface IReturnRepository
{
    Task<IReadOnlyList<ProductReturn>> ListAsync(ReturnStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProductReturn>> ListByTicketAsync(int ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<ReturnableOrderLine>> GetReturnableLinesAsync(int orderId, CancellationToken ct = default);
    Task<int> CreateAsync(ProductReturn r, CancellationToken ct = default);
    Task ApproveAsync(int id, CancellationToken ct = default);
    Task RejectAsync(int id, CancellationToken ct = default);
    Task MarkRefundedAsync(int id, CancellationToken ct = default);
    Task RestockAsync(int id, CancellationToken ct = default);
}
