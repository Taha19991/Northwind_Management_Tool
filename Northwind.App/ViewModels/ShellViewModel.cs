using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;

namespace Northwind.App.ViewModels;

public sealed record NavigationMenuItem(string Title, PackIconKind Icon, Type ViewModelType);

public sealed partial class ShellViewModel : ViewModelBase
{
    private static readonly NavigationMenuItem[] AllMenuItems =
    [
        new("Dashboard", PackIconKind.ViewDashboard, typeof(DashboardViewModel)),
        new("Customers", PackIconKind.AccountGroup, typeof(CustomersViewModel)),
        new("Products", PackIconKind.PackageVariantClosed, typeof(ProductsViewModel)),
        new("Orders", PackIconKind.ClipboardList, typeof(OrdersViewModel)),
        new("New Order", PackIconKind.CartPlus, typeof(OrderEditorViewModel)),
        new("Shipping", PackIconKind.TruckDelivery, typeof(ShippingViewModel)),
        new("Support Tickets", PackIconKind.Lifebuoy, typeof(TicketsViewModel)),
        new("Returns", PackIconKind.KeyboardReturn, typeof(ReturnsViewModel)),
        new("Employees", PackIconKind.BadgeAccount, typeof(EmployeesViewModel)),
        new("Reports", PackIconKind.ChartBar, typeof(ReportsViewModel)),
        new("SQL Console", PackIconKind.Console, typeof(SqlConsoleViewModel))
    ];

    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private bool _isSyncingMenu;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private NavigationMenuItem? _activeMenuItem;

    public ObservableCollection<NavigationMenuItem> MenuItems { get; } = [];

    public string CurrentUsername => _sessionService.CurrentUser?.Username ?? "-";

    public string CurrentRoleText => _sessionService.CurrentUser is { } user
        ? EnumText.ToDisplay(user.Role)
        : "-";

    public event Action? LogoutRequested;

    public ShellViewModel(ISessionService sessionService, INavigationService navigationService)
    {
        _sessionService = sessionService;
        _navigationService = navigationService;
        _navigationService.CurrentViewChanged += HandleNavigationChanged;
    }

    public void LoadForCurrentUser()
    {
        MenuItems.Clear();

        foreach (var item in AllMenuItems)
        {
            if (_navigationService.CanNavigateTo(item.ViewModelType))
            {
                MenuItems.Add(item);
            }
        }

        OnPropertyChanged(nameof(CurrentUsername));
        OnPropertyChanged(nameof(CurrentRoleText));

        if (MenuItems.Count > 0)
        {
            ActiveMenuItem = MenuItems[0];
        }
    }

    public void Detach()
    {
        _navigationService.CurrentViewChanged -= HandleNavigationChanged;
    }

    partial void OnActiveMenuItemChanged(NavigationMenuItem? value)
    {
        if (!_isSyncingMenu && value is not null)
        {
            _navigationService.NavigateTo(value.ViewModelType);
        }
    }

    [RelayCommand]
    private void Logout()
    {
        _sessionService.Logout();
        LogoutRequested?.Invoke();
    }

    private void HandleNavigationChanged(ViewModelBase viewModel)
    {
        CurrentView = viewModel;

        var targetType = viewModel.GetType();
        if (ActiveMenuItem?.ViewModelType == targetType)
        {
            return;
        }

        foreach (var item in MenuItems)
        {
            if (item.ViewModelType == targetType)
            {
                _isSyncingMenu = true;
                ActiveMenuItem = item;
                _isSyncingMenu = false;
                break;
            }
        }
    }
}
