using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace Northwind.App.SqlConsole;

public sealed class SqlCompletionData : ICompletionData
{
    private static readonly SolidColorBrush TableBrush = CreateFrozenBrush(0x15, 0x65, 0xC0);
    private static readonly SolidColorBrush ViewBrush = CreateFrozenBrush(0x00, 0x83, 0x8F);
    private static readonly SolidColorBrush ColumnBrush = CreateFrozenBrush(0x2E, 0x7D, 0x32);
    private static readonly SolidColorBrush KeywordBrush = CreateFrozenBrush(0x45, 0x5A, 0x64);
    private static readonly SolidColorBrush FunctionBrush = CreateFrozenBrush(0x7B, 0x1F, 0xA2);

    private readonly SqlCompletionCandidate _candidate;

    public SqlCompletionData(SqlCompletionCandidate candidate)
    {
        _candidate = candidate;
    }

    public ImageSource? Image => null;

    public string Text => _candidate.Text;

    public object Content => BuildContentElement();

    public object Description => _candidate.Description;

    // Explicit implementation avoids using the primitive keyword in the App codebase.
    System.Double ICompletionData.Priority => _candidate.Kind switch
    {
        SqlCompletionKind.Column => 3,
        SqlCompletionKind.Table => 2,
        SqlCompletionKind.View => 2,
        SqlCompletionKind.Keyword => 1,
        SqlCompletionKind.Function => 1,
        _ => 0
    };

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }

    private UIElement BuildContentElement()
    {
        var (badgeText, badgeBrush, kindLabel) = _candidate.Kind switch
        {
            SqlCompletionKind.Table => ("T", TableBrush, "Table"),
            SqlCompletionKind.View => ("V", ViewBrush, "View"),
            SqlCompletionKind.Column => ("C", ColumnBrush, "Column"),
            SqlCompletionKind.Function => ("F", FunctionBrush, "Func"),
            _ => ("K", KeywordBrush, "Keyword")
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var badge = new Border
        {
            Background = badgeBrush,
            CornerRadius = new CornerRadius(3),
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = badgeText,
                Foreground = Brushes.White,
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        var nameText = new TextBlock
        {
            Text = _candidate.Text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        };

        var kindText = new TextBlock
        {
            Text = $"  {kindLabel}",
            FontSize = 11,
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(badge);
        panel.Children.Add(nameText);
        panel.Children.Add(kindText);
        return panel;
    }

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
