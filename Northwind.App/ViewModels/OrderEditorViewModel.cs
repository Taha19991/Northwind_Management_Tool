using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed record ProductOrderOption(Product Product)
{
    public short ProductId => Product.ProductId;
    public decimal UnitPrice => Product.UnitPrice ?? 0m;
    public short UnitsInStock => Product.UnitsInStock ?? 0;

    public string DisplayLabel =>
        $"{Product.ProductName} — ${UnitPrice.ToString("N2", CultureInfo.CurrentCulture)} (Stock: {UnitsInStock})";
}

public sealed record DiscountOption(decimal Rate, string Label);

public sealed partial class OrderEditorViewModel : ViewModelBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly ISessionService _sessionService;
    private readonly IDialogService _dialogService;
    private readonly INavigationService _navigationService;

    public OrderEditorViewModel(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        ILookupRepository lookupRepository,
        ISessionService sessionService,
        IDialogService dialogService,
        INavigationService navigationService)
    {
        _orderRepository = orderRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
        _sessionService = sessionService;
        _dialogService = dialogService;
        _navigationService = navigationService;

        DiscountOptions =
        [
            new(0.00m, "0%"),
            new(0.05m, "5%"),
            new(0.10m, "10%"),
            new(0.15m, "15%"),
            new(0.20m, "20%"),
            new(0.25m, "25%")
        ];
        _activeDiscountOption = DiscountOptions[0];

        OrderLines.CollectionChanged += (_, _) =>
        {
            RecalculateTotals();
            SaveOrderCommand.NotifyCanExecuteChanged();
        };

        ErrorsChanged += (_, _) => SaveOrderCommand.NotifyCanExecuteChanged();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy))
            {
                SaveOrderCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public bool CanEdit => _sessionService.HasAnyRole(UserRole.Manager, UserRole.Sales);

    public bool CanChangeEmployee => _sessionService.HasRole(UserRole.Manager);

    public ObservableCollection<Customer> Customers { get; } = [];
    public ObservableCollection<Employee> Employees { get; } = [];
    public ObservableCollection<Shipper> Shippers { get; } = [];
    public ObservableCollection<ProductOrderOption> AvailableProducts { get; } = [];
    public ObservableCollection<OrderLine> OrderLines { get; } = [];
    public IReadOnlyList<DiscountOption> DiscountOptions { get; }

    // Header fields
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Customer is required.")]
    private Customer? _activeCustomer;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Employee is required.")]
    private short? _employeeId;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Order date is required.")]
    private DateTime? _orderDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _requiredDate = DateTime.Today.AddDays(28);

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Shipper is required.")]
    private short? _shipVia;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(OrderEditorViewModel), nameof(ValidateFreight))]
    private string _freightText = "0.00";

    // Shipping address fields matching 01_northwind.sql limits
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(40, ErrorMessage = "Ship name cannot exceed 40 characters.")]
    private string _shipName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(60, ErrorMessage = "Ship address cannot exceed 60 characters.")]
    private string _shipAddress = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(15, ErrorMessage = "Ship city cannot exceed 15 characters.")]
    private string _shipCity = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(15, ErrorMessage = "Ship region cannot exceed 15 characters.")]
    private string _shipRegion = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    private string _shipPostalCode = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(15, ErrorMessage = "Ship country cannot exceed 15 characters.")]
    private string _shipCountry = string.Empty;

    // New line entry fields
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveProductPriceDisplay))]
    [NotifyPropertyChangedFor(nameof(ActiveProductStockDisplay))]
    private ProductOrderOption? _activeProduct;

    [ObservableProperty]
    private string _lineQuantityText = "1";

    [ObservableProperty]
    private DiscountOption? _activeDiscountOption;

    [ObservableProperty]
    private string? _lineErrorMessage;

    public string ActiveProductPriceDisplay => ActiveProduct is not null
        ? $"${ActiveProduct.UnitPrice.ToString("N2", CultureInfo.CurrentCulture)}"
        : "-";

    public string ActiveProductStockDisplay => ActiveProduct is not null
        ? $"{ActiveProduct.UnitsInStock} in stock"
        : "-";

    // Live totals (all decimal)
    [ObservableProperty]
    private decimal _grossTotal;

    [ObservableProperty]
    private decimal _discountTotal;

    [ObservableProperty]
    private decimal _netTotal;

    public override Task InitializeAsync() => RunSafeAsync(async () =>
    {
        var customers = await _customerRepository.SearchAsync(null);
        var employees = await _lookupRepository.GetEmployeesAsync();
        var shippers = await _lookupRepository.GetShippersAsync();
        var products = await _productRepository.GetAllAsync(null, null);

        Customers.Clear();
        foreach (var c in customers) Customers.Add(c);

        Employees.Clear();
        foreach (var e in employees) Employees.Add(e);

        Shippers.Clear();
        foreach (var s in shippers) Shippers.Add(s);

        AvailableProducts.Clear();
        foreach (var p in products)
        {
            if (!p.Discontinued)
            {
                AvailableProducts.Add(new ProductOrderOption(p));
            }
        }

        OrderLines.Clear();
        OrderDate = DateTime.Today;
        RequiredDate = DateTime.Today.AddDays(28);
        FreightText = 0m.ToString("N2", CultureInfo.CurrentCulture);
        EmployeeId = _sessionService.CurrentUser?.EmployeeId
            ?? (Employees.Count > 0 ? Employees[0].EmployeeId : null);
        ShipVia = Shippers.Count > 0 ? Shippers[0].ShipperId : null;
        ActiveProduct = AvailableProducts.Count > 0 ? AvailableProducts[0] : null;
        ActiveDiscountOption = DiscountOptions[0];
        LineQuantityText = "1";
        LineErrorMessage = null;

        ValidateAllProperties();
        SaveOrderCommand.NotifyCanExecuteChanged();
    });

    partial void OnActiveCustomerChanged(Customer? value)
    {
        if (value is null)
        {
            return;
        }

        ShipName = value.CompanyName;
        ShipAddress = value.Address ?? string.Empty;
        ShipCity = value.City ?? string.Empty;
        ShipRegion = value.Region ?? string.Empty;
        ShipPostalCode = value.PostalCode ?? string.Empty;
        ShipCountry = value.Country ?? string.Empty;
    }

    partial void OnOrderDateChanged(DateTime? value)
    {
        if (value.HasValue)
        {
            RequiredDate = value.Value.AddDays(28);
        }
    }

    [RelayCommand]
    private void AddLine()
    {
        LineErrorMessage = null;

        if (ActiveProduct is null)
        {
            LineErrorMessage = "Please choose a product.";
            return;
        }

        if (!short.TryParse(LineQuantityText?.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var quantity) ||
            quantity <= 0)
        {
            LineErrorMessage = "Quantity must be greater than zero.";
            return;
        }

        foreach (var existing in OrderLines)
        {
            if (existing.ProductId == ActiveProduct.ProductId)
            {
                LineErrorMessage = "This product is already on the order.";
                return;
            }
        }

        var line = new OrderLine
        {
            ProductId = ActiveProduct.ProductId,
            ProductName = ActiveProduct.Product.ProductName,
            UnitPrice = ActiveProduct.UnitPrice,
            Quantity = quantity,
            Discount = ActiveDiscountOption?.Rate ?? 0m
        };

        OrderLines.Add(line);
        LineQuantityText = "1";
        ActiveDiscountOption = DiscountOptions[0];
    }

    [RelayCommand]
    private void RemoveLine(OrderLine? line)
    {
        if (line is not null)
        {
            OrderLines.Remove(line);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveOrder))]
    private Task SaveOrderAsync() => RunSafeAsync(async () =>
    {
        LineErrorMessage = null;
        ValidateAllProperties();
        SaveOrderCommand.NotifyCanExecuteChanged();
        if (HasErrors)
        {
            return;
        }

        if (OrderLines.Count == 0)
        {
            LineErrorMessage = "Add at least one product line to the order.";
            return;
        }

        var order = new Order
        {
            CustomerId = ActiveCustomer!.CustomerId,
            EmployeeId = EmployeeId!.Value,
            OrderDate = DateOnly.FromDateTime(OrderDate!.Value),
            RequiredDate = RequiredDate.HasValue ? DateOnly.FromDateTime(RequiredDate.Value) : null,
            ShipVia = ShipVia!.Value,
            Freight = ParseDecimalOrZero(FreightText),
            ShipName = ShipName,
            ShipAddress = ShipAddress,
            ShipCity = ShipCity,
            ShipRegion = ShipRegion,
            ShipPostalCode = ShipPostalCode,
            ShipCountry = ShipCountry
        };

        var newOrderId = await _orderRepository.CreateOrderAsync(order, OrderLines);
        _dialogService.ShowInfo($"Order {newOrderId} has been saved.", "Order Created");
        _navigationService.NavigateTo<OrdersViewModel>(newOrderId);
    });

    private bool CanSaveOrder() =>
        CanEdit && IsNotBusy && !HasErrors && OrderLines.Count > 0;

    [RelayCommand]
    private void Cancel()
    {
        _navigationService.NavigateTo<OrdersViewModel>();
    }

    private void RecalculateTotals()
    {
        decimal gross = 0m;
        decimal discount = 0m;
        decimal net = 0m;

        foreach (var line in OrderLines)
        {
            gross += line.GrossAmount;
            discount += line.DiscountAmount;
            net += line.NetAmount;
        }

        GrossTotal = Math.Round(gross, 2, MidpointRounding.AwayFromZero);
        DiscountTotal = Math.Round(discount, 2, MidpointRounding.AwayFromZero);
        NetTotal = Math.Round(net, 2, MidpointRounding.AwayFromZero);
    }

    public static ValidationResult? ValidateFreight(string? value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        if (!TryParseDecimal(value, out var parsed))
        {
            return new ValidationResult("Enter a valid freight amount (e.g. 25.50).");
        }

        if (parsed < 0m)
        {
            return new ValidationResult("Freight cannot be negative.");
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
