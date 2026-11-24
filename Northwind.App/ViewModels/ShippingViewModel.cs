using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;
using Northwind.Core.Filters;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed record PendingShipmentItem(Order Order, bool IsOverdue);

public sealed record ShipperDelaySummary(string ShipperName, int LateCount);

public sealed partial class ShippingViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ISessionService _sessionService;

    public ShippingViewModel(
        IOrderRepository orderRepository,
        ISettingsService settingsService,
        IDialogService dialogService,
        ISessionService sessionService)
    {
        _orderRepository = orderRepository;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _sessionService = sessionService;
    }

    public bool CanEdit => _sessionService.HasAnyRole(UserRole.Manager, UserRole.Logistics);

    public ObservableCollection<PendingShipmentItem> PendingOrders { get; } = [];
    public ObservableCollection<Order> LateOrders { get; } = [];
    public ObservableCollection<ShipperDelaySummary> ShipperDelays { get; } = [];

    [ObservableProperty]
    private DateOnly _referenceDate;

    [ObservableProperty]
    private string? _statusMessage;

    public override Task InitializeAsync() => RefreshAsync();

    [RelayCommand]
    private Task RefreshAsync() => RunSafeAsync(async () =>
    {
        var refDate = await _settingsService.GetReferenceDateAsync();
        ReferenceDate = refDate;

        var pending = await _orderRepository.ListAsync(new OrderFilter { Status = OrderStatus.Pending });
        var late = await _orderRepository.ListAsync(new OrderFilter { Status = OrderStatus.Late });

        var sortedPending = new List<Order>(pending);
        sortedPending.Sort((a, b) =>
        {
            var da = a.RequiredDate ?? DateOnly.MaxValue;
            var db = b.RequiredDate ?? DateOnly.MaxValue;
            var cmp = da.CompareTo(db);
            return cmp != 0 ? cmp : a.OrderId.CompareTo(b.OrderId);
        });

        PendingOrders.Clear();
        foreach (var order in sortedPending)
        {
            var isOverdue = order.RequiredDate.HasValue && order.RequiredDate.Value < refDate;
            PendingOrders.Add(new PendingShipmentItem(order, isOverdue));
        }

        var sortedLate = new List<Order>(late);
        sortedLate.Sort((a, b) =>
        {
            var delayA = a.DelayDays ?? 0;
            var delayB = b.DelayDays ?? 0;
            var cmp = delayB.CompareTo(delayA);
            if (cmp != 0) return cmp;
            cmp = b.OrderDate.CompareTo(a.OrderDate);
            return cmp != 0 ? cmp : b.OrderId.CompareTo(a.OrderId);
        });

        LateOrders.Clear();
        var countsByShipper = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var order in sortedLate)
        {
            LateOrders.Add(order);
            var shipper = string.IsNullOrWhiteSpace(order.ShipperName) ? "Unknown" : order.ShipperName;
            countsByShipper[shipper] = countsByShipper.TryGetValue(shipper, out var current) ? current + 1 : 1;
        }

        var shipperSummaries = new List<ShipperDelaySummary>(countsByShipper.Count);
        foreach (var pair in countsByShipper)
        {
            shipperSummaries.Add(new ShipperDelaySummary(pair.Key, pair.Value));
        }
        shipperSummaries.Sort((a, b) =>
        {
            var cmp = b.LateCount.CompareTo(a.LateCount);
            return cmp != 0 ? cmp : string.Compare(a.ShipperName, b.ShipperName, StringComparison.CurrentCultureIgnoreCase);
        });

        ShipperDelays.Clear();
        foreach (var summary in shipperSummaries)
        {
            ShipperDelays.Add(summary);
        }
    }, allowConcurrent: true);

    [RelayCommand]
    private async Task ShipOrderAsync(PendingShipmentItem? item)
    {
        if (item is null || !CanEdit)
        {
            return;
        }

        StatusMessage = null;
        if (_dialogService.ShowShipOrderDialog(item.Order))
        {
            await RefreshAsync();
            StatusMessage = $"Order #{item.Order.OrderId} has been shipped.";
        }
    }
}
