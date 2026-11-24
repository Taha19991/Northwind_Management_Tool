using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Northwind.App.Services.Interfaces;
using Northwind.Core.Enums;
using Northwind.Core.Interfaces;
using Northwind.Core.Models;

namespace Northwind.App.ViewModels;

public sealed partial class CustomersViewModel : ViewModelBase
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISessionService _sessionService;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _historyCts;
    private bool _suppressCustomerIdTransform;

    public CustomersViewModel(ICustomerRepository customerRepository, ISessionService sessionService)
    {
        _customerRepository = customerRepository;
        _sessionService = sessionService;

        ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy) or nameof(IsNewMode) or nameof(ActiveCustomer))
            {
                SaveCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public bool CanEdit => _sessionService.HasAnyRole(UserRole.Manager, UserRole.Sales);

    public bool IsReadOnly => !CanEdit;

    public bool IsCustomerIdReadOnly => !CanEdit || !IsNewMode;

    public ObservableCollection<Customer> Customers { get; } = [];

    public ObservableCollection<Order> OrderHistory { get; } = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveForm))]
    private Customer? _activeCustomer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomerIdReadOnly))]
    [NotifyPropertyChangedFor(nameof(HasActiveForm))]
    private bool _isNewMode;

    [ObservableProperty]
    private decimal _totalOrderRevenue;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoadingHistory;

    public bool HasActiveForm => IsNewMode || ActiveCustomer is not null;

    // Form fields matching varchar(n) lengths in 01_northwind.sql
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Customer ID is required.")]
    [RegularExpression("^[A-Z]{5}$", ErrorMessage = "Customer ID must be exactly 5 uppercase letters (e.g. ALFKI).")]
    private string _customerId = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Company name is required.")]
    [StringLength(40, ErrorMessage = "Company name cannot exceed 40 characters.")]
    private string _companyName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(30, ErrorMessage = "Contact name cannot exceed 30 characters.")]
    private string _contactName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(30, ErrorMessage = "Contact title cannot exceed 30 characters.")]
    private string _contactTitle = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(60, ErrorMessage = "Address cannot exceed 60 characters.")]
    private string _address = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(15, ErrorMessage = "City cannot exceed 15 characters.")]
    private string _city = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(15, ErrorMessage = "Region cannot exceed 15 characters.")]
    private string _region = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(10, ErrorMessage = "Postal code cannot exceed 10 characters.")]
    private string _postalCode = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(15, ErrorMessage = "Country cannot exceed 15 characters.")]
    private string _country = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(24, ErrorMessage = "Phone cannot exceed 24 characters.")]
    private string _phone = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(24, ErrorMessage = "Fax cannot exceed 24 characters.")]
    private string _fax = string.Empty;

    public override Task InitializeAsync() => LoadCustomersAsync(keepCustomerId: null, CancellationToken.None);

    partial void OnSearchTextChanged(string value)
    {
        _ = DebounceSearchAsync();
    }

    partial void OnCustomerIdChanged(string value)
    {
        if (_suppressCustomerIdTransform)
        {
            return;
        }

        var normalized = (value ?? string.Empty).ToUpperInvariant();
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            _suppressCustomerIdTransform = true;
            CustomerId = normalized;
            _suppressCustomerIdTransform = false;
        }
    }

    partial void OnActiveCustomerChanged(Customer? value)
    {
        StatusMessage = null;
        if (value is null)
        {
            if (!IsNewMode)
            {
                ClearFormFields();
                ClearOrderHistory();
            }
            return;
        }

        IsNewMode = false;
        PopulateForm(value);
        _ = LoadOrderHistoryAsync(value.CustomerId);
    }

    private async Task DebounceSearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(300, token);
            var keepId = IsNewMode ? null : ActiveCustomer?.CustomerId;
            await LoadCustomersAsync(keepId, token);
        }
        catch (OperationCanceledException)
        {
            // New character typed; previous search canceled.
        }
    }

    [RelayCommand]
    private Task RefreshAsync() => LoadCustomersAsync(ActiveCustomer?.CustomerId, CancellationToken.None);

    private Task LoadCustomersAsync(string? keepCustomerId, CancellationToken ct) =>
        RunSafeAsync(async () =>
        {
            var items = await _customerRepository.SearchAsync(SearchText, ct);
            ct.ThrowIfCancellationRequested();

            Customers.Clear();
            foreach (var customer in items)
            {
                Customers.Add(customer);
            }

            if (!IsNewMode)
            {
                Customer? match = null;
                if (!string.IsNullOrEmpty(keepCustomerId))
                {
                    foreach (var c in Customers)
                    {
                        if (string.Equals(c.CustomerId, keepCustomerId, StringComparison.OrdinalIgnoreCase))
                        {
                            match = c;
                            break;
                        }
                    }
                }

                ActiveCustomer = match ?? (Customers.Count > 0 ? Customers[0] : null);
            }
        }, allowConcurrent: true);

    private async Task LoadOrderHistoryAsync(string customerId)
    {
        _historyCts?.Cancel();
        _historyCts?.Dispose();
        _historyCts = new CancellationTokenSource();
        var token = _historyCts.Token;

        IsLoadingHistory = true;
        try
        {
            var orders = await _customerRepository.GetOrderHistoryAsync(customerId, token);
            token.ThrowIfCancellationRequested();

            OrderHistory.Clear();
            decimal total = 0m;
            foreach (var order in orders)
            {
                OrderHistory.Add(order);
                total += order.NetTotal;
            }
            TotalOrderRevenue = total;
        }
        catch (OperationCanceledException)
        {
            // Another customer was chosen.
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartNew))]
    private void NewCustomer()
    {
        StatusMessage = null;
        ErrorMessage = null;
        IsNewMode = true;
        ActiveCustomer = null;
        ClearFormFields();
        ClearOrderHistory();
        ValidateAllProperties();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private bool CanStartNew() => CanEdit;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunSafeAsync(async () =>
    {
        StatusMessage = null;
        ValidateAllProperties();
        SaveCommand.NotifyCanExecuteChanged();
        if (HasErrors)
        {
            return;
        }

        var model = new Customer
        {
            CustomerId = CustomerId.Trim().ToUpperInvariant(),
            CompanyName = CompanyName.Trim(),
            ContactName = ContactName,
            ContactTitle = ContactTitle,
            Address = Address,
            City = City,
            Region = Region,
            PostalCode = PostalCode,
            Country = Country,
            Phone = Phone,
            Fax = Fax
        };

        var wasNew = IsNewMode;
        if (wasNew)
        {
            await _customerRepository.InsertAsync(model);
        }
        else
        {
            await _customerRepository.UpdateAsync(model);
        }

        IsNewMode = false;
        var savedId = model.CustomerId;

        var items = await _customerRepository.SearchAsync(SearchText);
        Customers.Clear();
        foreach (var c in items)
        {
            Customers.Add(c);
        }

        Customer? savedItem = null;
        foreach (var c in Customers)
        {
            if (string.Equals(c.CustomerId, savedId, StringComparison.OrdinalIgnoreCase))
            {
                savedItem = c;
                break;
            }
        }

        ActiveCustomer = savedItem ?? await _customerRepository.GetByIdAsync(savedId);
        StatusMessage = wasNew ? "New customer has been saved." : "Customer details have been saved.";
    });

    private bool CanSave() =>
        CanEdit && IsNotBusy && !HasErrors && (IsNewMode || ActiveCustomer is not null);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        StatusMessage = null;
        ErrorMessage = null;

        if (IsNewMode)
        {
            IsNewMode = false;
            if (Customers.Count > 0)
            {
                ActiveCustomer = Customers[0];
            }
            else
            {
                ClearFormFields();
            }
        }
        else if (ActiveCustomer is not null)
        {
            PopulateForm(ActiveCustomer);
        }
    }

    private bool CanCancel() => CanEdit && HasActiveForm;

    private void PopulateForm(Customer c)
    {
        _suppressCustomerIdTransform = true;
        CustomerId = c.CustomerId;
        _suppressCustomerIdTransform = false;

        CompanyName = c.CompanyName;
        ContactName = c.ContactName ?? string.Empty;
        ContactTitle = c.ContactTitle ?? string.Empty;
        Address = c.Address ?? string.Empty;
        City = c.City ?? string.Empty;
        Region = c.Region ?? string.Empty;
        PostalCode = c.PostalCode ?? string.Empty;
        Country = c.Country ?? string.Empty;
        Phone = c.Phone ?? string.Empty;
        Fax = c.Fax ?? string.Empty;

        ClearErrors();
        ValidateAllProperties();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void ClearFormFields()
    {
        _suppressCustomerIdTransform = true;
        CustomerId = string.Empty;
        _suppressCustomerIdTransform = false;

        CompanyName = string.Empty;
        ContactName = string.Empty;
        ContactTitle = string.Empty;
        Address = string.Empty;
        City = string.Empty;
        Region = string.Empty;
        PostalCode = string.Empty;
        Country = string.Empty;
        Phone = string.Empty;
        Fax = string.Empty;

        ClearErrors();
    }

    private void ClearOrderHistory()
    {
        OrderHistory.Clear();
        TotalOrderRevenue = 0m;
    }
}
