using System.Windows;
using System.Windows.Controls;
using Northwind.App.ViewModels;

namespace Northwind.App.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();
    }

    private void OnTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is EmployeesViewModel viewModel && e.NewValue is EmployeeNodeViewModel node)
        {
            viewModel.ActiveEmployee = node;
        }
    }
}
