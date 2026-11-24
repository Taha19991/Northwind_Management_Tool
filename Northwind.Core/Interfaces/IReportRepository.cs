using Northwind.Core.Reports;

namespace Northwind.Core.Interfaces;

public interface IReportRepository
{
    Task<KpiSummary> GetKpisAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MonthlyRevenueRow>> GetMonthlyRevenueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmployeeCategoryRevenueRow>> GetEmployeeCategoryRevenueAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LateShipmentRow>> GetLateShipmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InactiveCustomerRow>> GetInactiveCustomersAsync(DateOnly referenceDate, CancellationToken ct = default);
    Task<IReadOnlyList<DiscountImpactRow>> GetDiscountImpactAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TopProductRow>> GetTopProductsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TicketStatusRow>> GetTicketsByStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ResolutionTimeRow>> GetResolutionTimeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReturnReasonRow>> GetReturnsByReasonAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReorderRow>> GetReorderProductsAsync(CancellationToken ct = default);
    string GetSqlText(string reportKey);
    Task<DateOnly> GetReferenceDateAsync(CancellationToken ct = default);
}
