using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface ILookupRepository
{
    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Supplier>> GetSuppliersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Shipper>> GetShippersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> GetEmployeesAsync(CancellationToken ct = default);
}
