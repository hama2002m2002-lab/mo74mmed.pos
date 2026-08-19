using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class DamagedItemsViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;

    #region Form Properties

    private string _searchBarcode = string.Empty;
    public string SearchBarcode
    {
        get => _searchBarcode;
        set => SetProperty(ref _searchBarcode, value);
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    private decimal _quantity = 1.0m;
    public decimal Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    private string _reason = "تالف / كسر";
    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    private string _notes = string.Empty;
    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    private decimal _totalLossValue;
    public decimal TotalLossValue
    {
        get => _totalLossValue;
        set => SetProperty(ref _totalLossValue, value);
    }

    private int _totalDamagedCount;
    public int TotalDamagedCount
    {
        get => _totalDamagedCount;
        set => SetProperty(ref _totalDamagedCount, value);
    }

    public ObservableCollection<DamagedItem> DamagedList { get; } = new();
    public ObservableCollection<Product> ProductsList { get; } = new();

    #endregion

    #region Commands

    public ICommand ScanOrSearchBarcodeCommand { get; }
    public ICommand SaveDamagedRecordCommand { get; }
    public ICommand DeleteDamagedRecordCommand { get; }
    public ICommand BackToMainCommand { get; }

    public event Action? RequestBackToNavigation;
    public event Action? DamagedRecordAdded;

    #endregion

    public DamagedItemsViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);

        ScanOrSearchBarcodeCommand = new AsyncRelayCommand(async () =>
        {
            if (!string.IsNullOrWhiteSpace(SearchBarcode))
            {
                string clean = SearchBarcode.Trim();
                var p = await _productService.GetProductByBarcodeAsync(clean);
                if (p != null)
                {
                    SelectedProduct = p;
                }
                else
                {
                    MessageBox.Show($"الباركود {clean} غير مسجل في المخزن.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        });

        SaveDamagedRecordCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedProduct == null)
            {
                MessageBox.Show("يرجى اختيار أو مسح باركود المادة التالفة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Quantity <= 0)
            {
                MessageBox.Show("يرجى تحديد كمية تالفة صحيحة أكبر من صفر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var product = await _context.Products.FindAsync(SelectedProduct.Id);
                if (product != null)
                {
                    // خصم الكمية التالفة من رصيد المخزن مباشرة
                    product.StockQuantity = Math.Max(0, product.StockQuantity - Quantity);
                    if (product.ItemsPerCarton > 0)
                    {
                        product.CartonsCount = (int)(product.StockQuantity / product.ItemsPerCarton);
                    }
                    product.UpdatedAt = DateTime.UtcNow;

                    var damaged = new DamagedItem
                    {
                        Id = Guid.NewGuid(),
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Barcode = product.Barcode,
                        Quantity = Quantity,
                        UnitCost = product.Cost,
                        Reason = Reason,
                        Notes = Notes,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.DamagedItems.AddAsync(damaged);
                    await _context.SaveChangesAsync();

                    SelectedProduct = null;
                    SearchBarcode = string.Empty;
                    Quantity = 1;
                    Notes = string.Empty;

                    await LoadDataAsync();
                    DamagedRecordAdded?.Invoke();

                    MessageBox.Show($"تم تسجيل إتلاف ({damaged.Quantity} {product.Unit}) من المادة '{product.Name}' وخصمها من المخزن بنجاح.",
                        "تم تسجيل التالف", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ سجل التالف: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        DeleteDamagedRecordCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is DamagedItem item)
            {
                var res = MessageBox.Show($"هل ترغب في حذف سجل الإتلاف للمادة '{item.ProductName}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _context.DamagedItems.Remove(item);
                    await _context.SaveChangesAsync();
                    await LoadDataAsync();
                }
            }
        });

        BackToMainCommand = new RelayCommand(() => RequestBackToNavigation?.Invoke());
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        var list = await _context.DamagedItems.OrderByDescending(d => d.CreatedAt).ToListAsync();
        DamagedList.Clear();
        decimal totalLoss = 0;
        decimal totalCount = 0;

        foreach (var d in list)
        {
            DamagedList.Add(d);
            totalLoss += d.TotalLossAmount;
            totalCount += d.Quantity;
        }

        TotalLossValue = totalLoss;
        TotalDamagedCount = (int)totalCount;

        var prods = await _productService.GetAllProductsListAsync(null);
        ProductsList.Clear();
        foreach (var p in prods) ProductsList.Add(p);
    }
}
