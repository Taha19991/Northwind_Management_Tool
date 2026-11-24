using System.Globalization;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Northwind.Core.Enums;
using Northwind.Core.Reports;
using SkiaSharp;

namespace Northwind.App.Charts;

public sealed record CartesianChartModel(
    ISeries[] Series,
    Axis[] XAxes,
    Axis[] YAxes,
    LegendPosition LegendPosition = LegendPosition.Hidden);

public sealed record PieChartModel(
    ISeries[] Series,
    LegendPosition LegendPosition = LegendPosition.Right);

public static class ChartFactory
{
    private static readonly CultureInfo ChartCulture = CultureInfo.GetCultureInfo("en-US");

    // Consistent MaterialDesign palette
    private static readonly SKColor PrimaryBlue = new(21, 101, 192);
    private static readonly SKColor PrimaryBlueFill = new(21, 101, 192, 36);
    private static readonly SKColor Indigo = new(57, 73, 171);
    private static readonly SKColor Teal = new(0, 137, 123);
    private static readonly SKColor Green = new(46, 125, 50);
    private static readonly SKColor Amber = new(237, 108, 2);
    private static readonly SKColor DeepOrange = new(216, 67, 21);
    private static readonly SKColor Red = new(198, 40, 40);
    private static readonly SKColor Purple = new(123, 31, 162);
    private static readonly SKColor Cyan = new(0, 151, 167);
    private static readonly SKColor BlueGray = new(96, 125, 139);
    private static readonly SKColor AxisTextColor = new(71, 85, 105);
    private static readonly SKColor GridLineColor = new(226, 232, 240);

