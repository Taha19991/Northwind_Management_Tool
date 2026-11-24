using System.Windows;
using Northwind.App.ViewModels;

namespace Northwind.App.Views;

public partial class LoginWindow : Window
{
    public LoginViewModel ViewModel { get; }

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        Loaded += (_, _) => UsernameTextBox.Focus();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Password = PasswordInput.Password;
    }
}
