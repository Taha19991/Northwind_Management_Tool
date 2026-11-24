using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using Northwind.App.Converters;
using Northwind.App.SqlConsole;
using Northwind.App.ViewModels;

namespace Northwind.App.Views;

public partial class SqlConsoleView : UserControl
{
    private static readonly SqlCellValueConverter CellValueConverter = new();
    private static readonly IsDbNullConverter DbNullConverter = new();
    private static readonly SolidColorBrush NullTextBrush = CreateFrozenBrush(0x94, 0xA3, 0xB8);

    private SqlConsoleViewModel? _viewModel;
    private CompletionWindow? _completionWindow;
    private ErrorHighlightRenderer? _errorRenderer;
    private DispatcherTimer? _errorHighlightTimer;
    private bool _hasInitializedEditorText;

    public SqlConsoleView()
    {
        InitializeComponent();

        LoadSyntaxHighlighting();
        SqlEditor.Text = SqlConsoleViewModel.DefaultStartupText;
        _hasInitializedEditorText = true;

        SqlEditor.TextArea.TextEntered += OnEditorTextEntered;
        SqlEditor.TextArea.PreviewKeyDown += OnEditorPreviewKeyDown;
        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void LoadSyntaxHighlighting()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Northwind.App.SqlConsole.PostgreSQL.xshd";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return;
        }

        using var reader = new XmlTextReader(stream);
        SqlEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        DetachViewModel();

