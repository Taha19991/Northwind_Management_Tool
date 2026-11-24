using System.Collections;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using Northwind.App.Charts;
using Northwind.App.Reports;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Interfaces;
using Northwind.Core.Reports;

namespace Northwind.App.ViewModels;

public sealed partial class ReportsViewModel : ViewModelBase
{
    private readonly IReportRepository _reportRepository;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ICsvExportService _csvExportService;
    private CancellationTokenSource? _runCts;
    private Type? _resultItemType;
    private bool _isInitializing;

    public ReportsViewModel(
        IReportRepository reportRepository,
        ISettingsService settingsService,
        IDialogService dialogService,
        ICsvExportService csvExportService)
    {
        _reportRepository = reportRepository;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _csvExportService = csvExportService;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy) or nameof(ActiveReport) or nameof(ResultRows))
            {
                RunReportCommand.NotifyCanExecuteChanged();
                ShowSqlCommand.NotifyCanExecuteChanged();
                ExportCsvCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public IReadOnlyList<ReportDefinition> Catalog => ReportCatalog.All;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReferenceDateParameter))]
    [NotifyPropertyChangedFor(nameof(HasChart))]
    [NotifyPropertyChangedFor(nameof(IsCartesianChart))]
    [NotifyPropertyChangedFor(nameof(IsPieChart))]
    private ReportDefinition? _activeReport;

    [ObservableProperty]
    private DateTime? _referenceDate;

    [ObservableProperty]
    private IEnumerable? _resultRows;

    [ObservableProperty]
    private int _rowCount;

    [ObservableProperty]
    private long _elapsedMs;

    [ObservableProperty]
    private string _executionSummary = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private ISeries[] _cartesianSeries = [];

    [ObservableProperty]
    private Axis[] _cartesianXAxes = [];

    [ObservableProperty]
    private Axis[] _cartesianYAxes = [];

    [ObservableProperty]
    private LegendPosition _cartesianLegendPosition = LegendPosition.Hidden;

    [ObservableProperty]
    private ISeries[] _pieSeries = [];

    [ObservableProperty]
    private LegendPosition _pieLegendPosition = LegendPosition.Right;

    public bool HasReferenceDateParameter => ActiveReport?.HasReferenceDateParameter == true;
    public bool HasChart => ActiveReport?.HasChart == true;
    public bool IsCartesianChart => ActiveReport?.IsCartesianChart == true;
    public bool IsPieChart => ActiveReport?.IsPieChart == true;

    public override Task InitializeAsync() => InitializeCoreAsync(reportKey: null);

    public override Task InitializeAsync(object? parameter) =>
        InitializeCoreAsync(parameter as string);

    private async Task InitializeCoreAsync(string? reportKey)
    {
        _isInitializing = true;
        try
        {
            var refDate = await _settingsService.GetReferenceDateAsync();
            ReferenceDate = refDate.ToDateTime(TimeOnly.MinValue);

            var target = ReportCatalog.FindByKey(reportKey) ?? ReportCatalog.All[0];
            ActiveReport = target;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ActiveReport ??= ReportCatalog.All[0];
        }
        finally
        {
            _isInitializing = false;
        }

        if (ActiveReport is not null)
        {
            await RunReportAsync();
        }
    }

    partial void OnActiveReportChanged(ReportDefinition? value)
    {
        StatusMessage = null;
        if (!_isInitializing && value is not null)
        {
            _ = RunReportAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunReport))]
    private Task RunReportAsync()
    {
        var report = ActiveReport;
        if (report is null)
        {
            return Task.CompletedTask;
        }

        _runCts?.Cancel();
        _runCts?.Dispose();
        _runCts = new CancellationTokenSource();
        var token = _runCts.Token;

        return RunSafeAsync(async () =>
        {
            StatusMessage = null;
            var sw = Stopwatch.StartNew();

            switch (report.Key)
            {
                case "D1_MonthlyRevenue":
                {
                    var rows = await _reportRepository.GetMonthlyRevenueAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(MonthlyRevenueRow), sw.ElapsedMilliseconds);
                    ApplyCartesian(ChartFactory.CreateMonthlyRevenueChart(rows));
                    break;
                }
                case "D2_EmployeeCategoryRevenue":
                {
                    var rows = await _reportRepository.GetEmployeeCategoryRevenueAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(EmployeeCategoryRevenueRow), sw.ElapsedMilliseconds);
                    ApplyCartesian(ChartFactory.CreateEmployeeCategoryRevenueChart(rows));
                    break;
                }
                case "D3_LateShipments":
                {
                    var rows = await _reportRepository.GetLateShipmentsAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(LateShipmentRow), sw.ElapsedMilliseconds);
                    ApplyCartesian(ChartFactory.CreateLateShipmentsChart(rows));
                    break;
                }
                case "D4_InactiveCustomers":
                {
                    var refDate = ReferenceDate.HasValue
                        ? DateOnly.FromDateTime(ReferenceDate.Value)
                        : await _settingsService.GetReferenceDateAsync(token);
                    var rows = await _reportRepository.GetInactiveCustomersAsync(refDate, token);
                    sw.Stop();
                    ApplyResult(rows, typeof(InactiveCustomerRow), sw.ElapsedMilliseconds);
                    ClearCharts();
                    break;
                }
                case "D5_DiscountImpact":
                {
                    var rows = await _reportRepository.GetDiscountImpactAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(DiscountImpactRow), sw.ElapsedMilliseconds);
                    ApplyCartesian(ChartFactory.CreateDiscountImpactChart(rows));
                    break;
                }
                case "D6_TopProducts":
                {
                    var rows = await _reportRepository.GetTopProductsAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(TopProductRow), sw.ElapsedMilliseconds);
                    ApplyCartesian(ChartFactory.CreateTopProductsChart(rows));
                    break;
                }
                case "D7_TicketsByStatus":
                {
                    var rows = await _reportRepository.GetTicketsByStatusAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(TicketStatusRow), sw.ElapsedMilliseconds);
                    ApplyCartesian(ChartFactory.CreateTicketsByStatusChart(rows));
                    break;
                }
                case "D8_ResolutionTime":
                {
                    var rows = await _reportRepository.GetResolutionTimeAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(ResolutionTimeRow), sw.ElapsedMilliseconds);
                    ApplyCartesian(ChartFactory.CreateResolutionTimeChart(rows));
                    break;
                }
                case "D9_ReturnsByReason":
                {
                    var rows = await _reportRepository.GetReturnsByReasonAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(ReturnReasonRow), sw.ElapsedMilliseconds);
                    ApplyPie(ChartFactory.CreateReturnsByReasonChart(rows));
                    break;
                }
                case "D10_Reorder":
                {
                    var rows = await _reportRepository.GetReorderProductsAsync(token);
                    sw.Stop();
                    ApplyResult(rows, typeof(ReorderRow), sw.ElapsedMilliseconds);
                    ClearCharts();
                    break;
                }
            }
        }, allowConcurrent: true);
    }

    private bool CanRunReport() => ActiveReport is not null;

    [RelayCommand(CanExecute = nameof(CanShowSql))]
    private void ShowSql()
    {
        if (ActiveReport is null)
        {
            return;
        }

        var sqlText = _reportRepository.GetSqlText(ActiveReport.Key);
        _dialogService.ShowSqlDialog($"{ActiveReport.Code} — {ActiveReport.Title}", sqlText);
    }

    private bool CanShowSql() => ActiveReport is not null;

    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private Task ExportCsvAsync() => RunSafeAsync(async () =>
    {
        if (ActiveReport is null || ResultRows is null || _resultItemType is null)
        {
            return;
        }

        var dateStamp = ReferenceDate.HasValue
            ? DateOnly.FromDateTime(ReferenceDate.Value)
            : await _settingsService.GetReferenceDateAsync();

        var defaultFileName = $"{ActiveReport.Code}_{ActiveReport.CsvSlug}_{dateStamp.ToString(AppFormats.IsoDate)}.csv";
        var filePath = _dialogService.ShowSaveCsvDialog(defaultFileName);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        await _csvExportService.ExportAsync(ResultRows, _resultItemType, filePath);
        StatusMessage = $"CSV exported: {System.IO.Path.GetFileName(filePath)}";
    });

    private bool CanExportCsv() =>
        ActiveReport is not null && ResultRows is not null && _resultItemType is not null && IsNotBusy;

    private void ApplyResult<T>(IReadOnlyList<T> rows, Type itemType, long elapsedMs)
    {
        _resultItemType = itemType;
        ResultRows = rows;
        RowCount = rows.Count;
        ElapsedMs = elapsedMs;
        ExecutionSummary = $"{rows.Count} rows · {elapsedMs} ms";
    }

    private void ApplyCartesian(CartesianChartModel model)
    {
        PieSeries = [];
        CartesianSeries = model.Series;
        CartesianXAxes = model.XAxes;
        CartesianYAxes = model.YAxes;
        CartesianLegendPosition = model.LegendPosition;
    }

    private void ApplyPie(PieChartModel model)
    {
        CartesianSeries = [];
        CartesianXAxes = [];
        CartesianYAxes = [];
        PieSeries = model.Series;
        PieLegendPosition = model.LegendPosition;
    }

    private void ClearCharts()
    {
        CartesianSeries = [];
        CartesianXAxes = [];
        CartesianYAxes = [];
        PieSeries = [];
    }
}
