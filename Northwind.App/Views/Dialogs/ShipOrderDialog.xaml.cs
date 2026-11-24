using System.Windows;
using Northwind.App.ViewModels.Dialogs;

namespace Northwind.App.Views.Dialogs;

public partial class ShipOrderDialog : Window
{
    public ShipOrderDialogViewModel ViewModel { get; }

    public ShipOrderDialog(ShipOrderDialogViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
        Loaded += async (_, _) => await viewModel.InitializeAsync();
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(bool result)
    {
        DialogResult = result;
        Close();
    }
}
