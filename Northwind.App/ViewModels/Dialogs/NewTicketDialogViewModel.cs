using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels.Dialogs;

public sealed record TicketCategoryOption(TicketCategory Category, string DisplayName);

public sealed record TicketPriorityOption(TicketPriority Priority, string DisplayName);

public sealed record TicketAssigneeOption(short? EmployeeId, string DisplayName);

public sealed partial class NewTicketDialogViewModel : ViewModelBase
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILookupRepository _lookupRepository;

    public NewTicketDialogViewModel(
        ITicketRepository ticketRepository,
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        ILookupRepository lookupRepository)
    {
        _ticketRepository = ticketRepository;
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _lookupRepository = lookupRepository;

        Categories = Enum.GetValues<TicketCategory>()
            .Select(c => new TicketCategoryOption(c, EnumText.ToDisplay(c)))
            .ToList();
        _selectedCategory = Categories[0];

        Priorities = Enum.GetValues<TicketPriority>()
            .Select(p => new TicketPriorityOption(p, EnumText.ToDisplay(p)))
            .ToList();
        _selectedPriority = Priorities.First(p => p.Priority == TicketPriority.Medium);

        ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public event Action<bool>? CloseRequested;

    public int? CreatedTicketId { get; private set; }

    public IReadOnlyList<TicketCategoryOption> Categories { get; }
    public IReadOnlyList<TicketPriorityOption> Priorities { get; }
    public ObservableCollection<Customer> Customers { get; } = [];
    public ObservableCollection<TicketAssigneeOption> Assignees { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomerMode))]
    private bool _isOrderMode = true;

    public bool IsCustomerMode
    {
        get => !IsOrderMode;
        set => IsOrderMode = !value;
    }

    [ObservableProperty]
    private string _orderIdText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResolvedOrder))]
    private int? _resolvedOrderId;

    [ObservableProperty]
    private string? _resolvedOrderSummary;

    [ObservableProperty]
    private string? _orderLookupError;

    public bool HasResolvedOrder => ResolvedOrderId.HasValue;

    [ObservableProperty]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private TicketCategoryOption _selectedCategory;

    [ObservableProperty]
    private TicketPriorityOption _selectedPriority;

    [ObservableProperty]
    private TicketAssigneeOption? _selectedAssignee;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Subject is required.")]
    [StringLength(120, ErrorMessage = "Subject must be 120 characters or fewer.")]
    private string _subject = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    public override Task InitializeAsync() => RunSafeAsync(async () =>
    {
        var customers = await _customerRepository.SearchAsync(null);
        Customers.Clear();
        foreach (var c in customers)
        {
            Customers.Add(c);
        }

        var employees = await _lookupRepository.GetEmployeesAsync();
        Assignees.Clear();
        Assignees.Add(new TicketAssigneeOption(null, "(Unassigned)"));
        foreach (var emp in employees)
        {
            Assignees.Add(new TicketAssigneeOption(emp.EmployeeId, emp.FullName));
        }

        SelectedAssignee = Assignees[0];
    });

    partial void OnOrderIdTextChanged(string value)
    {
        ResolvedOrderId = null;
        ResolvedOrderSummary = null;
        OrderLookupError = null;
    }

    partial void OnIsOrderModeChanged(bool value)
    {
        ErrorMessage = null;
        OrderLookupError = null;
    }

    [RelayCommand]
    private Task LookupOrderAsync() => RunSafeAsync(async () =>
    {
        await ResolveOrderInternalAsync();
    });

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunSafeAsync(async () =>
    {
        ValidateAllProperties();
        SaveCommand.NotifyCanExecuteChanged();
        if (HasErrors)
        {
            ErrorMessage = "Please fix the validation errors before saving.";
            return;
        }

        int? orderId = null;
        string? customerId = null;

        if (IsOrderMode)
        {
            if (!ResolvedOrderId.HasValue)
            {
                await ResolveOrderInternalAsync();
            }

            if (!ResolvedOrderId.HasValue)
            {
                ErrorMessage = OrderLookupError ?? "Please enter a valid order number.";
                return;
            }

            orderId = ResolvedOrderId.Value;
        }
        else
        {
            if (SelectedCustomer is null)
            {
                ErrorMessage = "Please select a customer.";
                return;
            }

            customerId = SelectedCustomer.CustomerId;
        }

        var ticket = new SupportTicket
        {
            OrderId = orderId,
            CustomerId = customerId,
            AssignedEmployeeId = SelectedAssignee?.EmployeeId,
            Category = SelectedCategory.Category,
            Priority = SelectedPriority.Priority,
            Status = TicketStatus.Open,
            Subject = Subject.Trim(),
            Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
        };

        CreatedTicketId = await _ticketRepository.CreateAsync(ticket);
        CloseRequested?.Invoke(true);
    });

    private bool CanSave() => IsNotBusy;

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    private async Task ResolveOrderInternalAsync()
    {
        OrderLookupError = null;
        ResolvedOrderId = null;
        ResolvedOrderSummary = null;

        if (!int.TryParse(OrderIdText.Trim(), out var orderId) || orderId <= 0)
        {
            OrderLookupError = "Enter a valid positive order number.";
            return;
        }

        var order = await _orderRepository.GetWithLinesAsync(orderId);
        if (order is null)
        {
            OrderLookupError = $"Order #{orderId} was not found.";
            return;
        }

        ResolvedOrderId = order.OrderId;
        var customerLabel = string.IsNullOrWhiteSpace(order.CustomerName)
            ? order.CustomerId
            : $"{order.CustomerName} ({order.CustomerId})";
        ResolvedOrderSummary = $"Order #{order.OrderId} — {customerLabel}";
    }
}