    private static readonly Dictionary<string, SKColor> ProductCategoryColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Beverages"] = PrimaryBlue,
        ["Condiments"] = Amber,
        ["Confections"] = Purple,
        ["Dairy Products"] = Teal,
        ["Grains/Cereals"] = Green,
        ["Meat/Poultry"] = Red,
        ["Produce"] = Cyan,
        ["Seafood"] = Indigo
    };

    private static readonly Dictionary<TicketCategory, SKColor> TicketCategoryColors = new()
    {
        [TicketCategory.LateDelivery] = Amber,
        [TicketCategory.DamagedProduct] = Red,
        [TicketCategory.WrongItem] = Purple,
        [TicketCategory.Billing] = PrimaryBlue,
        [TicketCategory.Other] = BlueGray
    };

    private static readonly Dictionary<TicketStatus, SKColor> TicketStatusColors = new()
    {
        [TicketStatus.Open] = Red,
        [TicketStatus.InProgress] = Amber,
        [TicketStatus.Resolved] = Green,
        [TicketStatus.Closed] = BlueGray
    };

    private static readonly Dictionary<ReturnReason, SKColor> ReturnReasonColors = new()
    {
        [ReturnReason.Damaged] = Red,
        [ReturnReason.WrongItem] = Purple,
        [ReturnReason.Expired] = Amber,
        [ReturnReason.NotNeeded] = PrimaryBlue,
        [ReturnReason.Other] = BlueGray
    };

    private static readonly SKColor[] FallbackPalette =
    [
        PrimaryBlue, Teal, Amber, Purple, Green, DeepOrange, Cyan, Indigo, Red, BlueGray
    ];

    public static ISeries[] CreateEmptySeries() => [];

    // D1: Monthly Net Revenue (Line)
    public static CartesianChartModel CreateMonthlyRevenueChart(IReadOnlyList<MonthlyRevenueRow> rows)
    {
        var values = new decimal[rows.Count];
        var labels = new string[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            values[i] = rows[i].NetRevenue;
            labels[i] = rows[i].Month.ToString("MMM yyyy", ChartCulture);
        }

        ISeries[] series =
        [
            new LineSeries<decimal>
            {
                Name = "Monthly Net Revenue ($)",
                Values = values,
                Stroke = new SolidColorPaint(PrimaryBlue) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(PrimaryBlueFill),
                GeometryStroke = new SolidColorPaint(PrimaryBlue) { StrokeThickness = 2.5f },
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 8,
                LineSmoothness = 0.25,
                YToolTipLabelFormatter = point => FormatCurrencyDetailed(point.Model)
            }
        ];

        Axis[] xAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsRotation = -40,
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = null
            }
        ];

        Axis[] yAxes = [CreateCurrencyAxis()];

        return new CartesianChartModel(series, xAxes, yAxes, LegendPosition.Hidden);
    }

    // D2: Revenue by Employee and Category (Stacked Column)
    public static CartesianChartModel CreateEmployeeCategoryRevenueChart(IReadOnlyList<EmployeeCategoryRevenueRow> rows)
    {
        var employees = new List<string>();
        var categories = new List<string>();
        var matrix = new Dictionary<(string Employee, string Category), decimal>(rows.Count);

        foreach (var row in rows)
        {
            if (!employees.Contains(row.Employee)) employees.Add(row.Employee);
            if (!categories.Contains(row.CategoryName)) categories.Add(row.CategoryName);
            matrix[(row.Employee, row.CategoryName)] = row.NetRevenue;
        }

        employees.Sort(StringComparer.CurrentCultureIgnoreCase);
        categories.Sort(StringComparer.OrdinalIgnoreCase);

        var seriesList = new List<ISeries>(categories.Count);
        for (var c = 0; c < categories.Count; c++)
        {
            var category = categories[c];
            var values = new decimal[employees.Count];
            for (var e = 0; e < employees.Count; e++)
            {
                values[e] = matrix.TryGetValue((employees[e], category), out var amount) ? amount : 0m;
            }

            seriesList.Add(new StackedColumnSeries<decimal>
            {
                Name = category,
                Values = values,
                Fill = new SolidColorPaint(GetProductCategoryColor(category, c)),
                MaxBarWidth = 38,
                YToolTipLabelFormatter = point => FormatCurrencyDetailed(point.Model)
            });
        }

        Axis[] xAxes =
        [
            new Axis
            {
                Labels = employees,
                LabelsRotation = -22,
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = null
            }
        ];

        Axis[] yAxes = [CreateCurrencyAxis()];

        return new CartesianChartModel(seriesList.ToArray(), xAxes, yAxes, LegendPosition.Right);
    }

    // D3: Late Shipments by Shipper (Column with percentage labels)
    public static CartesianChartModel CreateLateShipmentsChart(IReadOnlyList<LateShipmentRow> rows)
    {
        var labels = new string[rows.Count];
        var values = new int[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            labels[i] = rows[i].Shipper;
            values[i] = rows[i].Late;
        }

        ISeries[] series =
        [
            new ColumnSeries<int>
            {
                Name = "Late Orders",
                Values = values,
                Fill = new SolidColorPaint(DeepOrange),
                MaxBarWidth = 54,
                DataLabelsPaint = new SolidColorPaint(AxisTextColor),
                DataLabelsSize = 12,
                DataLabelsPosition = DataLabelsPosition.Top,
                DataLabelsFormatter = point =>
                {
                    var idx = point.Index;
                    if (idx < 0 || idx >= rows.Count) return point.Model.ToString(ChartCulture);
                    var pct = (rows[idx].LatePct ?? 0m).ToString("N1", ChartCulture);
                    return $"{rows[idx].Late} ({pct}%)";
                },
                YToolTipLabelFormatter = point =>
                {
                    var idx = point.Index;
                    if (idx < 0 || idx >= rows.Count) return $"{point.Model} late";
                    var r = rows[idx];
                    var pct = (r.LatePct ?? 0m).ToString("N1", ChartCulture);
                    return $"{r.Late} late / {r.Shipped} shipped ({pct}%)";
                }
            }
        ];

        Axis[] xAxes =
        [
            new Axis
            {
                Labels = labels,
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = null
            }
        ];

        Axis[] yAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MinStep = 2,
                TextSize = 11.5,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = new SolidColorPaint(GridLineColor) { StrokeThickness = 1 },
                Labeler = value => value.ToString("N0", ChartCulture)
            }
        ];

        return new CartesianChartModel(series, xAxes, yAxes, LegendPosition.Hidden);
    }

    // D5: Discount Impact (Stacked Column: Net Revenue + Discount Given)
    public static CartesianChartModel CreateDiscountImpactChart(IReadOnlyList<DiscountImpactRow> rows)
    {
        var labels = new string[rows.Count];
        var netValues = new decimal[rows.Count];
        var discountValues = new decimal[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            labels[i] = rows[i].LineType;
            netValues[i] = rows[i].Net;
            discountValues[i] = rows[i].Discount;
        }

        ISeries[] series =
        [
            new StackedColumnSeries<decimal>
            {
                Name = "Net Revenue ($)",
                Values = netValues,
                Fill = new SolidColorPaint(PrimaryBlue),
                MaxBarWidth = 72,
                YToolTipLabelFormatter = point => FormatCurrencyDetailed(point.Model)
            },
            new StackedColumnSeries<decimal>
            {
                Name = "Discount Given ($)",
                Values = discountValues,
                Fill = new SolidColorPaint(Amber),
                MaxBarWidth = 72,
                YToolTipLabelFormatter = point => FormatCurrencyDetailed(point.Model)
            }
        ];

        Axis[] xAxes =
        [
            new Axis
            {
                Labels = labels,
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 12.5,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = null
            }
        ];

        Axis[] yAxes = [CreateCurrencyAxis()];

        return new CartesianChartModel(series, xAxes, yAxes, LegendPosition.Bottom);
    }

    // D6: Top 10 Products (Horizontal Bar — highest revenue at top)
    public static CartesianChartModel CreateTopProductsChart(IReadOnlyList<TopProductRow> rows)
    {
        // RowSeries draws index 0 at the bottom, so reverse to place the highest value at the top.
        var count = rows.Count;
        var labels = new string[count];
        var values = new decimal[count];

        for (var i = 0; i < count; i++)
        {
            var src = rows[count - 1 - i];
            labels[i] = src.ProductName;
            values[i] = src.NetRevenue;
        }

        ISeries[] series =
        [
            new RowSeries<decimal>
            {
                Name = "Net Revenue ($)",
                Values = values,
                Fill = new SolidColorPaint(Teal),
                MaxBarWidth = 22,
                XToolTipLabelFormatter = point => FormatCurrencyDetailed(point.Model)
            }
        ];

        Axis[] xAxes =
        [
            new Axis
            {
                MinLimit = 0,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = new SolidColorPaint(GridLineColor) { StrokeThickness = 1 },
                Labeler = value => $"${value.ToString("N0", ChartCulture)}"
            }
        ];

        Axis[] yAxes =
        [
            new Axis
            {
                Labels = labels,
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = null
            }
        ];

        return new CartesianChartModel(series, xAxes, yAxes, LegendPosition.Hidden);
    }

    // D7: Tickets by Category and Status (Stacked Column)
    public static CartesianChartModel CreateTicketsByStatusChart(IReadOnlyList<TicketStatusRow> rows)
    {
        var categories = new List<TicketCategory>();
        var matrix = new Dictionary<(TicketCategory Category, TicketStatus Status), int>(rows.Count);

        foreach (var row in rows)
        {
            if (!categories.Contains(row.Category)) categories.Add(row.Category);
            matrix[(row.Category, row.Status)] = row.Tickets;
        }

        categories.Sort();

        var labels = new string[categories.Count];
        for (var i = 0; i < categories.Count; i++)
        {
            labels[i] = EnumText.ToDisplay(categories[i]);
        }

        TicketStatus[] statuses =
        [
            TicketStatus.Open,
            TicketStatus.InProgress,
            TicketStatus.Resolved,
            TicketStatus.Closed
        ];

        var seriesList = new List<ISeries>(statuses.Length);
        foreach (var status in statuses)
        {
            var values = new int[categories.Count];
            for (var c = 0; c < categories.Count; c++)
            {
                values[c] = matrix.TryGetValue((categories[c], status), out var count) ? count : 0;
            }

            seriesList.Add(new StackedColumnSeries<int>
            {
                Name = EnumText.ToDisplay(status),
                Values = values,
                Fill = new SolidColorPaint(TicketStatusColors.GetValueOrDefault(status, BlueGray)),
                MaxBarWidth = 48,
                YToolTipLabelFormatter = point => $"{point.Model} tickets"
            });
        }

        Axis[] xAxes =
        [
            new Axis
            {
                Labels = labels,
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 11.5,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = null
            }
        ];

        Axis[] yAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MinStep = 1,
                TextSize = 11.5,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = new SolidColorPaint(GridLineColor) { StrokeThickness = 1 },
                Labeler = value => value.ToString("N0", ChartCulture)
            }
        ];

        return new CartesianChartModel(seriesList.ToArray(), xAxes, yAxes, LegendPosition.Bottom);
    }

    // D8: Ticket Resolution Time (Column)
    public static CartesianChartModel CreateResolutionTimeChart(IReadOnlyList<ResolutionTimeRow> rows)
    {
        var labels = new string[rows.Count];
        var values = new decimal[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            labels[i] = EnumText.ToDisplay(rows[i].Category);
            values[i] = rows[i].AvgHours;
        }

        ISeries[] series =
        [
            new ColumnSeries<decimal>
            {
                Name = "Avg. Resolution (Hours)",
                Values = values,
                Fill = new SolidColorPaint(Indigo),
                MaxBarWidth = 54,
                DataLabelsPaint = new SolidColorPaint(AxisTextColor),
                DataLabelsSize = 12,
                DataLabelsPosition = DataLabelsPosition.Top,
                DataLabelsFormatter = point => $"{point.Model.ToString("N1", ChartCulture)} h",
                YToolTipLabelFormatter = point => $"{point.Model.ToString("N1", ChartCulture)} hours"
            }
        ];

        Axis[] xAxes =
        [
            new Axis
            {
                Labels = labels,
                MinStep = 1,
                ForceStepToMin = true,
                TextSize = 11.5,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = null
            }
        ];

        Axis[] yAxes =
        [
            new Axis
            {
                MinLimit = 0,
                TextSize = 11.5,
                LabelsPaint = new SolidColorPaint(AxisTextColor),
                SeparatorsPaint = new SolidColorPaint(GridLineColor) { StrokeThickness = 1 },
                Labeler = value => $"{value.ToString("N0", ChartCulture)} h"
            }
        ];

        return new CartesianChartModel(series, xAxes, yAxes, LegendPosition.Hidden);
    }

    // D9: Returns by Reason (Pie)
    public static PieChartModel CreateReturnsByReasonChart(IReadOnlyList<ReturnReasonRow> rows)
    {
        var series = new ISeries[rows.Count];

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var displayLabel = EnumText.ToDisplay(row.Reason);
            var color = ReturnReasonColors.TryGetValue(row.Reason, out var c)
                ? c
                : FallbackPalette[i % FallbackPalette.Length];

            series[i] = new PieSeries<decimal>
            {
                Name = displayLabel,
                Values = [row.Refund],
                Fill = new SolidColorPaint(color),
                DataLabelsPaint = new SolidColorPaint(SKColors.White),
                DataLabelsPosition = PolarLabelsPosition.Middle,
                DataLabelsSize = 12,
                DataLabelsFormatter = point => FormatCurrencyDetailed(point.Model),
                ToolTipLabelFormatter = point =>
                    $"{displayLabel}: {FormatCurrencyDetailed(point.Model)} ({row.Returns} returns)"
            };
        }

        return new PieChartModel(series, LegendPosition.Right);
    }

    public static SKColor GetTicketCategoryColor(TicketCategory category) =>
        TicketCategoryColors.GetValueOrDefault(category, BlueGray);

    private static SKColor GetProductCategoryColor(string categoryName, int index) =>
        ProductCategoryColors.TryGetValue(categoryName, out var color)
            ? color
            : FallbackPalette[index % FallbackPalette.Length];

    private static Axis CreateCurrencyAxis() =>
        new()
        {
            MinLimit = 0,
            TextSize = 11.5,
            LabelsPaint = new SolidColorPaint(AxisTextColor),
            SeparatorsPaint = new SolidColorPaint(GridLineColor) { StrokeThickness = 1 },
            Labeler = value => $"${value.ToString("N0", ChartCulture)}"
        };

    private static string FormatCurrencyDetailed(decimal amount) =>
        $"${amount.ToString("N2", ChartCulture)}";
}
