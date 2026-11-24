using System.Data;
using Dapper;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class ProductRepository(IDbConnectionFactory connectionFactory) : IProductRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Product>> GetAllAsync(
        short? categoryId,
        short? supplierId,
        CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
                   p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
                   p.reorder_level, (p.discontinued = 1) AS discontinued,
                   c.category_name, s.company_name AS supplier_name
            FROM products p
            JOIN categories c ON c.category_id = p.category_id
            JOIN suppliers s ON s.supplier_id = p.supplier_id
            WHERE (CAST(@CategoryId AS smallint) IS NULL OR p.category_id = @CategoryId)
              AND (CAST(@SupplierId AS smallint) IS NULL OR p.supplier_id = @SupplierId)
            ORDER BY p.product_name;
            """;

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("CategoryId", categoryId, DbType.Int16);
            parameters.Add("SupplierId", supplierId, DbType.Int16);

            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Product>(
                new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<IReadOnlyList<Product>> GetLowStockAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.product_id, p.product_name, p.supplier_id, p.category_id,
                   p.quantity_per_unit, p.unit_price, p.units_in_stock, p.units_on_order,
                   p.reorder_level, (p.discontinued = 1) AS discontinued,
                   c.category_name, s.company_name AS supplier_name
            FROM products p
            JOIN categories c ON c.category_id = p.category_id
            JOIN suppliers s ON s.supplier_id = p.supplier_id
            WHERE p.discontinued = 0 AND p.units_in_stock <= p.reorder_level
            ORDER BY p.units_in_stock, p.product_name;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Product>(
                new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<short> InsertAsync(Product p, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO products (
                product_name, supplier_id, category_id, quantity_per_unit,
                unit_price, units_in_stock, units_on_order, reorder_level, discontinued)
            VALUES (
                @ProductName, @SupplierId, @CategoryId, @QuantityPerUnit,
                @UnitPrice, @UnitsInStock, @UnitsOnOrder, @ReorderLevel, @DiscontinuedInt)
            RETURNING product_id;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var parameters = new
            {
                ProductName = p.ProductName.Trim(),
                p.SupplierId,
                p.CategoryId,
                QuantityPerUnit = string.IsNullOrWhiteSpace(p.QuantityPerUnit) ? null : p.QuantityPerUnit.Trim(),
                p.UnitPrice,
                p.UnitsInStock,
                p.UnitsOnOrder,
                p.ReorderLevel,
                DiscontinuedInt = p.Discontinued ? 1 : 0
            };

            return await connection.ExecuteScalarAsync<short>(
                new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task UpdateAsync(Product p, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE products
            SET product_name      = @ProductName,
                supplier_id       = @SupplierId,
                category_id       = @CategoryId,
                quantity_per_unit = @QuantityPerUnit,
                unit_price        = @UnitPrice,
                units_in_stock    = @UnitsInStock,
                units_on_order    = @UnitsOnOrder,
                reorder_level     = @ReorderLevel,
                discontinued      = @DiscontinuedInt
            WHERE product_id = @ProductId;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var parameters = new
            {
                p.ProductId,
                ProductName = p.ProductName.Trim(),
                p.SupplierId,
                p.CategoryId,
                QuantityPerUnit = string.IsNullOrWhiteSpace(p.QuantityPerUnit) ? null : p.QuantityPerUnit.Trim(),
                p.UnitPrice,
                p.UnitsInStock,
                p.UnitsOnOrder,
                p.ReorderLevel,
                DiscontinuedInt = p.Discontinued ? 1 : 0
            };

            await connection.ExecuteAsync(
                new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }
}
