using Northwind.Core.Filters;
using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> ListAsync(OrderFilter f, CancellationToken ct = default);
    Task<Order?> GetWithLinesAsync(int id, CancellationToken ct = default);
    Task<int> CreateOrderAsync(Order o, IEnumerable<OrderLine> lines, CancellationToken ct = default);
    Task ShipAsync(int orderId, short shipperId, decimal freight, DateOnly shippedDate, CancellationToken ct = default);
}
