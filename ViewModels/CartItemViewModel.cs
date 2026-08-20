using System;
using System.Windows.Input;
using HamoPos.Models;

namespace HamoPos.ViewModels;

/// <summary>
/// يمثل عنصراً أو صنفاً في سلة المشتريات الحالية لشاشة الكاشير مع دعم أزرار أنواع البيع وفحص التكلفة
/// </summary>
public class CartItemViewModel : BaseViewModel
{
    private int _itemIndex = 1;
    public int ItemIndex
    {
        get => _itemIndex;
        set => SetProperty(ref _itemIndex, value);
    }

    public Guid? ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = "قطعة";

    // الأسعار المرجعية
    public decimal RetailPrice { get; set; }
    public decimal WholesalePrice { get; set; }
    public decimal CartonPrice { get; set; }
    public decimal PieceCost { get; set; }
    public decimal CartonCost { get; set; }
    public decimal ItemsPerCarton { get; set; } = 1;

    public event Action? SaleTypeChanged;

    // نوعية البيع (مفرد، جملة، كرتون، إرجاع)
    private string _saleType = "مفرد";
    public string SaleType
    {
        get => _saleType;
        set
        {
            if (SetProperty(ref _saleType, value))
            {
                ApplySaleTypePrice();
                OnPropertyChanged(nameof(IsRetail));
                OnPropertyChanged(nameof(IsWholesale));
                OnPropertyChanged(nameof(IsCarton));
                OnPropertyChanged(nameof(IsReturn));
                OnPropertyChanged(nameof(IsBelowCost));
                OnPropertyChanged(nameof(EffectiveCost));
                SaleTypeChanged?.Invoke();
            }
        }
    }

    public bool IsRetail => SaleType == "مفرد";
    public bool IsWholesale => SaleType == "جملة";
    public bool IsCarton => SaleType == "كرتون";
    public bool IsReturn => SaleType == "إرجاع";

    // سعر الشراء / التكلفة الفعلي للصنف الحالي
    public decimal EffectiveCost => SaleType == "كرتون" ? CartonCost : PieceCost;

    // فحص ما إذا كان سعر البيع أقل من التكلفة
    public bool IsBelowCost
    {
        get
        {
            if (IsReturn) return false;
            decimal effectiveCost = EffectiveCost;
            return effectiveCost > 0 && UnitPrice > 0 && UnitPrice < effectiveCost;
        }
    }

    private decimal _unitPrice;
    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (SetProperty(ref _unitPrice, value))
            {
                Recalculate();
                OnPropertyChanged(nameof(IsBelowCost));
            }
        }
    }

    private decimal _quantity = 1.0m;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                Recalculate();
            }
        }
    }

    private decimal _discountAmount = 0.0m;
    public decimal DiscountAmount
    {
        get => _discountAmount;
        set
        {
            if (SetProperty(ref _discountAmount, value))
            {
                Recalculate();
            }
        }
    }

    public decimal TaxRate { get; set; } = 0.0m;

    private decimal _taxAmount;
    public decimal TaxAmount
    {
        get => _taxAmount;
        private set => SetProperty(ref _taxAmount, value);
    }

    private decimal _subTotal;
    public decimal SubTotal
    {
        get => _subTotal;
        private set => SetProperty(ref _subTotal, value);
    }

    private decimal _totalPrice;
    public decimal TotalPrice
    {
        get => _totalPrice;
        private set => SetProperty(ref _totalPrice, value);
    }

    private bool _isNewlyAdded = true;
    public bool IsNewlyAdded
    {
        get => _isNewlyAdded;
        set => SetProperty(ref _isNewlyAdded, value);
    }

    public ICommand SetRetailCommand { get; }
    public ICommand SetWholesaleCommand { get; }
    public ICommand SetCartonCommand { get; }
    public ICommand SetReturnCommand { get; }

    public CartItemViewModel()
    {
        SetRetailCommand = new RelayCommand(() => SaleType = "مفرد");
        SetWholesaleCommand = new RelayCommand(() => SaleType = "جملة");
        SetCartonCommand = new RelayCommand(() => SaleType = "كرتون");
        SetReturnCommand = new RelayCommand(() => SaleType = "إرجاع");
    }

    public CartItemViewModel(Product product, decimal quantity = 1.0m, string saleType = "مفرد") : this()
    {
        ProductId = product.Id;
        Barcode = product.Barcode;
        ProductName = product.Name;
        Unit = product.Unit;
        TaxRate = product.TaxRate;
        Quantity = quantity;

        RetailPrice = product.Price;
        WholesalePrice = product.WholesalePrice;
        CartonPrice = product.CartonSellingPrice;

        PieceCost = product.Cost;
        CartonCost = product.CartonPurchasePrice > 0 ? product.CartonPurchasePrice : (product.Cost * (product.ItemsPerCarton > 0 ? product.ItemsPerCarton : 1));
        ItemsPerCarton = product.ItemsPerCarton > 0 ? product.ItemsPerCarton : 1;

        _saleType = saleType;
        ApplySaleTypePrice();
        _isNewlyAdded = true;
    }

    public void ApplySaleTypePrice()
    {
        UnitPrice = _saleType switch
        {
            "جملة" => WholesalePrice,
            "كرتون" => CartonPrice,
            "إرجاع" => RetailPrice,
            _ => RetailPrice
        };
        Recalculate();
    }

    public void Recalculate()
    {
        decimal gross = Quantity * UnitPrice;
        decimal netAfterDiscount = Math.Max(0, gross - DiscountAmount);
        
        if (_saleType == "إرجاع")
        {
            SubTotal = -gross;
            TaxAmount = 0;
            TotalPrice = -gross;
        }
        else
        {
            SubTotal = netAfterDiscount;
            TaxAmount = netAfterDiscount * TaxRate;
            TotalPrice = SubTotal + TaxAmount;
        }

        OnPropertyChanged(nameof(SubTotal));
        OnPropertyChanged(nameof(TaxAmount));
        OnPropertyChanged(nameof(TotalPrice));
        OnPropertyChanged(nameof(IsBelowCost));
        OnPropertyChanged(nameof(EffectiveCost));
        OnPropertyChanged(nameof(IsReturn));
    }
}
