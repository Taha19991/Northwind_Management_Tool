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

public sealed record CategoryFilterOption(short? CategoryId, string Name);

public sealed record SupplierFilterOption(short? SupplierId, string Name);

public sealed partial class ProductsViewModel : ViewModelBase
{
    private readonly IProductRepository _productRepository;
    private readonly ILookupRepository _lookupRepository;
    private readonly ISessionService _sessionService;
    private bool _isInitializing;

    public ProductsViewModel(
        IProductRepository productRepository,
        ILookupRepository lookupRepository,
        ISessionService sessionService)
    {
        _productRepository = productRepository;
        _lookupRepository = lookupRepository;
        _sessionService = sessionService;

        ErrorsChanged += (_, _) => SaveCommand.NotifyCanExecuteChanged();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(IsBusy) or nameof(IsNewMode) or nameof(ActiveProduct))
            {
                SaveCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        };
    }

    public bool CanEdit => _sessionService.HasAnyRole(UserRole.Manager, UserRole.Logistics);

    public bool IsReadOnly => !CanEdit;

    public ObservableCollection<Product> Products { get; } = [];
    public ObservableCollection<CategoryFilterOption> CategoryFilters { get; } = [];
    public ObservableCollection<SupplierFilterOption> SupplierFilters { get; } = [];
    public ObservableCollection<Category> Categories { get; } = [];
    public ObservableCollection<Supplier> Suppliers { get; } = [];

    [ObservableProperty]
    private CategoryFilterOption? _activeCategoryFilter;

    [ObservableProperty]
    private SupplierFilterOption? _activeSupplierFilter;

    [ObservableProperty]
    private bool _onlyLowStock;

    [ObservableProperty]
    private bool _showDiscontinued = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveForm))]
    private Product? _activeProduct;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveForm))]
    [NotifyPropertyChangedFor(nameof(FormHeader))]
    private bool _isNewMode;

    [ObservableProperty]
    private string? _statusMessage;

    public bool HasActiveForm => IsNewMode || ActiveProduct is not null;

    public string FormHeader => IsNewMode ? "New Product" : "Product Details";

    // Form fields matching column limits in 01_northwind.sql / 02_app_extensions.sql
    [ObservableProperty]
    private short _productId;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(40, ErrorMessage = "Product name cannot exceed 40 characters.")]
    private string _productName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Category is required.")]
    private short? _categoryId;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Supplier is required.")]
    private short? _supplierId;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(20, ErrorMessage = "Quantity per unit cannot exceed 20 characters.")]
    private string _quantityPerUnit = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ProductsViewModel), nameof(ValidateUnitPrice))]
    private string _unitPriceText = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ProductsViewModel), nameof(ValidateNonNegativeShort))]
    private string _unitsInStockText = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ProductsViewModel), nameof(ValidateNonNegativeShort))]
    private string _unitsOnOrderText = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ProductsViewModel), nameof(ValidateNonNegativeShort))]
    private string _reorderLevelText = string.Empty;

    [ObservableProperty]
    private bool _discontinued;

    public override Task InitializeAsync() => RunSafeAsync(async () =>
    {
        _isInitializing = true;
        try
        {
            var categories = await _lookupRepository.GetCategoriesAsync();
            var suppliers = await _lookupRepository.GetSuppliersAsync();

            Categories.Clear();
            CategoryFilters.Clear();
            CategoryFilters.Add(new CategoryFilterOption(null, "All"));
            foreach (var category in categories)
            {
                Categories.Add(category);
                CategoryFilters.Add(new CategoryFilterOption(category.CategoryId, category.CategoryName));
            }

            Suppliers.Clear();
            SupplierFilters.Clear();
            SupplierFilters.Add(new SupplierFilterOption(null, "All"));
            foreach (var supplier in suppliers)
            {
                Suppliers.Add(supplier);
                SupplierFilters.Add(new SupplierFilterOption(supplier.SupplierId, supplier.CompanyName));
            }

            ActiveCategoryFilter = CategoryFilters[0];
            ActiveSupplierFilter = SupplierFilters[0];
        }
        finally
        {
            _isInitializing = false;
        }

        await LoadProductsCoreAsync(keepProductId: null);
    });

    partial void OnActiveCategoryFilterChanged(CategoryFilterOption? value)
    {
        if (!_isInitializing)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnActiveSupplierFilterChanged(SupplierFilterOption? value)
    {
        if (!_isInitializing)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnOnlyLowStockChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnShowDiscontinuedChanged(bool value)
    {
        if (!_isInitializing)
        {
            _ = RefreshAsync();
        }
    }

    partial void OnActiveProductChanged(Product? value)
    {
        StatusMessage = null;
        if (value is null)
        {
            if (!IsNewMode)
            {
                ClearFormFields();
            }
            return;
        }

        IsNewMode = false;
        PopulateForm(value);
    }

    [RelayCommand]
    private Task RefreshAsync() =>
        RunSafeAsync(() => LoadProductsCoreAsync(IsNewMode ? null : ActiveProduct?.ProductId), allowConcurrent: true);

    private async Task LoadProductsCoreAsync(short? keepProductId)
    {
        var categoryFilter = ActiveCategoryFilter?.CategoryId;
        var supplierFilter = ActiveSupplierFilter?.SupplierId;

        IReadOnlyList<Product> rows;
        if (OnlyLowStock)
        {
            rows = await _productRepository.GetLowStockAsync();
        }
        else
        {
            rows = await _productRepository.GetAllAsync(categoryFilter, supplierFilter);
        }

        Products.Clear();
        foreach (var product in rows)
        {
            if (OnlyLowStock)
            {
                if (categoryFilter.HasValue && product.CategoryId != categoryFilter.Value)
                {
                    continue;
                }
                if (supplierFilter.HasValue && product.SupplierId != supplierFilter.Value)
                {
                    continue;
                }
            }
            else if (!ShowDiscontinued && product.Discontinued)
            {
                continue;
            }

            Products.Add(product);
        }

        if (!IsNewMode)
        {
            Product? match = null;
            if (keepProductId.HasValue)
            {
                foreach (var p in Products)
                {
                    if (p.ProductId == keepProductId.Value)
                    {
                        match = p;
                        break;
                    }
                }
            }

            ActiveProduct = match ?? (Products.Count > 0 ? Products[0] : null);
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartNew))]
    private void NewProduct()
    {
        StatusMessage = null;
        ErrorMessage = null;
        IsNewMode = true;
        ActiveProduct = null;
        ClearFormFields();
        UnitPriceText = 0m.ToString("N2", CultureInfo.CurrentCulture);
        UnitsInStockText = "0";
        UnitsOnOrderText = "0";
        ReorderLevelText = "0";
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

        var model = new Product
        {
            ProductId = ProductId,
            ProductName = ProductName.Trim(),
            CategoryId = CategoryId!.Value,
            SupplierId = SupplierId!.Value,
            QuantityPerUnit = QuantityPerUnit,
            UnitPrice = ParseOptionalDecimal(UnitPriceText),
            UnitsInStock = ParseOptionalShort(UnitsInStockText),
            UnitsOnOrder = ParseOptionalShort(UnitsOnOrderText),
            ReorderLevel = ParseOptionalShort(ReorderLevelText),
            Discontinued = Discontinued
        };

        var wasNew = IsNewMode;
        short savedId;
        if (wasNew)
        {
            savedId = await _productRepository.InsertAsync(model);
        }
        else
        {
            await _productRepository.UpdateAsync(model);
            savedId = model.ProductId;
        }

        IsNewMode = false;
        await LoadProductsCoreAsync(savedId);
        StatusMessage = wasNew ? "New product has been saved." : "Product details have been saved.";
    });

    private bool CanSave() =>
        CanEdit && IsNotBusy && !HasErrors && (IsNewMode || ActiveProduct is not null);

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        StatusMessage = null;
        ErrorMessage = null;

        if (IsNewMode)
        {
            IsNewMode = false;
            if (Products.Count > 0)
            {
                ActiveProduct = Products[0];
            }
            else
            {
                ClearFormFields();
            }
        }
        else if (ActiveProduct is not null)
        {
            PopulateForm(ActiveProduct);
        }
    }

    private bool CanCancel() => CanEdit && HasActiveForm;

    private void PopulateForm(Product p)
    {
        ProductId = p.ProductId;
        ProductName = p.ProductName;
        CategoryId = p.CategoryId;
        SupplierId = p.SupplierId;
        QuantityPerUnit = p.QuantityPerUnit ?? string.Empty;
        UnitPriceText = p.UnitPrice.HasValue ? p.UnitPrice.Value.ToString("N2", CultureInfo.CurrentCulture) : string.Empty;
        UnitsInStockText = p.UnitsInStock?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        UnitsOnOrderText = p.UnitsOnOrder?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        ReorderLevelText = p.ReorderLevel?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
        Discontinued = p.Discontinued;

        ClearErrors();
        ValidateAllProperties();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void ClearFormFields()
    {
        ProductId = 0;
        ProductName = string.Empty;
        CategoryId = null;
        SupplierId = null;
        QuantityPerUnit = string.Empty;
        UnitPriceText = string.Empty;
        UnitsInStockText = string.Empty;
        UnitsOnOrderText = string.Empty;
        ReorderLevelText = string.Empty;
        Discontinued = false;
        ClearErrors();
    }

    public static ValidationResult? ValidateUnitPrice(string? value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        if (!TryParseDecimal(value, out var parsed))
        {
            return new ValidationResult("Enter a valid unit price (e.g. 18.50).");
        }

        if (parsed < 0m)
        {
            return new ValidationResult("Unit price cannot be negative.");
        }

        if (parsed > 99_999_999.99m)
        {
            return new ValidationResult("Unit price exceeds the allowed limit.");
        }

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateNonNegativeShort(string? value, ValidationContext _)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        if (!short.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var parsed))
        {
            return new ValidationResult("Enter an integer between 0 and 32767.");
        }

        if (parsed < 0)
        {
            return new ValidationResult("Value cannot be negative.");
        }

        return ValidationResult.Success;
    }

    private static bool TryParseDecimal(string raw, out decimal result)
    {
        var trimmed = raw.Trim();
        return decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out result)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }

    private static decimal? ParseOptionalDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return TryParseDecimal(raw, out var value) ? Math.Round(value, 2) : null;
    }

    private static short? ParseOptionalShort(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return short.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var value)
            ? value
            : null;
    }
}
