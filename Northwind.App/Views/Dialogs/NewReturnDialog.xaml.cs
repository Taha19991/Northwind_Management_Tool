using System.Windows;
using Northwind.App.ViewModels.Dialogs;

namespace Northwind.App.Views.Dialogs;

public partial class NewReturnDialog : Window
{
    public NewReturnDialogViewModel ViewModel { get; }

    public NewReturnDialog(NewReturnDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(bool result)
    {
        DialogResult = result;
        Close();
    }
}
