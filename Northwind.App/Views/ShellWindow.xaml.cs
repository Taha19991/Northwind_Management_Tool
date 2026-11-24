using System.Windows;
using Northwind.App.ViewModels;

namespace Northwind.App.Views;

public partial class ShellWindow : Window
{
    public ShellViewModel ViewModel { get; }

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) => ViewModel.LoadForCurrentUser();
        Closed += (_, _) => ViewModel.Detach();
    }
}
