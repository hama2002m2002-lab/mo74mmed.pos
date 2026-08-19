using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace HamoPos.ViewModels;

/// <summary>
/// يمثل نافذة أو تبويب فاتورة بيع مستقلة تتيح تعدد الفواتير للكاشير مع معالجة ذكية للخصم
/// </summary>
public class InvoiceTabViewModel : BaseViewModel
{
    private int _tabIndex = 1;
    public int TabIndex
    {
        get => _tabIndex;
        set
        {
            if (SetProperty(ref _tabIndex, value))
            {
                Title = $"فاتورة #{value}";
            }
        }
    }

    private string _title = "فاتورة #1";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public ObservableCollection<CartItemViewModel> CartItems { get; } = new();

    private decimal _cartSubTotal;
    public decimal CartSubTotal
    {
        get => _cartSubTotal;
        private set => SetProperty(ref _cartSubTotal, value);
    }

    private decimal _cartTaxTotal;
    public decimal CartTaxTotal
    {
        get => _cartTaxTotal;
        private set => SetProperty(ref _cartTaxTotal, value);
    }

    private decimal _cartDiscountTotal = 0;
    public decimal CartDiscountTotal
    {
        get => _cartDiscountTotal;
        set
        {
            if (SetProperty(ref _cartDiscountTotal, value))
            {
                RecalculateTotals();
            }
        }
    }

    private string _discountInputText = string.Empty;
    public string DiscountInputText
    {
        get => _discountInputText;
        set
        {
            if (SetProperty(ref _discountInputText, value))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _cartDiscountTotal = 0;
                }
                else if (decimal.TryParse(value.Trim(), out decimal parsedVal) && parsedVal >= 0)
                {
                    _cartDiscountTotal = parsedVal;
                }
                else
                {
                    _cartDiscountTotal = 0;
                }
                OnPropertyChanged(nameof(CartDiscountTotal));
                RecalculateTotals();
            }
        }
    }

    private decimal _cartGrandTotal;
    public decimal CartGrandTotal
    {
        get => _cartGrandTotal;
        private set => SetProperty(ref _cartGrandTotal, value);
    }

    // عدد الأنواع (الأصناف المختلفة)
    private int _distinctItemsCount;
    public int DistinctItemsCount
    {
        get => _distinctItemsCount;
        private set => SetProperty(ref _distinctItemsCount, value);
    }

    // إجمالي عدد المواد والقطع
    private decimal _totalPiecesCount;
    public decimal TotalPiecesCount
    {
        get => _totalPiecesCount;
        private set => SetProperty(ref _totalPiecesCount, value);
    }

    public InvoiceTabViewModel(int tabIndex = 1)
    {
        TabIndex = tabIndex;
    }

    public void RecalculateTotals()
    {
        decimal subTotal = 0;
        decimal taxTotal = 0;
        decimal piecesCount = 0;

        for (int i = 0; i < CartItems.Count; i++)
        {
            var item = CartItems[i];
            item.ItemIndex = i + 1;
            item.Recalculate();
            subTotal += item.SubTotal;
            taxTotal += item.TaxAmount;
            piecesCount += item.Quantity;
        }

        CartSubTotal = subTotal;
        CartTaxTotal = taxTotal;
        DistinctItemsCount = CartItems.Count;
        TotalPiecesCount = piecesCount;
        CartGrandTotal = (subTotal + taxTotal) - CartDiscountTotal;

        OnPropertyChanged(nameof(CartSubTotal));
        OnPropertyChanged(nameof(CartTaxTotal));
        OnPropertyChanged(nameof(CartGrandTotal));
        OnPropertyChanged(nameof(DistinctItemsCount));
        OnPropertyChanged(nameof(TotalPiecesCount));
    }
}
