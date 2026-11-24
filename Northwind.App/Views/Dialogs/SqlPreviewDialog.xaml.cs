using System.Windows;

namespace Northwind.App.Views.Dialogs;

public partial class SqlPreviewDialog : Window
{
    public SqlPreviewDialog(string reportTitle, string sqlText)
    {
        InitializeComponent();
        Title = $"SQL — {reportTitle}";
        TitleTextBlock.Text = reportTitle;
        SqlTextBox.Text = sqlText;
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(SqlTextBox.Text);
        CopiedLabel.Visibility = Visibility.Visible;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
