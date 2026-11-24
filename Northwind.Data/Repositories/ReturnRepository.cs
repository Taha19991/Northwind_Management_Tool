using Dapper;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class ReturnRepository(IDbConnectionFactory connectionFactory) : IReturnRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    private const string SelectBaseSql = """
        SELECT v.return_id,
               v.order_id,
               v.product_id,
               r.ticket_id,
               r.processed_by,
               v.return_date,
               v.quantity,
               v.reason,
               v.status,
               r.restocked,
               v.product_name,
               v.refund_amount,
               CASE WHEN e.employee_id IS NOT NULL THEN e.first_name || ' ' || e.last_name END AS processed_by_name
        FROM v_return_overview v
        JOIN product_returns r ON r.return_id = v.return_id
        LEFT JOIN employees e ON e.employee_id = r.processed_by
        """;

    public async Task<IReadOnlyList<ProductReturn>> ListAsync(ReturnStatus? status = null, CancellationToken ct = default)
    {
        var sql = status.HasValue
            ? $"{SelectBaseSql}\nWHERE v.status = @Status\nORDER BY v.return_date DESC, v.return_id DESC;"
            : $"{SelectBaseSql}\nORDER BY v.return_date DESC, v.return_id DESC;";

        object? parameters = status.HasValue
            ? new { Status = EnumText.ToDb(status.Value) }
            : null;

        await using var conn = await _connectionFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ReturnRowDto>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        return rows.Select(MapToModel).ToList();
    }

    public async Task<IReadOnlyList<ProductReturn>> ListByTicketAsync(int ticketId, CancellationToken ct = default)
    {
        const string sql = $"""
            {SelectBaseSql}
            WHERE r.ticket_id = @TicketId
            ORDER BY v.return_date DESC, v.return_id DESC;
            """;

        await using var conn = await _connectionFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ReturnRowDto>(
            new CommandDefinition(sql, new { TicketId = ticketId }, cancellationToken: ct));

        return rows.Select(MapToModel).ToList();
    }

    public async Task<IReadOnlyList<ReturnableOrderLine>> GetReturnableLinesAsync(int orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT od.order_id,
                   od.product_id,
                   p.product_name,
                   od.unit_price,
                   od.discount,
                   od.quantity AS ordered_quantity,
                   COALESCE(ret.returned_qty, 0)::int AS already_returned_quantity
            FROM order_details od
            JOIN products p ON p.product_id = od.product_id
            LEFT JOIN (
                SELECT order_id, product_id, SUM(quantity) AS returned_qty
                FROM product_returns
                WHERE order_id = @OrderId
                  AND status <> 'Rejected'
                GROUP BY order_id, product_id
            ) ret ON ret.order_id = od.order_id AND ret.product_id = od.product_id
            WHERE od.order_id = @OrderId
            ORDER BY p.product_name;
            """;

        await using var conn = await _connectionFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<ReturnableOrderLine>(
            new CommandDefinition(sql, new { OrderId = orderId }, cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<int> CreateAsync(ProductReturn r, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(r);

        const string sql = """
            INSERT INTO product_returns
                (order_id, product_id, ticket_id, processed_by, return_date, quantity, reason, status, restocked)
            VALUES
                (@OrderId, @ProductId, @TicketId, @ProcessedBy, @ReturnDate, @Quantity, @Reason, @Status, FALSE)
            RETURNING return_id;
            """;

        var returnDate = r.ReturnDate == default
            ? DateOnly.FromDateTime(DateTime.Today)
            : r.ReturnDate;

        try
        {
            await using var conn = await _connectionFactory.OpenAsync(ct);
            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        r.OrderId,
                        r.ProductId,
                        r.TicketId,
                        r.ProcessedBy,
                        ReturnDate = returnDate,
                        r.Quantity,
                        Reason = EnumText.ToDb(r.Reason),
                        Status = EnumText.ToDb(r.Status)
                    },
                    cancellationToken: ct));
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public Task ApproveAsync(int id, CancellationToken ct = default) =>
        UpdateStatusAsync(id, ReturnStatus.Approved, ReturnStatus.Requested, ct);

    public Task RejectAsync(int id, CancellationToken ct = default) =>
        UpdateStatusAsync(id, ReturnStatus.Rejected, ReturnStatus.Requested, ct);

    public Task MarkRefundedAsync(int id, CancellationToken ct = default) =>
        UpdateStatusAsync(id, ReturnStatus.Refunded, ReturnStatus.Approved, ct);

    public async Task RestockAsync(int id, CancellationToken ct = default)
    {
        // Trigger tg_returns_restock updates products.units_in_stock automatically.
        const string sql = """
            UPDATE product_returns
            SET restocked = TRUE
            WHERE return_id = @ReturnId
              AND restocked = FALSE
              AND status IN ('Approved', 'Refunded');
            """;

        try
        {
            await using var conn = await _connectionFactory.OpenAsync(ct);
            await conn.ExecuteAsync(
                new CommandDefinition(sql, new { ReturnId = id }, cancellationToken: ct));
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    private async Task UpdateStatusAsync(int id, ReturnStatus newStatus, ReturnStatus requiredCurrentStatus, CancellationToken ct)
    {
        const string sql = """
            UPDATE product_returns
            SET status = @NewStatus
            WHERE return_id = @ReturnId
              AND status = @RequiredStatus;
            """;

        try
        {
            await using var conn = await _connectionFactory.OpenAsync(ct);
            await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ReturnId = id,
                        NewStatus = EnumText.ToDb(newStatus),
                        RequiredStatus = EnumText.ToDb(requiredCurrentStatus)
                    },
                    cancellationToken: ct));
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    private static ProductReturn MapToModel(ReturnRowDto dto) => new()
    {
        ReturnId = dto.ReturnId,
        OrderId = dto.OrderId,
        ProductId = dto.ProductId,
        TicketId = dto.TicketId,
        ProcessedBy = dto.ProcessedBy,
        ReturnDate = dto.ReturnDate,
        Quantity = dto.Quantity,
        Reason = EnumText.ParseReturnReason(dto.Reason),
        Status = EnumText.ParseReturnStatus(dto.Status),
        Restocked = dto.Restocked,
        ProductName = dto.ProductName,
        RefundAmount = dto.RefundAmount,
        ProcessedByName = dto.ProcessedByName
    };

    private sealed class ReturnRowDto
    {
        public int ReturnId { get; set; }
        public int OrderId { get; set; }
        public short ProductId { get; set; }
        public int? TicketId { get; set; }
        public short? ProcessedBy { get; set; }
        public DateOnly ReturnDate { get; set; }
        public short Quantity { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool Restocked { get; set; }
        public string? ProductName { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? ProcessedByName { get; set; }
    }
}
