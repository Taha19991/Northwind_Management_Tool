using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Northwind.App.Charts;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Interfaces;

namespace Northwind.App.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly IReportRepository _reportRepository;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private decimal _totalNetRevenue;

    [ObservableProperty]
    private int _totalOrders;

    [ObservableProperty]
    private int _openOrders;

    [ObservableProperty]
    private int _openTickets;

    [ObservableProperty]
    private int _lowStockProducts;

    [ObservableProperty]
    private bool _hasData;

    // D1: Monthly Net Revenue
    [ObservableProperty]
    private ISeries[] _monthlyRevenueSeries = [];

    [ObservableProperty]
    private Axis[] _monthlyRevenueXAxes = [];

    [ObservableProperty]
    private Axis[] _monthlyRevenueYAxes = [];

    [ObservableProperty]
    private int _monthlyPointCount;

    // D3: Late Shipments
    [ObservableProperty]
    private ISeries[] _lateShipmentsSeries = [];

    [ObservableProperty]
    private Axis[] _lateShipmentsXAxes = [];

    [ObservableProperty]
    private Axis[] _lateShipmentsYAxes = [];

    // D6: Top 10 Products
    [ObservableProperty]
    private ISeries[] _topProductsSeries = [];

    [ObservableProperty]
    private Axis[] _topProductsXAxes = [];

    [ObservableProperty]
    private Axis[] _topProductsYAxes = [];

    public DashboardViewModel(IReportRepository reportRepository, INavigationService navigationService)
    {
        _reportRepository = reportRepository;
        _navigationService = navigationService;
    }

    public override Task InitializeAsync() => LoadKpisAsync();

    [RelayCommand]
    private Task LoadKpisAsync() => RunSafeAsync(async () =>
    {
        var summary = await _reportRepository.GetKpisAsync();
        TotalNetRevenue = summary.TotalNetRevenue;
        TotalOrders = summary.TotalOrders;
        OpenOrders = summary.OpenOrders;
        OpenTickets = summary.OpenTickets;
        LowStockProducts = summary.LowStockProducts;

        var monthlyRows = await _reportRepository.GetMonthlyRevenueAsync();
        var d1Chart = ChartFactory.CreateMonthlyRevenueChart(monthlyRows);
        MonthlyRevenueSeries = d1Chart.Series;
        MonthlyRevenueXAxes = d1Chart.XAxes;
        MonthlyRevenueYAxes = d1Chart.YAxes;
        MonthlyPointCount = monthlyRows.Count;

        var lateRows = await _reportRepository.GetLateShipmentsAsync();
        var d3Chart = ChartFactory.CreateLateShipmentsChart(lateRows);
        LateShipmentsSeries = d3Chart.Series;
        LateShipmentsXAxes = d3Chart.XAxes;
        LateShipmentsYAxes = d3Chart.YAxes;

        var topProductRows = await _reportRepository.GetTopProductsAsync();
        var d6Chart = ChartFactory.CreateTopProductsChart(topProductRows);
        TopProductsSeries = d6Chart.Series;
        TopProductsXAxes = d6Chart.XAxes;
        TopProductsYAxes = d6Chart.YAxes;

        HasData = true;
    });

    [RelayCommand]
    private void OpenReport(string? reportKey)
    {
        if (!string.IsNullOrWhiteSpace(reportKey))
        {
            _navigationService.NavigateTo<ReportsViewModel>(reportKey);
        }
    }

    [RelayCommand]
    private void OpenActiveTickets()
    {
        _navigationService.NavigateTo<TicketsViewModel>("Active");
    }
}
