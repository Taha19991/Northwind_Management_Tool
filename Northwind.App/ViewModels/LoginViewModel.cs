using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;

namespace Northwind.App.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public event Action? LoginSucceeded;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    [RelayCommand]
    private Task LoginAsync() => RunSafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your username and password.";
            return;
        }

        var authenticated = await _authService.LoginAsync(Username, Password);
        if (!authenticated)
        {
            ErrorMessage = "Invalid username or password.";
            return;
        }

        LoginSucceeded?.Invoke();
    });
}
