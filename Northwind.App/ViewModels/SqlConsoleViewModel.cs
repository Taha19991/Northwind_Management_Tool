using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Reports;
using Northwind.App.Services.Interfaces;
using Northwind.App.SqlConsole;
using Northwind.Core.Exceptions;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed record SqlExampleItem(
    string Code,
    string Key,
    string Title,
    bool HasReferenceDateParameter)
{
    public string DisplayName => $"{Code}: {Title}";
}

public sealed record QueryHistoryItem(
    DateTime ExecutedAt,
    string FirstLine,
    string FullSql,
    int RowCount,
    bool Truncated,
    long ElapsedMs)
{
    public string TimeText => ExecutedAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public string RowSummary => Truncated
        ? $"1,000+ rows ({ElapsedMs:N0} ms)"
        : $"{RowCount:N0} {(RowCount == 1 ? "row" : "rows")} ({ElapsedMs:N0} ms)";
}

public sealed class SchemaColumnNode
{
    public SchemaColumnNode(string name, string dataType, bool isPrimaryKey, bool isNullable)
    {
        Name = name;
        DataType = dataType;
        IsPrimaryKey = isPrimaryKey;
        IsNullable = isNullable;
    }

    public string Name { get; }
    public string DataType { get; }
    public bool IsPrimaryKey { get; }
    public bool IsNullable { get; }
    public string InsertText => Name.ToLowerInvariant();
    public string TypeBadge => IsNullable ? DataType : $"{DataType} NOT NULL";
}

public sealed class SchemaObjectNode
{
    public SchemaObjectNode(SchemaObject schemaObject, IReadOnlyList<SchemaColumnNode> columns, bool isExpanded = false)
    {
        Name = schemaObject.Name;
        Kind = schemaObject.Kind;
        Columns = columns;
        IsExpanded = isExpanded;
    }

    public string Name { get; }
    public SchemaObjectKind Kind { get; }
    public bool IsView => Kind == SchemaObjectKind.View;
    public IReadOnlyList<SchemaColumnNode> Columns { get; }
    public bool IsExpanded { get; }
    public string InsertText => Name.ToLowerInvariant();
    public string HeaderSummary => $"{Name} ({Columns.Count})";
}

public sealed class SchemaGroupNode
{
    public SchemaGroupNode(string title, bool isViewGroup, IReadOnlyList<SchemaObjectNode> items)
    {
        Title = title;
        IsViewGroup = isViewGroup;
        Items = items;
    }

    public string Title { get; }
    public bool IsViewGroup { get; }
    public IReadOnlyList<SchemaObjectNode> Items { get; }
    public string HeaderText => $"{Title} ({Items.Count})";
}

public sealed partial class SqlConsoleViewModel : ViewModelBase
{
    public const int MaxGridRows = 1000;
    private const int MaxHistoryItems = 20;

    public const string DefaultStartupText =
        "-- Write a query and press F5. Try: SELECT * FROM customers c WHERE c.country = 'Germany';\n";

    private readonly IQueryConsoleRepository _queryConsoleRepository;
    private readonly IReportRepository _reportRepository;
    private readonly ISettingsService _settingsService;
    private readonly ICsvExportService _csvExportService;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _queryCts;
    private IReadOnlyList<SchemaObject> _allSchema = [];

    public SqlConsoleViewModel(
        IQueryConsoleRepository queryConsoleRepository,
        IReportRepository reportRepository,
        ISettingsService settingsService,
        ISqlCompletionSource completionSource,
        ICsvExportService csvExportService,
        IDialogService dialogService)
    {
        _queryConsoleRepository = queryConsoleRepository;
        _reportRepository = reportRepository;
        _settingsService = settingsService;
        _csvExportService = csvExportService;
        _dialogService = dialogService;

        CompletionEngine = new SqlCompletionEngine(completionSource);

        var examples = new List<SqlExampleItem>
        {
            new("K1", "K1_Kpis", "Dashboard KPIs", HasReferenceDateParameter: false)
        };
        foreach (var def in ReportCatalog.All)
        {
            examples.Add(new SqlExampleItem(def.Code, def.Key, def.Title, def.HasReferenceDateParameter));
        }
        Examples = examples;
    }

    public SqlCompletionEngine CompletionEngine { get; }
    public IReadOnlyList<SchemaObject> AllSchemaObjects => _allSchema;
    public IReadOnlyList<SqlExampleItem> Examples { get; }
    public ObservableCollection<SchemaGroupNode> SchemaGroups { get; } = [];
    public ObservableCollection<QueryHistoryItem> History { get; } = [];

    public event Action<string>? EditorTextReplaceRequested;
    public event Action<int>? ErrorPositionHighlightRequested;

