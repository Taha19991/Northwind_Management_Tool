using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels.Dialogs;

public sealed record ReturnReasonOption(ReturnReason Reason, string DisplayName);

public sealed record LinkedTicketOption(int? TicketId, string DisplayName);

public sealed partial class NewReturnDialogViewModel : ViewModelBase
{
    private readonly IReturnRepository _returnRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ISessionService _sessionService;

    public NewReturnDialogViewModel(
        IReturnRepository returnRepository,
        ITicketRepository ticketRepository,
        ISessionService sessionService)
    {
        _returnRepository = returnRepository;
        _ticketRepository = ticketRepository;
        _sessionService = sessionService;

        Reasons = Enum.GetValues<ReturnReason>()
            .Select(r => new ReturnReasonOption(r, EnumText.ToDisplay(r)))
            .ToList();
        _selectedReason = Reasons[0];

        LinkedTickets.Add(new LinkedTicketOption(null, "(None)"));
        _selectedTicket = LinkedTickets[0];

        ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy) or nameof(SelectedLine) or nameof(LoadedOrderId))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public event Action<bool>? CloseRequested;

    public int? CreatedReturnId { get; private set; }

    public IReadOnlyList<ReturnReasonOption> Reasons { get; }
    public ObservableCollection<ReturnableOrderLine> ReturnableLines { get; } = [];
    public ObservableCollection<LinkedTicketOption> LinkedTickets { get; } = [];

    [ObservableProperty]
    private string _orderIdText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadedOrder))]
    private int? _loadedOrderId;

    [ObservableProperty]
    private string? _orderLookupMessage;

    public bool HasLoadedOrder => LoadedOrderId.HasValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLine))]
    [NotifyPropertyChangedFor(nameof(QuantityHint))]
    [NotifyPropertyChangedFor(nameof(EstimatedRefund))]
    private ReturnableOrderLine? _selectedLine;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(NewReturnDialogViewModel), nameof(ValidateQuantity))]
    [NotifyPropertyChangedFor(nameof(EstimatedRefund))]
    private string _quantityText = "1";

    [ObservableProperty]
    private ReturnReasonOption _selectedReason;

    [ObservableProperty]
    private LinkedTicketOption? _selectedTicket;

    public bool HasSelectedLine => SelectedLine is not null;

    public string QuantityHint => SelectedLine is null
        ? "Quantity *"
        : $"Quantity * (max {SelectedLine.RemainingQuantity})";

    public decimal EstimatedRefund
    {
        get
        {
            if (SelectedLine is null || !short.TryParse(QuantityText.Trim(), out var qty) || qty <= 0)
            {
                return 0m;
            }

            return Math.Round(SelectedLine.UnitPrice * qty * (1m - SelectedLine.Discount), 2);
        }
    }

    partial void OnOrderIdTextChanged(string value)
    {
        LoadedOrderId = null;
        OrderLookupMessage = null;
        ReturnableLines.Clear();
        SelectedLine = null;
        ResetTicketOptions();
    }

    partial void OnSelectedLineChanged(ReturnableOrderLine? value)
    {
        if (value is not null)
        {
            QuantityText = value.RemainingQuantity > 0 ? "1" : "0";
        }

        ValidateProperty(QuantityText, nameof(QuantityText));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task LoadOrderLinesAsync() => RunSafeAsync(async () =>
    {
        ErrorMessage = null;
        OrderLookupMessage = null;
        LoadedOrderId = null;
        ReturnableLines.Clear();
        SelectedLine = null;
        ResetTicketOptions();

        if (!int.TryParse(OrderIdText.Trim(), out var orderId) || orderId <= 0)
        {
            OrderLookupMessage = "Enter a valid positive order number.";
            return;
        }

        var lines = await _returnRepository.GetReturnableLinesAsync(orderId);
        if (lines.Count == 0)
        {
            OrderLookupMessage = $"Order #{orderId} was not found or has no line items.";
            return;
        }

        foreach (var line in lines)
        {
            ReturnableLines.Add(line);
        }

        var orderTickets = await _ticketRepository.ListByOrderAsync(orderId);
        foreach (var ticket in orderTickets)
        {
            LinkedTickets.Add(new LinkedTicketOption(
                ticket.TicketId,
                $"#{ticket.TicketId} — {ticket.Subject} ({ticket.StatusText})"));
        }

        LoadedOrderId = orderId;
        SelectedLine = ReturnableLines.FirstOrDefault(l => l.RemainingQuantity > 0) ?? ReturnableLines[0];
        ValidateProperty(QuantityText, nameof(QuantityText));
        SaveCommand.NotifyCanExecuteChanged();
    });

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunSafeAsync(async () =>
    {
        ValidateAllProperties();
        SaveCommand.NotifyCanExecuteChanged();

        if (!LoadedOrderId.HasValue || SelectedLine is null)
        {
            ErrorMessage = "Load an order and select a product line first.";
            return;
        }

        if (HasErrors)
        {
            ErrorMessage = GetErrors(nameof(QuantityText)).Cast<ValidationResult>().FirstOrDefault()?.ErrorMessage
                ?? "Please fix the validation errors before saving.";
            return;
        }

        var quantity = short.Parse(QuantityText.Trim());
        var productReturn = new ProductReturn
        {
            OrderId = LoadedOrderId.Value,
            ProductId = SelectedLine.ProductId,
            TicketId = SelectedTicket?.TicketId,
            ProcessedBy = _sessionService.CurrentUser?.EmployeeId,
            ReturnDate = DateOnly.FromDateTime(DateTime.Today),
            Quantity = quantity,
            Reason = SelectedReason.Reason,
            Status = ReturnStatus.Requested,
            Restocked = false
        };

        CreatedReturnId = await _returnRepository.CreateAsync(productReturn);
        CloseRequested?.Invoke(true);
    });

    private bool CanSave() => IsNotBusy && LoadedOrderId.HasValue && SelectedLine is not null && !HasErrors;

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    public static ValidationResult? ValidateQuantity(string? value, ValidationContext context)
    {
        if (context.ObjectInstance is not NewReturnDialogViewModel vm || vm.SelectedLine is null)
        {
            return ValidationResult.Success;
        }

        if (vm.SelectedLine.RemainingQuantity <= 0)
        {
            return new ValidationResult("No remaining quantity is available to return for this order line.");
        }

        if (string.IsNullOrWhiteSpace(value) || !short.TryParse(value.Trim(), out var qty) || qty <= 0)
        {
            return new ValidationResult("Quantity must be a positive whole number.");
        }

        if (qty > vm.SelectedLine.RemainingQuantity)
        {
            return new ValidationResult(
                $"Quantity cannot exceed the remaining returnable quantity ({vm.SelectedLine.RemainingQuantity}).");
        }

        return ValidationResult.Success;
    }

    private void ResetTicketOptions()
    {
        LinkedTickets.Clear();
        LinkedTickets.Add(new LinkedTicketOption(null, "(None)"));
        SelectedTicket = LinkedTickets[0];
    }
}
