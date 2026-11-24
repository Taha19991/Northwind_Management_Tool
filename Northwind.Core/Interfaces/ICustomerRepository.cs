using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> SearchAsync(string? text, CancellationToken ct = default);
    Task<Customer?> GetByIdAsync(string id, CancellationToken ct = default);
    Task InsertAsync(Customer c, CancellationToken ct = default);
    Task UpdateAsync(Customer c, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetOrderHistoryAsync(string customerId, CancellationToken ct = default);
}