    [ObservableProperty]
    private bool _isSchemaPanelOpen = true;

    [ObservableProperty]
    private string _schemaSearchText = string.Empty;

    [ObservableProperty]
    private string _schemaSummaryText = "Loading schema...";

    [ObservableProperty]
    private bool _isLoadingSchema;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunQuery))]
    [NotifyPropertyChangedFor(nameof(CanExportCsv))]
    [NotifyCanExecuteChangedFor(nameof(CancelQueryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))]
    private bool _isExecuting;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExportCsv))]
    [NotifyPropertyChangedFor(nameof(HasResult))]
    [NotifyCanExecuteChangedFor(nameof(ExportCsvCommand))]
    private DataTable? _resultTable;

    [ObservableProperty]
    private DataView? _resultView;

    [ObservableProperty]
    private string _statusText = "Ready. Press F5 or Ctrl+Enter to run a query.";

    [ObservableProperty]
    private bool _isResultTruncated;

    [ObservableProperty]
    private SqlExampleItem? _selectedExample;

    [ObservableProperty]
    private QueryHistoryItem? _selectedHistoryItem;

    public bool CanRunQuery => !IsExecuting;
    public bool HasResult => ResultTable is not null;
    public bool CanExportCsv => !IsExecuting && ResultTable is { Columns.Count: > 0, Rows.Count: > 0 };

    public override Task InitializeAsync() => RefreshSchemaAsync();

    partial void OnSchemaSearchTextChanged(string value)
    {
        RebuildSchemaTree();
    }

    partial void OnSelectedExampleChanged(SqlExampleItem? value)
    {
        if (value is not null)
        {
            _ = LoadExampleAsync(value);
        }
    }

    partial void OnSelectedHistoryItemChanged(QueryHistoryItem? value)
    {
        if (value is not null)
        {
            EditorTextReplaceRequested?.Invoke(value.FullSql);
            ErrorMessage = null;
            StatusText = $"Loaded query from history ({value.TimeText}).";
            SelectedHistoryItem = null;
        }
    }

    [RelayCommand]
    private void ToggleSchemaPanel()
    {
        IsSchemaPanelOpen = !IsSchemaPanelOpen;
    }

    [RelayCommand]
    private async Task RefreshSchemaAsync()
    {
        IsLoadingSchema = true;
        try
        {
            var schema = await _queryConsoleRepository.GetSchemaAsync();
            _allSchema = schema;

            var tableCount = schema.Count(o => o.Kind == SchemaObjectKind.Table);
            var viewCount = schema.Count(o => o.Kind == SchemaObjectKind.View);
            var columnCount = schema.Sum(o => o.Columns.Count);
            SchemaSummaryText = $"{schema.Count} objects ({tableCount} tables, {viewCount} views) · {columnCount} columns";

            RebuildSchemaTree();
        }
        catch (QueryConsoleException ex)
        {
            SchemaSummaryText = "Schema unavailable";
            ErrorMessage = ex.FormattedError;
        }
        catch (Exception ex)
        {
            SchemaSummaryText = "Schema unavailable";
            ErrorMessage = ErrorTranslator?.Invoke(ex) ?? ex.Message;
        }
        finally
        {
            IsLoadingSchema = false;
        }
    }

    public async Task<string> BuildExampleSqlAsync(SqlExampleItem example)
    {
        var sql = _reportRepository.GetSqlText(example.Key).Trim();
        if (example.HasReferenceDateParameter)
        {
            var referenceDate = await _settingsService.GetReferenceDateAsync();
            var literal = $"DATE '{referenceDate.ToString(AppFormats.IsoDate, CultureInfo.InvariantCulture)}'";
            sql = sql.Replace("@ReferenceDate", literal, StringComparison.OrdinalIgnoreCase);
        }

        return sql;
    }

    private async Task LoadExampleAsync(SqlExampleItem example)
    {
        try
        {
            ErrorMessage = null;
            var sql = await BuildExampleSqlAsync(example);
            EditorTextReplaceRequested?.Invoke(sql);
            StatusText = $"Loaded example {example.Code}: {example.Title}. Press F5 to run.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ErrorTranslator?.Invoke(ex) ?? ex.Message;
        }
        finally
        {
            SelectedExample = null;
        }
    }

    public async Task ExecuteSqlAsync(string sql, int selectionStartOffset = 0)
    {
        if (IsExecuting)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sql))
        {
            ErrorMessage = "Enter a SQL query to run.";
            StatusText = "No query to run.";
            return;
        }

        _queryCts?.Cancel();
        _queryCts?.Dispose();
        var cts = new CancellationTokenSource();
        _queryCts = cts;

        IsExecuting = true;
        ErrorMessage = null;
        IsResultTruncated = false;
        StatusText = "Running query...";

        try
        {
            var result = await _queryConsoleRepository.ExecuteAsync(sql, MaxGridRows, cts.Token);

            ResultTable = result.Table;
            ResultView = result.Table.DefaultView;
            IsResultTruncated = result.Truncated;

            if (!result.Truncated)
            {
                var unit = result.RowCount == 1 ? "row" : "rows";
                StatusText = $"{result.RowCount:N0} {unit} · {result.ElapsedMs:N0} ms";
            }
            else if (result.TotalCountUnknown)
            {
                StatusText = $"Showing first {result.Table.Rows.Count:N0} rows (100,000+ total) · {result.ElapsedMs:N0} ms";
            }
            else
            {
                StatusText = $"Showing first {result.Table.Rows.Count:N0} of {result.RowCount:N0} rows · {result.ElapsedMs:N0} ms";
            }

            AddToHistory(sql, result);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = null;
            StatusText = "Query cancelled.";
        }
        catch (QueryConsoleException ex)
        {
            ErrorMessage = ex.FormattedError;
            StatusText = ex.FormattedError;
            if (ex.Position is { } pos)
            {
                var targetOffset = selectionStartOffset + Math.Max(0, pos - 1);
                ErrorPositionHighlightRequested?.Invoke(targetOffset);
            }
        }
        catch (Exception ex)
        {
            var message = ErrorTranslator?.Invoke(ex) ?? ex.Message;
            ErrorMessage = message;
            StatusText = message;
        }
        finally
        {
            IsExecuting = false;
            if (ReferenceEquals(_queryCts, cts))
            {
                _queryCts = null;
            }
            cts.Dispose();
        }
    }

    [RelayCommand(CanExecute = nameof(IsExecuting))]
    private void CancelQuery()
    {
        try
        {
            _queryCts?.Cancel();
            StatusText = "Cancelling query...";
        }
        catch (ObjectDisposedException)
        {
        }
    }

    [RelayCommand]
    private void ClearConsole()
    {
        if (IsExecuting)
        {
            return;
        }

        EditorTextReplaceRequested?.Invoke(string.Empty);
        ErrorMessage = null;
        ResultTable = null;
        ResultView = null;
        IsResultTruncated = false;
        StatusText = "Cleared.";
    }

    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportCsvAsync()
    {
        if (ResultTable is null || ResultTable.Rows.Count == 0)
        {
            return;
        }

        var defaultName = $"SqlConsole_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var filePath = _dialogService.ShowSaveCsvDialog(defaultName);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            await _csvExportService.ExportDataTableAsync(ResultTable, filePath);
            StatusText = $"Exported {ResultTable.Rows.Count:N0} rows to {System.IO.Path.GetFileName(filePath)}.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"CSV export failed: {ex.Message}";
        }
    }

    private void AddToHistory(string sql, QueryResult result)
    {
        var trimmed = sql.Trim();
        var firstLine = trimmed
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .FirstOrDefault() ?? trimmed;

        if (firstLine.Length > 80)
        {
            firstLine = firstLine[..77] + "...";
        }

        History.Insert(0, new QueryHistoryItem(
            DateTime.Now,
            firstLine,
            trimmed,
            result.RowCount,
            result.Truncated,
            result.ElapsedMs));

        while (History.Count > MaxHistoryItems)
        {
            History.RemoveAt(History.Count - 1);
        }
    }

    private void RebuildSchemaTree()
    {
        var filter = SchemaSearchText.Trim();
        var hasFilter = !string.IsNullOrEmpty(filter);

        var tableNodes = new List<SchemaObjectNode>();
        var viewNodes = new List<SchemaObjectNode>();

        foreach (var obj in _allSchema)
        {
            var objectMatches = !hasFilter || obj.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            var matchingCols = obj.Columns
                .Where(c => !hasFilter || objectMatches || c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select(c => new SchemaColumnNode(c.Name, c.DataType, c.IsPrimaryKey, c.IsNullable))
                .ToList();

            if (!objectMatches && matchingCols.Count == 0)
            {
                continue;
            }

            var node = new SchemaObjectNode(obj, matchingCols, isExpanded: hasFilter);
            if (obj.Kind == SchemaObjectKind.View)
            {
                viewNodes.Add(node);
            }
            else
            {
                tableNodes.Add(node);
            }
        }

        SchemaGroups.Clear();
        SchemaGroups.Add(new SchemaGroupNode("Tables", isViewGroup: false, tableNodes));
        SchemaGroups.Add(new SchemaGroupNode("Views", isViewGroup: true, viewNodes));
    }
}
