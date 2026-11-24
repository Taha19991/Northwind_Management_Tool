using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;
using Northwind.Core.Filters;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed record CustomerFilterOption(string? CustomerId, string DisplayName);

public sealed record EmployeeFilterOption(short? EmployeeId, string DisplayName);

public sealed record OrderStatusFilterOption(OrderStatus? Status, string DisplayName);

public sealed partial class OrdersViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;
    private CancellationTokenSource? _detailCts;
    private bool _suppressFilterReload;

    public OrdersViewModel(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        ILookupRepository lookupRepository,
        INavigationService navigationService,
        ISessionService sessionService)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _lookupRepository = lookupRepository;
        _navigationService = navigationService;
        _sessionService = sessionService;

        StatusFilters =
        [
            new(null, "All"),
            new(OrderStatus.Pending, EnumText.ToDisplay(OrderStatus.Pending)),
            new(OrderStatus.Shipped, EnumText.ToDisplay(OrderStatus.Shipped)),
            new(OrderStatus.Late, EnumText.ToDisplay(OrderStatus.Late))
        ];
        _activeStatusFilter = StatusFilters[0];
    }

    public bool CanCreateOrder => _sessionService.HasAnyRole(UserRole.Manager, UserRole.Sales);

    public ObservableCollection<Order> Orders { get; } = [];
    public ObservableCollection<CustomerFilterOption> CustomerFilters { get; } = [];
    public ObservableCollection<EmployeeFilterOption> EmployeeFilters { get; } = [];
    public IReadOnlyList<OrderStatusFilterOption> StatusFilters { get; }

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private CustomerFilterOption? _activeCustomerFilter;

    [ObservableProperty]
    private EmployeeFilterOption? _activeEmployeeFilter;

    [ObservableProperty]
    private OrderStatusFilterOption? _activeStatusFilter;

    [ObservableProperty]
    private Order? _activeOrder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    [NotifyPropertyChangedFor(nameof(DetailShipAddressSummary))]
    private Order? _orderDetail;

    [ObservableProperty]
    private decimal _detailGrossTotal;

    [ObservableProperty]
    private decimal _detailDiscountTotal;

    [ObservableProperty]
    private decimal _detailNetTotal;

    [ObservableProperty]
    private bool _isLoadingDetail;

    public bool HasDetail => OrderDetail is not null;

    public string DetailShipAddressSummary
    {
        get
        {
            if (OrderDetail is null)
            {
                return "-";
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(OrderDetail.ShipAddress)) parts.Add(OrderDetail.ShipAddress);
            if (!string.IsNullOrWhiteSpace(OrderDetail.ShipCity)) parts.Add(OrderDetail.ShipCity);
            if (!string.IsNullOrWhiteSpace(OrderDetail.ShipRegion)) parts.Add(OrderDetail.ShipRegion);
            if (!string.IsNullOrWhiteSpace(OrderDetail.ShipPostalCode)) parts.Add(OrderDetail.ShipPostalCode);
            if (!string.IsNullOrWhiteSpace(OrderDetail.ShipCountry)) parts.Add(OrderDetail.ShipCountry);
            return parts.Count > 0 ? string.Join(", ", parts) : "-";
        }
    }

    public override Task InitializeAsync() => InitializeCoreAsync(focusOrderId: null);

    public override Task InitializeAsync(object? parameter) =>
        InitializeCoreAsync(parameter is int orderId ? orderId : null);

    private Task InitializeCoreAsync(int? focusOrderId) => RunSafeAsync(async () =>
    {
        _suppressFilterReload = true;
        try
        {
            var customers = await _customerRepository.SearchAsync(null);
            var employees = await _lookupRepository.GetEmployeesAsync();

            CustomerFilters.Clear();
            CustomerFilters.Add(new CustomerFilterOption(null, "All"));
            foreach (var c in customers)
            {
                CustomerFilters.Add(new CustomerFilterOption(c.CustomerId, $"{c.CompanyName} ({c.CustomerId})"));
            }

            EmployeeFilters.Clear();
            EmployeeFilters.Add(new EmployeeFilterOption(null, "All"));
            foreach (var e in employees)
            {
                EmployeeFilters.Add(new EmployeeFilterOption(e.EmployeeId, e.FullName));
            }

            ActiveCustomerFilter = CustomerFilters[0];
            ActiveEmployeeFilter = EmployeeFilters[0];
            ActiveStatusFilter = StatusFilters[0];
            FromDate = null;
            ToDate = null;
        }
        finally
        {
            _suppressFilterReload = false;
        }

        await LoadOrdersCoreAsync(focusOrderId);
    });

    partial void OnFromDateChanged(DateTime? value) => TriggerFilterReload();
    partial void OnToDateChanged(DateTime? value) => TriggerFilterReload();
    partial void OnActiveCustomerFilterChanged(CustomerFilterOption? value) => TriggerFilterReload();
    partial void OnActiveEmployeeFilterChanged(EmployeeFilterOption? value) => TriggerFilterReload();
    partial void OnActiveStatusFilterChanged(OrderStatusFilterOption? value) => TriggerFilterReload();

    partial void OnActiveOrderChanged(Order? value)
    {
        if (value is null)
        {
            ClearDetail();
            return;
        }

        _ = LoadDetailAsync(value.OrderId);
    }

    private void TriggerFilterReload()
    {
        if (!_suppressFilterReload)
        {
            _ = RefreshAsync();
        }
    }

    [RelayCommand]
    private Task RefreshAsync() =>
        RunSafeAsync(() => LoadOrdersCoreAsync(ActiveOrder?.OrderId), allowConcurrent: true);

    [RelayCommand]
    private Task ClearFiltersAsync()
    {
        _suppressFilterReload = true;
        try
        {
            FromDate = null;
            ToDate = null;
            ActiveCustomerFilter = CustomerFilters.Count > 0 ? CustomerFilters[0] : null;
            ActiveEmployeeFilter = EmployeeFilters.Count > 0 ? EmployeeFilters[0] : null;
            ActiveStatusFilter = StatusFilters[0];
        }
        finally
        {
            _suppressFilterReload = false;
        }

        return RefreshAsync();
    }

    [RelayCommand]
    private void OpenNewOrder()
    {
        _navigationService.NavigateTo<OrderEditorViewModel>();
    }

    private async Task LoadOrdersCoreAsync(int? keepOrderId)
    {
        var filter = new OrderFilter
        {
            CustomerId = ActiveCustomerFilter?.CustomerId,
            EmployeeId = ActiveEmployeeFilter?.EmployeeId,
            Status = ActiveStatusFilter?.Status,
            FromDate = FromDate.HasValue ? DateOnly.FromDateTime(FromDate.Value) : null,
            ToDate = ToDate.HasValue ? DateOnly.FromDateTime(ToDate.Value) : null
        };

        var rows = await _orderRepository.ListAsync(filter);

        Orders.Clear();
        foreach (var order in rows)
        {
            Orders.Add(order);
        }

        Order? match = null;
        if (keepOrderId.HasValue)
        {
            foreach (var o in Orders)
            {
                if (o.OrderId == keepOrderId.Value)
                {
                    match = o;
                    break;
                }
            }
        }

        ActiveOrder = match ?? (Orders.Count > 0 ? Orders[0] : null);
        if (ActiveOrder is null)
        {
            ClearDetail();
        }
    }

    private async Task LoadDetailAsync(int orderId)
    {
        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = new CancellationTokenSource();
        var token = _detailCts.Token;

        IsLoadingDetail = true;
        try
        {
            var detail = await _orderRepository.GetWithLinesAsync(orderId, token);
            token.ThrowIfCancellationRequested();

            OrderDetail = detail;
            if (detail is null)
            {
                DetailGrossTotal = 0m;
                DetailDiscountTotal = 0m;
                DetailNetTotal = 0m;
                return;
            }

            decimal gross = 0m;
            decimal discount = 0m;
            decimal net = 0m;
            foreach (var line in detail.Lines)
            {
                gross += line.GrossAmount;
                discount += line.DiscountAmount;
                net += line.NetAmount;
            }

            DetailGrossTotal = Math.Round(gross, 2, MidpointRounding.AwayFromZero);
            DetailDiscountTotal = Math.Round(discount, 2, MidpointRounding.AwayFromZero);
            DetailNetTotal = Math.Round(net, 2, MidpointRounding.AwayFromZero);
        }
        catch (OperationCanceledException)
        {
            // Another order was chosen.
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingDetail = false;
        }
    }

    private void ClearDetail()
    {
        OrderDetail = null;
        DetailGrossTotal = 0m;
        DetailDiscountTotal = 0m;
        DetailNetTotal = 0m;
    }
}
