using Microsoft.Extensions.DependencyInjection;
using Northwind.App.Services.Interfaces;
using Northwind.App.ViewModels;
using Northwind.Core.Enums;

namespace Northwind.App.Services;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISessionService _sessionService;
    private readonly IDialogService _dialogService;
    private readonly Dictionary<Type, UserRole[]> _rolePolicies = [];

    public NavigationService(
        IServiceProvider serviceProvider,
        ISessionService sessionService,
        IDialogService dialogService)
    {
        _serviceProvider = serviceProvider;
        _sessionService = sessionService;
        _dialogService = dialogService;
    }

    public ViewModelBase? CurrentView { get; private set; }

    public event Action<ViewModelBase>? CurrentViewChanged;

    public void RegisterRoutePolicy<TViewModel>(params UserRole[] allowedRoles)
        where TViewModel : ViewModelBase
    {
        _rolePolicies[typeof(TViewModel)] = allowedRoles;
    }

    public bool CanNavigateTo<TViewModel>() where TViewModel : ViewModelBase =>
        CanNavigateTo(typeof(TViewModel));

    public bool CanNavigateTo(Type viewModelType)
    {
        if (!_sessionService.IsAuthenticated)
        {
            return false;
        }

        if (_rolePolicies.TryGetValue(viewModelType, out var allowedRoles))
        {
            return _sessionService.HasAnyRole(allowedRoles);
        }

        return false;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase =>
        NavigateTo(typeof(TViewModel), parameter: null);

    public void NavigateTo<TViewModel>(object? parameter) where TViewModel : ViewModelBase =>
        NavigateTo(typeof(TViewModel), parameter);

    public void NavigateTo(Type viewModelType) =>
        NavigateTo(viewModelType, parameter: null);

    public void NavigateTo(Type viewModelType, object? parameter)
    {
        if (!typeof(ViewModelBase).IsAssignableFrom(viewModelType))
        {
            throw new ArgumentException(
                $"Type '{viewModelType.Name}' must inherit from ViewModelBase.",
                nameof(viewModelType));
        }

        if (!CanNavigateTo(viewModelType))
        {
            _dialogService.ShowError("You do not have permission to access this screen.", "Access Denied");
            return;
        }

        var viewModel = (ViewModelBase)_serviceProvider.GetRequiredService(viewModelType);
        CurrentView = viewModel;
        CurrentViewChanged?.Invoke(viewModel);

        _ = parameter is null
            ? viewModel.InitializeAsync()
            : viewModel.InitializeAsync(parameter);
    }
}