        if (e.NewValue is SqlConsoleViewModel vm)
        {
            _viewModel = vm;
            vm.EditorTextReplaceRequested += OnEditorTextReplaceRequested;
            vm.ErrorPositionHighlightRequested += OnErrorPositionHighlightRequested;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateSchemaColumnWidth(vm.IsSchemaPanelOpen);

            if (!_hasInitializedEditorText)
            {
                SqlEditor.Text = SqlConsoleViewModel.DefaultStartupText;
                _hasInitializedEditorText = true;
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CloseCompletionWindow();
        ClearErrorHighlight();
        DetachViewModel();
    }

    private void DetachViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.EditorTextReplaceRequested -= OnEditorTextReplaceRequested;
        _viewModel.ErrorPositionHighlightRequested -= OnErrorPositionHighlightRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SqlConsoleViewModel.IsSchemaPanelOpen) && _viewModel is not null)
        {
            UpdateSchemaColumnWidth(_viewModel.IsSchemaPanelOpen);
        }
    }

    private void UpdateSchemaColumnWidth(bool isOpen)
    {
        SchemaColumnDefinition.Width = isOpen ? new GridLength(270) : new GridLength(0);
    }

    private void OnEditorTextReplaceRequested(string text)
    {
        CloseCompletionWindow();
        ClearErrorHighlight();
        SqlEditor.Text = text;
        SqlEditor.CaretOffset = text.Length;
        SqlEditor.Focus();
    }

    private void OnErrorPositionHighlightRequested(int offset)
    {
        var doc = SqlEditor.Document;
        if (doc is null || doc.TextLength == 0)
        {
            return;
        }

        var clamped = Math.Clamp(offset, 0, doc.TextLength);
        var start = clamped;
        var end = clamped;

        if (start < doc.TextLength && SqlCompletionEngine.IsIdentifierChar(doc.GetCharAt(start)))
        {
            while (start > 0 && SqlCompletionEngine.IsIdentifierChar(doc.GetCharAt(start - 1)))
            {
                start--;
            }
            while (end < doc.TextLength && SqlCompletionEngine.IsIdentifierChar(doc.GetCharAt(end)))
            {
                end++;
            }
        }
        else if (end < doc.TextLength)
        {
            end = start + 1;
        }
        else if (start > 0)
        {
            start--;
        }

        var length = Math.Max(0, end - start);
        var line = doc.GetLineByOffset(clamped).LineNumber;

        SqlEditor.CaretOffset = clamped;
        SqlEditor.ScrollToLine(line);
        if (length > 0)
        {
            SqlEditor.Select(start, length);
            ShowBriefErrorHighlight(start, length);
        }

        SqlEditor.Focus();
    }

    private void ShowBriefErrorHighlight(int startOffset, int length)
    {
        ClearErrorHighlight();

        _errorRenderer = new ErrorHighlightRenderer(startOffset, length);
        SqlEditor.TextArea.TextView.BackgroundRenderers.Add(_errorRenderer);
        SqlEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);

        _errorHighlightTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _errorHighlightTimer.Tick += (_, _) => ClearErrorHighlight();
        _errorHighlightTimer.Start();
    }

    private void ClearErrorHighlight()
    {
        if (_errorHighlightTimer is not null)
        {
            _errorHighlightTimer.Stop();
            _errorHighlightTimer = null;
        }

        if (_errorRenderer is not null)
        {
            SqlEditor.TextArea.TextView.BackgroundRenderers.Remove(_errorRenderer);
            _errorRenderer = null;
            SqlEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }
    }

    private void OnRunClick(object sender, RoutedEventArgs e)
    {
        _ = ExecuteCurrentEditorSqlAsync();
    }

    private Task ExecuteCurrentEditorSqlAsync()
    {
        if (_viewModel is null || !_viewModel.CanRunQuery)
        {
            return Task.CompletedTask;
        }

        CloseCompletionWindow();
        ClearErrorHighlight();

        var hasSelection = SqlEditor.SelectionLength > 0;
        var sqlToRun = hasSelection ? SqlEditor.SelectedText : SqlEditor.Text;
        var startOffset = hasSelection ? SqlEditor.SelectionStart : 0;

        return _viewModel.ExecuteSqlAsync(sqlToRun, startOffset);
    }

    private void OnEditorPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 || (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
        {
            e.Handled = true;
            _ = ExecuteCurrentEditorSqlAsync();
            return;
        }

        if (e.Key == Key.Space && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowCompletion(forceOpen: true);
        }
    }

    private void OnEditorTextEntered(object sender, TextCompositionEventArgs e)
    {
        if (_viewModel is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var ch = e.Text[0];
        if (ch == '.')
        {
            CloseCompletionWindow();
            ShowCompletion(forceOpen: true);
            return;
        }

        if (_completionWindow is null && (char.IsLetter(ch) || ch == '_'))
        {
            var text = SqlEditor.Text;
            var caret = SqlEditor.CaretOffset;
            var wordLen = 0;
            for (var i = caret - 1; i >= 0 && SqlCompletionEngine.IsIdentifierChar(text[i]); i--)
            {
                wordLen++;
            }

            if (wordLen >= 2)
            {
                ShowCompletion(forceOpen: false);
            }
        }
    }

    private void ShowCompletion(bool forceOpen)
    {
        if (_viewModel is null)
        {
            return;
        }

        var result = _viewModel.CompletionEngine.GetCompletions(
            SqlEditor.Text,
            SqlEditor.CaretOffset,
            _viewModel.AllSchemaObjects);

        if (result.Items.Count == 0)
        {
            CloseCompletionWindow();
            return;
        }

        if (!forceOpen && !string.IsNullOrEmpty(result.Prefix))
        {
            var hasPrefixMatch = result.Items.Any(i =>
                i.Text.StartsWith(result.Prefix, StringComparison.OrdinalIgnoreCase));
            if (!hasPrefixMatch)
            {
                return;
            }
        }

        CloseCompletionWindow();

        var window = new CompletionWindow(SqlEditor.TextArea)
        {
            StartOffset = result.ReplaceStartOffset,
            EndOffset = SqlEditor.CaretOffset,
            Width = 360
        };

        var data = window.CompletionList.CompletionData;
        foreach (var candidate in result.Items)
        {
            data.Add(new SqlCompletionData(candidate));
        }

        if (!string.IsNullOrEmpty(result.Prefix))
        {
            window.CompletionList.SelectItem(result.Prefix);
        }

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_completionWindow, window))
            {
                _completionWindow = null;
            }
        };

        _completionWindow = window;
        window.Show();
    }

    private void CloseCompletionWindow()
    {
        if (_completionWindow is not null)
        {
            _completionWindow.Close();
            _completionWindow = null;
        }
    }

    private void OnSchemaTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var insertText = SchemaTreeView.SelectedItem switch
        {
            SchemaObjectNode objNode => objNode.InsertText,
            SchemaColumnNode colNode => colNode.InsertText,
            _ => null
        };

        if (string.IsNullOrEmpty(insertText))
        {
            return;
        }

        var caret = SqlEditor.CaretOffset;
        SqlEditor.Document.Insert(caret, insertText);
        SqlEditor.CaretOffset = caret + insertText.Length;
        SqlEditor.Focus();
        e.Handled = true;
    }

    private void OnResultsAutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var propName = e.PropertyName;
        var textCol = new DataGridTextColumn
        {
            Header = propName,
            SortMemberPath = propName,
            Binding = new Binding($"[{propName}]")
            {
                Converter = CellValueConverter
            }
        };

        var isNumeric = e.PropertyType == typeof(short)
            || e.PropertyType == typeof(int)
            || e.PropertyType == typeof(long)
            || e.PropertyType == typeof(decimal)
            || e.PropertyType == typeof(float);

        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6, 0, 6, 0)));
        if (isNumeric)
        {
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
        }

        var nullTrigger = new DataTrigger
        {
            Binding = new Binding($"[{propName}]") { Converter = DbNullConverter },
            Value = true
        };
        nullTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, NullTextBrush));
        nullTrigger.Setters.Add(new Setter(TextBlock.FontStyleProperty, FontStyles.Italic));
        style.Triggers.Add(nullTrigger);

        textCol.ElementStyle = style;
        e.Column = textCol;
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private sealed class ErrorHighlightRenderer : IBackgroundRenderer
    {
        private static readonly SolidColorBrush FillBrush = CreateFrozenBrush(0xFF, 0xCD, 0xD2);
        private static readonly Pen BorderPen;

        private readonly TextSegment _segment;

        static ErrorHighlightRenderer()
        {
            var borderBrush = CreateFrozenBrush(0xC6, 0x28, 0x28);
            BorderPen = new Pen(borderBrush, 1.2);
            BorderPen.Freeze();
        }

        public ErrorHighlightRenderer(int startOffset, int length)
        {
            _segment = new TextSegment { StartOffset = startOffset, Length = length };
        }

        public KnownLayer Layer => KnownLayer.Selection;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (_segment.Length <= 0 || !textView.VisualLinesValid)
            {
                return;
            }

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, _segment))
            {
                drawingContext.DrawRoundedRectangle(FillBrush, BorderPen, rect, 2, 2);
            }
        }
    }
}
