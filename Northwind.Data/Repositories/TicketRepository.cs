using Dapper;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class TicketRepository(IDbConnectionFactory connectionFactory) : ITicketRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    private const string SelectBaseSql = """
        SELECT v.ticket_id,
               v.order_id,
               v.customer_id,
               t.assigned_employee_id,
               v.category,
               v.priority,
               v.status,
               v.subject,
               v.created_at,
               v.resolved_at,
               v.company_name,
               v.assigned_to
        FROM v_ticket_overview v
        JOIN support_tickets t ON t.ticket_id = v.ticket_id
        """;

    public async Task<IReadOnlyList<SupportTicket>> ListAsync(TicketStatus? status, CancellationToken ct = default)
    {
        var sql = status.HasValue
            ? $"{SelectBaseSql}\nWHERE v.status = @Status\nORDER BY v.created_at DESC, v.ticket_id DESC;"
            : $"{SelectBaseSql}\nORDER BY v.created_at DESC, v.ticket_id DESC;";

        object? parameters = status.HasValue
            ? new { Status = EnumText.ToDb(status.Value) }
            : null;

        await using var conn = await _connectionFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<TicketRowDto>(
            new CommandDefinition(sql, parameters, cancellationToken: ct));

        return rows.Select(MapToModel).ToList();
    }

    public async Task<SupportTicket?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT v.ticket_id,
                   v.order_id,
                   v.customer_id,
                   t.assigned_employee_id,
                   v.category,
                   v.priority,
                   v.status,
                   v.subject,
                   t.description,
                   v.created_at,
                   v.resolved_at,
                   v.company_name,
                   v.assigned_to
            FROM v_ticket_overview v
            JOIN support_tickets t ON t.ticket_id = v.ticket_id
            WHERE v.ticket_id = @TicketId;
            """;

        await using var conn = await _connectionFactory.OpenAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<TicketRowDto>(
            new CommandDefinition(sql, new { TicketId = id }, cancellationToken: ct));

        return row is null ? null : MapToModel(row);
    }

    public async Task<IReadOnlyList<SupportTicket>> ListByOrderAsync(int orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT v.ticket_id,
                   v.order_id,
                   v.customer_id,
                   t.assigned_employee_id,
                   v.category,
                   v.priority,
                   v.status,
                   v.subject,
                   v.created_at,
                   v.resolved_at,
                   v.company_name,
                   v.assigned_to
            FROM v_ticket_overview v
            JOIN support_tickets t ON t.ticket_id = v.ticket_id
            WHERE v.order_id = @OrderId
            ORDER BY v.created_at DESC, v.ticket_id DESC;
            """;

        await using var conn = await _connectionFactory.OpenAsync(ct);
        var rows = await conn.QueryAsync<TicketRowDto>(
            new CommandDefinition(sql, new { OrderId = orderId }, cancellationToken: ct));

        return rows.Select(MapToModel).ToList();
    }

    public async Task<int> CreateAsync(SupportTicket t, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(t);

        const string sql = """
            INSERT INTO support_tickets
                (order_id, customer_id, assigned_employee_id, category, priority, status, subject, description)
            VALUES
                (@OrderId, @CustomerId, @AssignedEmployeeId, @Category, @Priority, @Status, @Subject, @Description)
            RETURNING ticket_id;
            """;

        try
        {
            await using var conn = await _connectionFactory.OpenAsync(ct);
            return await conn.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        t.OrderId,
                        CustomerId = string.IsNullOrWhiteSpace(t.CustomerId) ? null : t.CustomerId.Trim(),
                        t.AssignedEmployeeId,
                        Category = EnumText.ToDb(t.Category),
                        Priority = EnumText.ToDb(t.Priority),
                        Status = EnumText.ToDb(t.Status),
                        Subject = t.Subject.Trim(),
                        Description = string.IsNullOrWhiteSpace(t.Description) ? null : t.Description.Trim()
                    },
                    cancellationToken: ct));
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task AssignAsync(int id, short? employeeId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE support_tickets
            SET assigned_employee_id = @EmployeeId
            WHERE ticket_id = @TicketId;
            """;

        try
        {
            await using var conn = await _connectionFactory.OpenAsync(ct);
            await conn.ExecuteAsync(
                new CommandDefinition(sql, new { TicketId = id, EmployeeId = employeeId }, cancellationToken: ct));
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public async Task ChangeStatusAsync(int id, TicketStatus status, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE support_tickets
            SET status = @Status,
                resolved_at = CASE
                    WHEN @Status = 'Resolved' THEN now()
                    WHEN @Status = 'In progress' THEN NULL
                    ELSE resolved_at
                END
            WHERE ticket_id = @TicketId
              AND status <> 'Closed';
            """;

        try
        {
            await using var conn = await _connectionFactory.OpenAsync(ct);
            await conn.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new { TicketId = id, Status = EnumText.ToDb(status) },
                    cancellationToken: ct));
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    private static SupportTicket MapToModel(TicketRowDto dto) => new()
    {
        TicketId = dto.TicketId,
        OrderId = dto.OrderId,
        CustomerId = dto.CustomerId,
        AssignedEmployeeId = dto.AssignedEmployeeId,
        Category = EnumText.ParseTicketCategory(dto.Category),
        Priority = EnumText.ParseTicketPriority(dto.Priority),
        Status = EnumText.ParseTicketStatus(dto.Status),
        Subject = dto.Subject,
        Description = dto.Description,
        CreatedAt = dto.CreatedAt,
        ResolvedAt = dto.ResolvedAt,
        CompanyName = dto.CompanyName,
        AssignedTo = dto.AssignedTo
    };

    private sealed class TicketRowDto
    {
        public int TicketId { get; set; }
        public int? OrderId { get; set; }
        public string? CustomerId { get; set; }
        public short? AssignedEmployeeId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? CompanyName { get; set; }
        public string? AssignedTo { get; set; }
    }
}
