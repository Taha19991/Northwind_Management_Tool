using System.Data;
using Dapper;
using Northwind.Core.Enums;
using Northwind.Core.Filters;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class OrderRepository(IDbConnectionFactory connectionFactory) : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<Order>> ListAsync(OrderFilter f, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(f);

        const string sql = """
            SELECT o.order_id, o.customer_id, o.employee_id, o.order_date,
                   o.required_date, o.shipped_date, o.ship_via, o.freight,
                   o.ship_name, o.ship_address, o.ship_city, o.ship_region,
                   o.ship_postal_code, o.ship_country,
                   c.company_name AS customer_name,
                   e.first_name || ' ' || e.last_name AS employee_name,
                   s.company_name AS shipper_name,
                   COALESCE(ROUND(SUM(v.net_amount), 2), 0) AS net_total
            FROM orders o
            JOIN customers c ON c.customer_id = o.customer_id
            JOIN employees e ON e.employee_id = o.employee_id
            JOIN shippers  s ON s.shipper_id  = o.ship_via
            LEFT JOIN v_order_lines v ON v.order_id = o.order_id
            WHERE (CAST(@CustomerId AS varchar(5)) IS NULL OR o.customer_id = @CustomerId)
              AND (CAST(@EmployeeId AS smallint)   IS NULL OR o.employee_id = @EmployeeId)
              AND (CAST(@ShipVia    AS smallint)   IS NULL OR o.ship_via    = @ShipVia)
              AND (CAST(@FromDate   AS date)       IS NULL OR o.order_date >= @FromDate)
              AND (CAST(@ToDate     AS date)       IS NULL OR o.order_date <= @ToDate)
              AND (
                  CAST(@Status AS varchar(20)) IS NULL
                  OR (@Status = 'Pending' AND o.shipped_date IS NULL)
                  OR (@Status = 'Shipped' AND o.shipped_date IS NOT NULL
                      AND (o.required_date IS NULL OR o.shipped_date <= o.required_date))
                  OR (@Status = 'Late'    AND o.shipped_date IS NOT NULL
                      AND o.required_date IS NOT NULL AND o.shipped_date > o.required_date)
              )
            GROUP BY o.order_id, o.customer_id, o.employee_id, o.order_date,
                     o.required_date, o.shipped_date, o.ship_via, o.freight,
                     o.ship_name, o.ship_address, o.ship_city, o.ship_region,
                     o.ship_postal_code, o.ship_country,
                     c.company_name, e.first_name, e.last_name, s.company_name
            ORDER BY o.order_date DESC, o.order_id DESC;
            """;

        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("CustomerId", string.IsNullOrWhiteSpace(f.CustomerId) ? null : f.CustomerId.Trim().ToUpperInvariant(), DbType.String);
            parameters.Add("EmployeeId", f.EmployeeId, DbType.Int16);
            parameters.Add("ShipVia", f.ShipVia, DbType.Int16);
            parameters.Add("FromDate", f.FromDate, DbType.Date);
            parameters.Add("ToDate", f.ToDate, DbType.Date);
            parameters.Add("Status", f.Status switch
            {
                OrderStatus.Pending => "Pending",
                OrderStatus.Shipped => "Shipped",
                OrderStatus.Late => "Late",
                _ => null
            }, DbType.String);

            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<Order>(
                new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<Order?> GetWithLinesAsync(int id, CancellationToken ct = default)
    {
        const string headerSql = """
            SELECT o.order_id, o.customer_id, o.employee_id, o.order_date,
                   o.required_date, o.shipped_date, o.ship_via, o.freight,
                   o.ship_name, o.ship_address, o.ship_city, o.ship_region,
                   o.ship_postal_code, o.ship_country,
                   c.company_name AS customer_name,
                   e.first_name || ' ' || e.last_name AS employee_name,
                   s.company_name AS shipper_name
            FROM orders o
            JOIN customers c ON c.customer_id = o.customer_id
            JOIN employees e ON e.employee_id = o.employee_id
            JOIN shippers  s ON s.shipper_id  = o.ship_via
            WHERE o.order_id = @Id;
            """;

        const string linesSql = """
            SELECT od.order_id, od.product_id, od.unit_price, od.quantity, od.discount,
                   p.product_name
            FROM order_details od
            JOIN products p ON p.product_id = od.product_id
            WHERE od.order_id = @Id
            ORDER BY od.product_id;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var order = await connection.QuerySingleOrDefaultAsync<Order>(
                new CommandDefinition(headerSql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false);

            if (order is null)
            {
                return null;
            }

            var lines = (await connection.QueryAsync<OrderLine>(
                new CommandDefinition(linesSql, new { Id = id }, cancellationToken: ct)).ConfigureAwait(false)).AsList();

            order.Lines = lines;

            decimal netSum = 0m;
            foreach (var line in lines)
            {
                netSum += line.NetAmount;
            }
            order.NetTotal = Math.Round(netSum, 2, MidpointRounding.AwayFromZero);

            return order;
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task<int> CreateOrderAsync(Order o, IEnumerable<OrderLine> lines, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(o);
        ArgumentNullException.ThrowIfNull(lines);

        var lineList = lines.ToList();
        if (lineList.Count == 0)
        {
            throw new Core.Exceptions.UserFriendlyException("An order must contain at least one product line.");
        }

        const string insertOrderSql = """
            INSERT INTO orders (
                customer_id, employee_id, order_date, required_date, ship_via, freight,
                ship_name, ship_address, ship_city, ship_region, ship_postal_code, ship_country)
            VALUES (
                @CustomerId, @EmployeeId, @OrderDate, @RequiredDate, @ShipVia, @Freight,
                @ShipName, @ShipAddress, @ShipCity, @ShipRegion, @ShipPostalCode, @ShipCountry)
            RETURNING order_id;
            """;

        const string insertLineSql = """
            INSERT INTO order_details (order_id, product_id, unit_price, quantity, discount)
            VALUES (@OrderId, @ProductId, @UnitPrice, @Quantity, @Discount);
            """;

        const string deductStockSql = """
            UPDATE products
            SET units_in_stock = units_in_stock - @Quantity
            WHERE product_id = @ProductId AND units_in_stock >= @Quantity;
            """;

        const string productStockSql = """
            SELECT product_name, COALESCE(units_in_stock, 0) AS units_in_stock
            FROM products
            WHERE product_id = @ProductId;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var orderParams = new
            {
                CustomerId = o.CustomerId.Trim().ToUpperInvariant(),
                o.EmployeeId,
                o.OrderDate,
                o.RequiredDate,
                o.ShipVia,
                Freight = o.Freight ?? 0m,
                ShipName = NullIfBlank(o.ShipName),
                ShipAddress = NullIfBlank(o.ShipAddress),
                ShipCity = NullIfBlank(o.ShipCity),
                ShipRegion = NullIfBlank(o.ShipRegion),
                ShipPostalCode = NullIfBlank(o.ShipPostalCode),
                ShipCountry = NullIfBlank(o.ShipCountry)
            };

            var newOrderId = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(insertOrderSql, orderParams, transaction: tx, cancellationToken: ct))
                .ConfigureAwait(false);

            foreach (var line in lineList)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        insertLineSql,
                        new
                        {
                            OrderId = newOrderId,
                            line.ProductId,
                            line.UnitPrice,
                            line.Quantity,
                            line.Discount
                        },
                        transaction: tx,
                        cancellationToken: ct)).ConfigureAwait(false);

                var affected = await connection.ExecuteAsync(
                    new CommandDefinition(
                        deductStockSql,
                        new { line.ProductId, line.Quantity },
                        transaction: tx,
                        cancellationToken: ct)).ConfigureAwait(false);

                if (affected == 0)
                {
                    var stockInfo = await connection.QuerySingleOrDefaultAsync<ProductStockInfo>(
                        new CommandDefinition(
                            productStockSql,
                            new { line.ProductId },
                            transaction: tx,
                            cancellationToken: ct)).ConfigureAwait(false);

                    var productName = stockInfo?.ProductName ?? line.ProductName ?? $"Product #{line.ProductId}";
                    var currentStock = stockInfo?.UnitsInStock ?? 0;

                    await tx.RollbackAsync(ct).ConfigureAwait(false);
                    throw new Core.Exceptions.UserFriendlyException(
                        $"Insufficient stock: {productName} ({currentStock} in stock)");
                }
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
            return newOrderId;
        }
        catch (Core.Exceptions.UserFriendlyException)
        {
            throw;
        }
        catch (PostgresException ex)
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
        catch
        {
            await tx.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task ShipAsync(
        int orderId,
        short shipperId,
        decimal freight,
        DateOnly shippedDate,
        CancellationToken ct = default)
    {
        const string sql = """
            UPDATE orders
            SET ship_via = @ShipVia,
                freight = @Freight,
                shipped_date = @ShippedDate
            WHERE order_id = @OrderId AND shipped_date IS NULL;
            """;

        try
        {
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        OrderId = orderId,
                        ShipVia = shipperId,
                        Freight = freight,
                        ShippedDate = shippedDate
                    },
                    cancellationToken: ct)).ConfigureAwait(false);

            if (affected == 0)
            {
                throw new Core.Exceptions.UserFriendlyException("This order has already been shipped.");
            }
        }
        catch (Core.Exceptions.UserFriendlyException)
        {
            throw;
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class ProductStockInfo
    {
        public string ProductName { get; set; } = string.Empty;
        public short UnitsInStock { get; set; }
    }
}
