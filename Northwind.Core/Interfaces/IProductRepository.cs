using Northwind.Core.Models;

namespace Northwind.Core.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(short? categoryId, short? supplierId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetLowStockAsync(CancellationToken ct = default);
    Task<short> InsertAsync(Product p, CancellationToken ct = default);
    Task UpdateAsync(Product p, CancellationToken ct = default);
}
