namespace Northwind.App.Reports;

public enum ReportChartType
{
    Line,
    Bar,
    StackedBar,
    HorizontalBar,
    Pie,
    Table
}

public sealed record ReportDefinition(
    string Code,
    string Key,
    string Title,
    string BusinessQuestion,
    ReportChartType ChartType,
    IReadOnlyList<string> Parameters,
    string CsvSlug)
{
    public bool HasReferenceDateParameter => Parameters.Contains("ReferenceDate");
    public bool HasChart => ChartType != ReportChartType.Table;
    public bool IsPieChart => ChartType == ReportChartType.Pie;
    public bool IsCartesianChart => HasChart && !IsPieChart;
}

public static class ReportCatalog
{
    public static IReadOnlyList<ReportDefinition> All { get; } =
    [
        new(
            "D1",
            "D1_MonthlyRevenue",
            "Monthly Net Revenue",
            "How did net revenue change month by month?",
            ReportChartType.Line,
            [],
            "MonthlyNetRevenue"),

        new(
            "D2",
            "D2_EmployeeCategoryRevenue",
            "Revenue by Employee and Category",
            "Which employee generated how much revenue in which product category?",
            ReportChartType.StackedBar,
            [],
            "RevenueByEmployeeAndCategory"),

        new(
            "D3",
            "D3_LateShipments",
            "Late Shipments by Shipper",
            "How are late deliveries distributed across shipping companies?",
            ReportChartType.Bar,
            [],
            "LateShipments"),

        new(
            "D4",
            "D4_InactiveCustomers",
            "Inactive Customers",
            "Which customers placed no order in the last 6 months?",
            ReportChartType.Table,
            ["ReferenceDate"],
            "InactiveCustomers"),

        new(
            "D5",
            "D5_DiscountImpact",
            "Discount Impact",
            "What is the effect of discounts on total revenue?",
            ReportChartType.StackedBar,
            [],
            "DiscountImpact"),

        new(
            "D6",
            "D6_TopProducts",
            "Top 10 Products",
            "Which 10 products generated the most revenue?",
            ReportChartType.HorizontalBar,
            [],
            "Top10Products"),

        new(
            "D7",
            "D7_TicketsByStatus",
            "Tickets by Category and Status",
            "How are support tickets distributed by category and status?",
            ReportChartType.StackedBar,
            [],
            "TicketsByCategoryAndStatus"),

        new(
            "D8",
            "D8_ResolutionTime",
            "Ticket Resolution Time",
            "How many hours does it take on average to resolve a ticket, by category?",
            ReportChartType.Bar,
            [],
            "TicketResolutionTime"),

        new(
            "D9",
            "D9_ReturnsByReason",
            "Returns by Reason",
            "Why are products returned, and how much is refunded?",
            ReportChartType.Pie,
            [],
            "ReturnsByReason"),

        new(
            "D10",
            "D10_Reorder",
            "Products to Reorder",
            "Which products have fallen to or below their reorder level?",
            ReportChartType.Table,
            [],
            "ProductsToReorder")
    ];

    public static ReportDefinition? FindByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        foreach (var item in All)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Code, key, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }
}
