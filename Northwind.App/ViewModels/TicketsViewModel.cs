using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;
using Northwind.App.ViewModels.Dialogs;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public enum TicketFilterKind
{
    All,
    Active,
    Open,
    InProgress,
    Resolved,
    Closed
}

public sealed partial class TicketFilterChip : ObservableObject
{
    public TicketFilterChip(TicketFilterKind kind, string label)
    {
        Kind = kind;
        Label = label;
    }

    public TicketFilterKind Kind { get; }
    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayText => $"{Label} ({Count})";
}

public sealed partial class TicketsViewModel : ViewModelBase
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IReturnRepository _returnRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly ISessionService _sessionService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _detailCts;

    public TicketsViewModel(
        ITicketRepository ticketRepository,
        IReturnRepository returnRepository,
        ILookupRepository lookupRepository,
        ISessionService sessionService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _ticketRepository = ticketRepository;
        _returnRepository = returnRepository;
        _lookupRepository = lookupRepository;
        _sessionService = sessionService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        FilterChips =
        [
            new(TicketFilterKind.All, "All"),
            new(TicketFilterKind.Active, "Open & In progress"),
            new(TicketFilterKind.Open, EnumText.ToDisplay(TicketStatus.Open)),
            new(TicketFilterKind.InProgress, EnumText.ToDisplay(TicketStatus.InProgress)),
            new(TicketFilterKind.Resolved, EnumText.ToDisplay(TicketStatus.Resolved)),
            new(TicketFilterKind.Closed, EnumText.ToDisplay(TicketStatus.Closed))
        ];
        FilterChips[0].IsSelected = true;
    }

    public bool CanEdit =>
        _sessionService.HasRole(UserRole.Manager) || _sessionService.HasRole(UserRole.Support);

    public bool CanOpenOrder => _navigationService.CanNavigateTo<OrdersViewModel>();

    public IReadOnlyList<TicketFilterChip> FilterChips { get; }
    public ObservableCollection<SupportTicket> Tickets { get; } = [];
    public ObservableCollection<ProductReturn> RelatedReturns { get; } = [];
    public ObservableCollection<TicketAssigneeOption> Assignees { get; } = [];

    [ObservableProperty]
    private TicketFilterKind _activeFilter = TicketFilterKind.All;

    [ObservableProperty]
    private SupportTicket? _activeTicket;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    [NotifyPropertyChangedFor(nameof(HasOrderLink))]
    [NotifyPropertyChangedFor(nameof(OrderDisplay))]
    [NotifyPropertyChangedFor(nameof(HasResolutionTime))]
    [NotifyPropertyChangedFor(nameof(ResolutionTimeDisplay))]
    [NotifyPropertyChangedFor(nameof(CanStartProgress))]
    [NotifyPropertyChangedFor(nameof(CanResolve))]
    [NotifyPropertyChangedFor(nameof(CanReopen))]
    [NotifyPropertyChangedFor(nameof(CanClose))]
    [NotifyPropertyChangedFor(nameof(IsClosedTicket))]
    private SupportTicket? _ticketDetail;

    [ObservableProperty]
    private TicketAssigneeOption? _selectedDetailAssignee;

    [ObservableProperty]
    private bool _isLoadingDetail;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasRelatedReturns;

    public bool HasDetail => TicketDetail is not null;

    public bool HasOrderLink => CanOpenOrder && TicketDetail?.OrderId is not null;

    public string OrderDisplay => TicketDetail?.OrderId is { } orderId
        ? $"Order #{orderId}"
        : "General (no order)";

    public bool HasResolutionTime => TicketDetail?.ResolutionHours is not null;

    public string ResolutionTimeDisplay => TicketDetail?.ResolutionHours is { } hours
        ? $"{hours:N1} hours"
        : "-";

    public bool CanStartProgress => CanEdit && TicketDetail?.Status == TicketStatus.Open;
    public bool CanResolve => CanEdit && TicketDetail?.Status == TicketStatus.InProgress;
    public bool CanReopen => CanEdit && TicketDetail?.Status == TicketStatus.Resolved;
    public bool CanClose => CanEdit && (TicketDetail?.Status == TicketStatus.Open || TicketDetail?.Status == TicketStatus.Resolved);
    public bool IsClosedTicket => TicketDetail?.Status == TicketStatus.Closed;

    public override Task InitializeAsync() => InitializeCoreAsync(TicketFilterKind.All);

    public override Task InitializeAsync(object? parameter)
    {
        var initialFilter = parameter switch
        {
            string s when string.Equals(s, "Active", StringComparison.OrdinalIgnoreCase) => TicketFilterKind.Active,
            TicketFilterKind kind => kind,
            _ => TicketFilterKind.All
        };

        return InitializeCoreAsync(initialFilter);
    }

    private Task InitializeCoreAsync(TicketFilterKind initialFilter) => RunSafeAsync(async () =>
    {
        SetSelectedChip(initialFilter);

        var employees = await _lookupRepository.GetEmployeesAsync();
        Assignees.Clear();
        Assignees.Add(new TicketAssigneeOption(null, "(Unassigned)"));
        foreach (var emp in employees)
        {
            Assignees.Add(new TicketAssigneeOption(emp.EmployeeId, emp.FullName));
        }

        await LoadTicketsCoreAsync(focusTicketId: null);
    });

    [RelayCommand]
    private Task SelectFilterAsync(TicketFilterChip? chip)
    {
        if (chip is null || chip.Kind == ActiveFilter)
        {
            return Task.CompletedTask;
        }

        StatusMessage = null;
        SetSelectedChip(chip.Kind);
        return RunSafeAsync(() => LoadTicketsCoreAsync(focusTicketId: ActiveTicket?.TicketId));
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        StatusMessage = null;
        return RunSafeAsync(() => LoadTicketsCoreAsync(focusTicketId: ActiveTicket?.TicketId));
    }

    [RelayCommand]
    private Task OpenNewTicketAsync()
    {
        if (!CanEdit)
        {
            return Task.CompletedTask;
        }

        var createdId = _dialogService.ShowNewTicketDialog();
        if (!createdId.HasValue)
        {
            return Task.CompletedTask;
        }

        StatusMessage = $"Ticket #{createdId.Value} created.";
        if (ActiveFilter is TicketFilterKind.InProgress or TicketFilterKind.Resolved or TicketFilterKind.Closed)
        {
            SetSelectedChip(TicketFilterKind.All);
        }

        return RunSafeAsync(() => LoadTicketsCoreAsync(focusTicketId: createdId.Value));
    }

    [RelayCommand]
    private Task SaveAssignmentAsync()
    {
        if (!CanEdit || TicketDetail is null)
        {
            return Task.CompletedTask;
        }

        var ticketId = TicketDetail.TicketId;
        var assigneeId = SelectedDetailAssignee?.EmployeeId;

        return RunSafeAsync(async () =>
        {
            await _ticketRepository.AssignAsync(ticketId, assigneeId);
            StatusMessage = assigneeId.HasValue
                ? $"Ticket #{ticketId} assigned to {SelectedDetailAssignee!.DisplayName}."
                : $"Ticket #{ticketId} unassigned.";
            await LoadTicketsCoreAsync(focusTicketId: ticketId);
        });
    }

    [RelayCommand]
    private Task StartProgressAsync() => ChangeStatusAsync(TicketStatus.InProgress);

    [RelayCommand]
    private Task ResolveTicketAsync() => ChangeStatusAsync(TicketStatus.Resolved);

    [RelayCommand]
    private Task ReopenTicketAsync() => ChangeStatusAsync(TicketStatus.InProgress);

    [RelayCommand]
    private Task CloseTicketAsync() => ChangeStatusAsync(TicketStatus.Closed);

    [RelayCommand]
    private void OpenLinkedOrder()
    {
        if (CanOpenOrder && TicketDetail?.OrderId is { } orderId)
        {
            _navigationService.NavigateTo<OrdersViewModel>(orderId);
        }
    }

    partial void OnActiveTicketChanged(SupportTicket? value)
    {
        _ = LoadDetailAsync(value?.TicketId);
    }

    private Task ChangeStatusAsync(TicketStatus targetStatus)
    {
        if (!CanEdit || TicketDetail is null || !IsTransitionAllowed(TicketDetail.Status, targetStatus))
        {
            return Task.CompletedTask;
        }

        var ticketId = TicketDetail.TicketId;

        return RunSafeAsync(async () =>
        {
            await _ticketRepository.ChangeStatusAsync(ticketId, targetStatus);
            StatusMessage = $"Ticket #{ticketId} moved to {EnumText.ToDisplay(targetStatus)}.";
            await LoadTicketsCoreAsync(focusTicketId: ticketId);
        });
    }

    private static bool IsTransitionAllowed(TicketStatus current, TicketStatus target) => (current, target) switch
    {
        (TicketStatus.Open, TicketStatus.InProgress) => true,
        (TicketStatus.Open, TicketStatus.Closed) => true,
        (TicketStatus.InProgress, TicketStatus.Resolved) => true,
        (TicketStatus.Resolved, TicketStatus.Closed) => true,
        (TicketStatus.Resolved, TicketStatus.InProgress) => true,
        _ => false
    };

    private async Task LoadTicketsCoreAsync(int? focusTicketId)
    {
        var allTickets = await _ticketRepository.ListAsync(null);
        UpdateFilterCounts(allTickets);

        IReadOnlyList<SupportTicket> visible = ActiveFilter switch
        {
            TicketFilterKind.All => allTickets,
            TicketFilterKind.Active => allTickets
                .Where(t => t.Status is TicketStatus.Open or TicketStatus.InProgress)
                .ToList(),
            TicketFilterKind.Open => await _ticketRepository.ListAsync(TicketStatus.Open),
            TicketFilterKind.InProgress => await _ticketRepository.ListAsync(TicketStatus.InProgress),
            TicketFilterKind.Resolved => await _ticketRepository.ListAsync(TicketStatus.Resolved),
            TicketFilterKind.Closed => await _ticketRepository.ListAsync(TicketStatus.Closed),
            _ => allTickets
        };

        Tickets.Clear();
        foreach (var item in visible)
        {
            Tickets.Add(item);
        }

        SupportTicket? toSelect = null;
        if (focusTicketId.HasValue)
        {
            toSelect = Tickets.FirstOrDefault(t => t.TicketId == focusTicketId.Value);
        }

        toSelect ??= Tickets.FirstOrDefault();

        if (ReferenceEquals(ActiveTicket, toSelect))
        {
            await LoadDetailAsync(toSelect?.TicketId);
        }
        else
        {
            ActiveTicket = toSelect;
        }
    }

    private async Task LoadDetailAsync(int? ticketId)
    {
        _detailCts?.Cancel();
        _detailCts?.Dispose();
        _detailCts = null;

        if (!ticketId.HasValue)
        {
            TicketDetail = null;
            RelatedReturns.Clear();
            HasRelatedReturns = false;
            SelectedDetailAssignee = Assignees.FirstOrDefault();
            return;
        }

        var cts = new CancellationTokenSource();
        _detailCts = cts;
        IsLoadingDetail = true;

        try
        {
            var detail = await _ticketRepository.GetByIdAsync(ticketId.Value, cts.Token);
            var returns = await _returnRepository.ListByTicketAsync(ticketId.Value, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            TicketDetail = detail;
            RelatedReturns.Clear();
            foreach (var r in returns)
            {
                RelatedReturns.Add(r);
            }
            HasRelatedReturns = RelatedReturns.Count > 0;

            SelectedDetailAssignee = detail?.AssignedEmployeeId is { } empId
                ? Assignees.FirstOrDefault(a => a.EmployeeId == empId) ?? Assignees.FirstOrDefault()
                : Assignees.FirstOrDefault();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = ErrorTranslator?.Invoke(ex) ?? ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_detailCts, cts))
            {
                IsLoadingDetail = false;
            }
        }
    }

    private void UpdateFilterCounts(IReadOnlyList<SupportTicket> allTickets)
    {
        var openCount = allTickets.Count(t => t.Status == TicketStatus.Open);
        var inProgressCount = allTickets.Count(t => t.Status == TicketStatus.InProgress);
        var resolvedCount = allTickets.Count(t => t.Status == TicketStatus.Resolved);
        var closedCount = allTickets.Count(t => t.Status == TicketStatus.Closed);

        foreach (var chip in FilterChips)
        {
            chip.Count = chip.Kind switch
            {
                TicketFilterKind.All => allTickets.Count,
                TicketFilterKind.Active => openCount + inProgressCount,
                TicketFilterKind.Open => openCount,
                TicketFilterKind.InProgress => inProgressCount,
                TicketFilterKind.Resolved => resolvedCount,
                TicketFilterKind.Closed => closedCount,
                _ => 0
            };
        }
    }

    private void SetSelectedChip(TicketFilterKind kind)
    {
        ActiveFilter = kind;
        foreach (var chip in FilterChips)
        {
            chip.IsSelected = chip.Kind == kind;
        }
    }
}
