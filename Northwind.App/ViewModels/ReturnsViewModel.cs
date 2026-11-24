using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed partial class ReturnFilterChip : ObservableObject
{
    public ReturnFilterChip(ReturnStatus? status, string label)
    {
        Status = status;
        Label = label;
    }

    public ReturnStatus? Status { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayText => $"{Label} ({Count})";
}

public sealed partial class ReturnsViewModel : ViewModelBase
{
    private readonly IReturnRepository _returnRepository;
    private readonly ISessionService _sessionService;
    private readonly IDialogService _dialogService;

    public ReturnsViewModel(
        IReturnRepository returnRepository,
        ISessionService sessionService,
        IDialogService dialogService)
    {
        _returnRepository = returnRepository;
        _sessionService = sessionService;
        _dialogService = dialogService;

        FilterChips =
        [
            new(null, "All"),
            new(ReturnStatus.Requested, EnumText.ToDisplay(ReturnStatus.Requested)),
            new(ReturnStatus.Approved, EnumText.ToDisplay(ReturnStatus.Approved)),
            new(ReturnStatus.Refunded, EnumText.ToDisplay(ReturnStatus.Refunded)),
            new(ReturnStatus.Rejected, EnumText.ToDisplay(ReturnStatus.Rejected))
        ];
        FilterChips[0].IsSelected = true;
    }

    public bool CanManageReturns =>
        _sessionService.HasRole(UserRole.Manager) || _sessionService.HasRole(UserRole.Support);

    public bool CanRestock =>
        _sessionService.HasRole(UserRole.Manager) || _sessionService.HasRole(UserRole.Logistics);

    public IReadOnlyList<ReturnFilterChip> FilterChips { get; }
    public ObservableCollection<ProductReturn> Returns { get; } = [];

    [ObservableProperty]
    private ReturnStatus? _activeStatusFilter;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(LinkedTicketDisplay))]
    [NotifyPropertyChangedFor(nameof(CanApprove))]
    [NotifyPropertyChangedFor(nameof(CanReject))]
    [NotifyPropertyChangedFor(nameof(CanMarkRefunded))]
    [NotifyPropertyChangedFor(nameof(CanReturnToStock))]
    [NotifyPropertyChangedFor(nameof(HasAnyAction))]
    [NotifyPropertyChangedFor(nameof(HasNoAction))]
    private ProductReturn? _activeReturn;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasSelection => ActiveReturn is not null;

    public string LinkedTicketDisplay => ActiveReturn?.TicketId is { } ticketId
        ? $"Ticket #{ticketId}"
        : "-";

    public bool CanApprove => CanManageReturns && ActiveReturn?.Status == ReturnStatus.Requested;
    public bool CanReject => CanManageReturns && ActiveReturn?.Status == ReturnStatus.Requested;
    public bool CanMarkRefunded => CanManageReturns && ActiveReturn?.Status == ReturnStatus.Approved;
    public bool CanReturnToStock => CanRestock && ActiveReturn is
    {
        Restocked: false,
        Status: ReturnStatus.Approved or ReturnStatus.Refunded
    };

    public bool HasAnyAction => CanApprove || CanReject || CanMarkRefunded || CanReturnToStock;
    public bool HasNoAction => HasSelection && !HasAnyAction;

    public override Task InitializeAsync() => RunSafeAsync(() => LoadReturnsCoreAsync(focusReturnId: null));

    [RelayCommand]
    private Task SelectFilterAsync(ReturnFilterChip? chip)
    {
        if (chip is null || chip.Status == ActiveStatusFilter)
        {
            return Task.CompletedTask;
        }

        StatusMessage = null;
        SetSelectedChip(chip.Status);
        return RunSafeAsync(() => LoadReturnsCoreAsync(focusReturnId: ActiveReturn?.ReturnId));
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        StatusMessage = null;
        return RunSafeAsync(() => LoadReturnsCoreAsync(focusReturnId: ActiveReturn?.ReturnId));
    }

    [RelayCommand]
    private Task OpenNewReturnAsync()
    {
        if (!CanManageReturns)
        {
            return Task.CompletedTask;
        }

        var createdId = _dialogService.ShowNewReturnDialog();
        if (!createdId.HasValue)
        {
            return Task.CompletedTask;
        }

        StatusMessage = $"Return #{createdId.Value} created.";
        if (ActiveStatusFilter.HasValue && ActiveStatusFilter.Value != ReturnStatus.Requested)
        {
            SetSelectedChip(null);
        }

        return RunSafeAsync(() => LoadReturnsCoreAsync(focusReturnId: createdId.Value));
    }

    [RelayCommand]
    private Task ApproveAsync()
    {
        if (!CanApprove || ActiveReturn is null)
        {
            return Task.CompletedTask;
        }

        var returnId = ActiveReturn.ReturnId;
        return RunSafeAsync(async () =>
        {
            await _returnRepository.ApproveAsync(returnId);
            StatusMessage = $"Return #{returnId} approved.";
            await LoadReturnsCoreAsync(focusReturnId: returnId);
        });
    }

    [RelayCommand]
    private Task RejectAsync()
    {
        if (!CanReject || ActiveReturn is null)
        {
            return Task.CompletedTask;
        }

        var returnId = ActiveReturn.ReturnId;
        if (!_dialogService.Confirm($"Reject return #{returnId}?", "Reject Return"))
        {
            return Task.CompletedTask;
        }

        return RunSafeAsync(async () =>
        {
            await _returnRepository.RejectAsync(returnId);
            StatusMessage = $"Return #{returnId} rejected.";
            await LoadReturnsCoreAsync(focusReturnId: returnId);
        });
    }

    [RelayCommand]
    private Task MarkRefundedAsync()
    {
        if (!CanMarkRefunded || ActiveReturn is null)
        {
            return Task.CompletedTask;
        }

        var returnId = ActiveReturn.ReturnId;
        return RunSafeAsync(async () =>
        {
            await _returnRepository.MarkRefundedAsync(returnId);
            StatusMessage = $"Return #{returnId} marked as refunded.";
            await LoadReturnsCoreAsync(focusReturnId: returnId);
        });
    }

    [RelayCommand]
    private Task ReturnToStockAsync()
    {
        if (!CanReturnToStock || ActiveReturn is null)
        {
            return Task.CompletedTask;
        }

        var item = ActiveReturn;
        if (!_dialogService.Confirm(
            $"Return {item.Quantity} unit(s) of {item.ProductName} to inventory stock?",
            "Return to Stock"))
        {
            return Task.CompletedTask;
        }

        return RunSafeAsync(async () =>
        {
            await _returnRepository.RestockAsync(item.ReturnId);
            StatusMessage = $"Return #{item.ReturnId} restocked ({item.Quantity} unit(s) returned to inventory).";
            await LoadReturnsCoreAsync(focusReturnId: item.ReturnId);
        });
    }

    private async Task LoadReturnsCoreAsync(int? focusReturnId)
    {
        var allReturns = await _returnRepository.ListAsync(null);
        UpdateFilterCounts(allReturns);

        var visible = ActiveStatusFilter.HasValue
            ? await _returnRepository.ListAsync(ActiveStatusFilter.Value)
            : allReturns;

        Returns.Clear();
        foreach (var item in visible)
        {
            Returns.Add(item);
        }

        ProductReturn? toSelect = null;
        if (focusReturnId.HasValue)
        {
            toSelect = Returns.FirstOrDefault(r => r.ReturnId == focusReturnId.Value);
        }

        ActiveReturn = toSelect ?? Returns.FirstOrDefault();
    }

    private void UpdateFilterCounts(IReadOnlyList<ProductReturn> allReturns)
    {
        foreach (var chip in FilterChips)
        {
            chip.Count = chip.Status.HasValue
                ? allReturns.Count(r => r.Status == chip.Status.Value)
                : allReturns.Count;
        }
    }

    private void SetSelectedChip(ReturnStatus? status)
    {
        ActiveStatusFilter = status;
        foreach (var chip in FilterChips)
        {
            chip.IsSelected = chip.Status == status;
        }
    }
}
