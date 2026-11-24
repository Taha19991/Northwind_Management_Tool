using System.Data;
using Dapper;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Reports;
using Northwind.Data.Errors;
using Npgsql;

namespace Northwind.Data.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly SqlLoader _sqlLoader;

    public ReportRepository(IDbConnectionFactory connectionFactory, SqlLoader sqlLoader)
    {
        _connectionFactory = connectionFactory;
        _sqlLoader = sqlLoader;
    }

    public async Task<KpiSummary> GetKpisAsync(CancellationToken ct = default)
    {
        try
        {
            var sql = _sqlLoader.Load("K1_Kpis");
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            return await connection.QuerySingleAsync<KpiSummary>(
                new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public string GetSqlText(string reportKey) => _sqlLoader.Load(reportKey);

    public async Task<DateOnly> GetReferenceDateAsync(CancellationToken ct = default)
    {
        try
        {
            const string sql = "SELECT COALESCE(MAX(order_date), CURRENT_DATE) FROM orders;";
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            return await connection.QuerySingleAsync<DateOnly>(
                new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false);
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    public Task<IReadOnlyList<MonthlyRevenueRow>> GetMonthlyRevenueAsync(CancellationToken ct = default) =>
        QueryReportAsync<MonthlyRevenueRow>("D1_MonthlyRevenue", parameters: null, ct);

    public Task<IReadOnlyList<EmployeeCategoryRevenueRow>> GetEmployeeCategoryRevenueAsync(CancellationToken ct = default) =>
        QueryReportAsync<EmployeeCategoryRevenueRow>("D2_EmployeeCategoryRevenue", parameters: null, ct);

    public Task<IReadOnlyList<LateShipmentRow>> GetLateShipmentsAsync(CancellationToken ct = default) =>
        QueryReportAsync<LateShipmentRow>("D3_LateShipments", parameters: null, ct);

    public Task<IReadOnlyList<InactiveCustomerRow>> GetInactiveCustomersAsync(DateOnly referenceDate, CancellationToken ct = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ReferenceDate", referenceDate, DbType.Date);
        return QueryReportAsync<InactiveCustomerRow>("D4_InactiveCustomers", parameters, ct);
    }

    public Task<IReadOnlyList<DiscountImpactRow>> GetDiscountImpactAsync(CancellationToken ct = default) =>
        QueryReportAsync<DiscountImpactRow>("D5_DiscountImpact", parameters: null, ct);

    public Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(CancellationToken ct = default) =>
        QueryReportAsync<TopProductRow>("D6_TopProducts", parameters: null, ct);

    public async Task<IReadOnlyList<TicketStatusRow>> GetTicketsByStatusAsync(CancellationToken ct = default)
    {
        var raw = await QueryReportAsync<TicketStatusDto>("D7_TicketsByStatus", parameters: null, ct).ConfigureAwait(false);
        var result = new List<TicketStatusRow>(raw.Count);
        foreach (var row in raw)
        {
            result.Add(new TicketStatusRow
            {
                Category = EnumText.ParseTicketCategory(row.Category),
                Status = EnumText.ParseTicketStatus(row.Status),
                Tickets = row.Tickets
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<ResolutionTimeRow>> GetResolutionTimeAsync(CancellationToken ct = default)
    {
        var raw = await QueryReportAsync<ResolutionTimeDto>("D8_ResolutionTime", parameters: null, ct).ConfigureAwait(false);
        var result = new List<ResolutionTimeRow>(raw.Count);
        foreach (var row in raw)
        {
            result.Add(new ResolutionTimeRow
            {
                Category = EnumText.ParseTicketCategory(row.Category),
                AvgHours = row.AvgHours
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<ReturnReasonRow>> GetReturnsByReasonAsync(CancellationToken ct = default)
    {
        var raw = await QueryReportAsync<ReturnReasonDto>("D9_ReturnsByReason", parameters: null, ct).ConfigureAwait(false);
        var result = new List<ReturnReasonRow>(raw.Count);
        foreach (var row in raw)
        {
            result.Add(new ReturnReasonRow
            {
                Reason = EnumText.ParseReturnReason(row.Reason),
                Returns = row.Returns,
                Units = row.Units,
                Refund = row.Refund
            });
        }
        return result;
    }

    public Task<IReadOnlyList<ReorderRow>> GetReorderProductsAsync(CancellationToken ct = default) =>
        QueryReportAsync<ReorderRow>("D10_Reorder", parameters: null, ct);

    private async Task<IReadOnlyList<T>> QueryReportAsync<T>(
        string reportKey,
        object? parameters,
        CancellationToken ct)
    {
        try
        {
            var sql = _sqlLoader.Load(reportKey);
            await using var connection = await _connectionFactory.OpenAsync(ct).ConfigureAwait(false);
            var rows = await connection.QueryAsync<T>(
                new CommandDefinition(sql, parameters, cancellationToken: ct)).ConfigureAwait(false);
            return rows.AsList();
        }
        catch (PostgresException ex)
        {
            throw PostgresErrorMapper.ToUserFriendlyException(ex);
        }
    }

    // Dapper calls Enum.Parse directly for enum properties instead of using TypeHandler,
    // so text values with spaces (e.g. 'In progress', 'Late delivery', 'Not needed')
    // are read into string DTOs and mapped safely via EnumText.
    private sealed class TicketStatusDto
    {
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Tickets { get; set; }
    }

    private sealed class ResolutionTimeDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal AvgHours { get; set; }
    }

    private sealed class ReturnReasonDto
    {
        public string Reason { get; set; } = string.Empty;
        public int Returns { get; set; }
        public int Units { get; set; }
        public decimal Refund { get; set; }
    }
}
