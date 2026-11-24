using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels.Dialogs;

public sealed partial class ShipOrderDialogViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILookupRepository _lookupRepository;

    public ShipOrderDialogViewModel(IOrderRepository orderRepository, ILookupRepository lookupRepository)
    {
        _orderRepository = orderRepository;
        _lookupRepository = lookupRepository;

        ErrorsChanged += (_, _) => ConfirmCommand.NotifyCanExecuteChanged();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy))
            {
                ConfirmCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public event Action<bool>? CloseRequested;

    public ObservableCollection<Shipper> Shippers { get; } = [];

    [ObservableProperty]
    private int _orderId;

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private DateOnly _orderDate;

    [ObservableProperty]
    private DateOnly? _requiredDate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Shipper is required.")]
    private short? _shipVia;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ShipOrderDialogViewModel), nameof(ValidateFreight))]
    private string _freightText = "0.00";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Shipped date is required.")]
    [CustomValidation(typeof(ShipOrderDialogViewModel), nameof(ValidateShippedDate))]
    private DateTime? _shippedDate = DateTime.Today;

    public void LoadOrder(Order order)
    {
        OrderId = order.OrderId;
        CustomerName = string.IsNullOrWhiteSpace(order.CustomerName)
            ? order.CustomerId
            : $"{order.CustomerName} ({order.CustomerId})";
        OrderDate = order.OrderDate;
        RequiredDate = order.RequiredDate;
        ShipVia = order.ShipVia;
        FreightText = (order.Freight ?? 0m).ToString("N2", CultureInfo.CurrentCulture);
        ShippedDate = DateTime.Today;
        ErrorMessage = null;

        ValidateAllProperties();
        ConfirmCommand.NotifyCanExecuteChanged();
    }

    public override Task InitializeAsync() => RunSafeAsync(async () =>
    {
        var shippers = await _lookupRepository.GetShippersAsync();
        Shippers.Clear();
        foreach (var shipper in shippers)
        {
            Shippers.Add(shipper);
        }

        if (!ShipVia.HasValue && Shippers.Count > 0)
        {
            ShipVia = Shippers[0].ShipperId;
        }

        ValidateAllProperties();
        ConfirmCommand.NotifyCanExecuteChanged();
    });

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private Task ConfirmAsync() => RunSafeAsync(async () =>
    {
        ValidateAllProperties();
        ConfirmCommand.NotifyCanExecuteChanged();
        if (HasErrors)
        {
            return;
        }

        var freight = ParseDecimalOrZero(FreightText);
        var shippedDate = DateOnly.FromDateTime(ShippedDate!.Value);

        await _orderRepository.ShipAsync(OrderId, ShipVia!.Value, freight, shippedDate);
        CloseRequested?.Invoke(true);
    });

    private bool CanConfirm() => IsNotBusy && !HasErrors;

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }

    public static ValidationResult? ValidateFreight(string? value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        if (!TryParseDecimal(value, out var parsed))
        {
            return new ValidationResult("Enter a valid freight amount (e.g. 25.00).");
        }

        if (parsed < 0m)
        {
            return new ValidationResult("Freight cannot be negative.");
        }

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateShippedDate(DateTime? value, ValidationContext context)
    {
        if (!value.HasValue)
        {
            return new ValidationResult("Shipped date is required.");
        }

        if (context.ObjectInstance is ShipOrderDialogViewModel vm &&
            DateOnly.FromDateTime(value.Value) < vm.OrderDate)
        {
            return new ValidationResult("Shipped date cannot be earlier than the order date.");
        }

        return ValidationResult.Success;
    }

    private static bool TryParseDecimal(string raw, out decimal result)
    {
        var trimmed = raw.Trim();
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out result)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static decimal ParseDecimalOrZero(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0m;
        }

        return TryParseDecimal(raw, out var value) ? Math.Round(value, 2) : 0m;
    }
}
