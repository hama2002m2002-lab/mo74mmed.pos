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

public class AuditItemViewModel : BaseViewModel
{
    public Guid ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public decimal Price { get; set; }
    public decimal SystemQuantity { get; set; }

    public Action? OnValueChanged { get; set; }

    private decimal _actualQuantity;
    public decimal ActualQuantity
    {
        get => _actualQuantity;
        set
        {
            if (SetProperty(ref _actualQuantity, value))
            {
                OnPropertyChanged(nameof(Difference));
                OnPropertyChanged(nameof(DifferenceStatus));
                OnPropertyChanged(nameof(DifferenceValue));
                OnPropertyChanged(nameof(DifferenceValueFormatted));
                OnValueChanged?.Invoke();
            }
        }
    }

    public decimal Difference => ActualQuantity - SystemQuantity;

    public decimal DifferenceValue => Math.Abs(Difference) * (Cost > 0 ? Cost : Price);

    public string DifferenceValueFormatted => Difference != 0 ? $"{DifferenceValue:N0} د.ع" : "0 د.ع";

    public string DifferenceStatus => Difference switch
    {
        > 0 => $"+{Difference:N0} (زيادة)",
        < 0 => $"{Difference:N0} (عجز)",
        _ => "مطابق ✔"
    };

    public AuditItemViewModel(Product product, Action? onValueChanged = null)
    {
        ProductId = product.Id;
        Barcode = product.Barcode;
        ProductName = product.Name;
        Cost = product.Cost;
        Price = product.Price;
        SystemQuantity = product.StockQuantity;
        ActualQuantity = product.StockQuantity;
        OnValueChanged = onValueChanged;
    }
}

public class StockAuditViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;

    private string _searchOrBarcode = string.Empty;
    public string SearchOrBarcode
    {
        get => _searchOrBarcode;
        set => SetProperty(ref _searchOrBarcode, value);
    }

    private int _matchedCount;
    public int MatchedCount
    {
        get => _matchedCount;
        set => SetProperty(ref _matchedCount, value);
    }

    private int _discrepanciesCount;
    public int DiscrepanciesCount
    {
        get => _discrepanciesCount;
        set => SetProperty(ref _discrepanciesCount, value);
    }

    public decimal TotalShortageValue => AuditList.Where(i => i.Difference < 0).Sum(i => i.DifferenceValue);
    public decimal TotalSurplusValue => AuditList.Where(i => i.Difference > 0).Sum(i => i.DifferenceValue);
    public decimal NetDifferenceValue => TotalSurplusValue - TotalShortageValue;

    public ObservableCollection<AuditItemViewModel> AuditList { get; } = new();

    public ICommand ScanOrSearchCommand { get; }
    public ICommand ApplyAuditAdjustmentsCommand { get; }
    public ICommand ReloadCommand { get; }

    public StockAuditViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);

        ScanOrSearchCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(SearchOrBarcode))
                return;

            string query = SearchOrBarcode.Trim();
            
            // 1. البحث في القائمة المحملة أولاً
            var existing = AuditList.FirstOrDefault(i => 
                i.Barcode.Equals(query, StringComparison.OrdinalIgnoreCase) || 
                i.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.ActualQuantity += 1;
                SearchOrBarcode = string.Empty;
                RecalculateStats();
                return;
            }

            // 2. إذا لم يكن في القائمة، جلبه من قاعدة البيانات
            var product = await _context.Products.FirstOrDefaultAsync(p => 
                p.Barcode == query || EF.Functions.Like(p.Name, $"%{query}%"));

            if (product != null)
            {
                var newItem = new AuditItemViewModel(product, RecalculateStats);
                newItem.ActualQuantity = product.StockQuantity; // يبدأ بالقيمة الفعلية
                AuditList.Insert(0, newItem);
                SearchOrBarcode = string.Empty;
                RecalculateStats();
            }
            else
            {
                MessageBox.Show($"لم يتم العثور على مادة بالرمز أو الاسم: {query}", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });

        ApplyAuditAdjustmentsCommand = new AsyncRelayCommand(async () =>
        {
            int diffCount = AuditList.Count(i => i.Difference != 0);
            if (diffCount == 0)
            {
                MessageBox.Show("كافة المواد مطابقة للمخزون الفعلي، لا توجد فروقات لاعتمادها.", "مطابقة تامة", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string reportSummary = $"تقرير الفروقات بالجرد:\n" +
                                   $"• عدد المواد ذات الفروقات: {diffCount}\n" +
                                   $"• إجمالي قيمة النقص (العجز): {TotalShortageValue:N0} د.ع\n" +
                                   $"• إجمالي قيمة الزيادة (الفائض): {TotalSurplusValue:N0} د.ع\n" +
                                   $"• صافي الفارق المالي: {NetDifferenceValue:N0} د.ع\n\n" +
                                   $"هل ترغب في اعتماد الكميات الفعلية وتحديث المخزون نهائياً؟";

            var res = MessageBox.Show(reportSummary, "تأكيد اعتماد الجرد المالي والمخزني", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                foreach (var itm in AuditList)
                {
                    var product = await _context.Products.FindAsync(itm.ProductId);
                    if (product != null)
                    {
                        product.StockQuantity = itm.ActualQuantity;
                        if (product.ItemsPerCarton > 0)
                        {
                            product.CartonsCount = (int)(itm.ActualQuantity / product.ItemsPerCarton);
                        }
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await _context.SaveChangesAsync();
                await LoadAllForAuditAsync();
                MessageBox.Show("تم اعتماد وتحديث نتائج الجرد في المخزن بنجاح!", "نجاح الجرد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });

        ReloadCommand = new AsyncRelayCommand(async () => await LoadAllForAuditAsync());
    }

    public async Task InitializeAsync()
    {
        await LoadAllForAuditAsync();
    }

    public async Task LoadAllForAuditAsync()
    {
        var products = await _productService.GetAllProductsListAsync(null);
        AuditList.Clear();
        foreach (var p in products)
        {
            AuditList.Add(new AuditItemViewModel(p, RecalculateStats));
        }
        RecalculateStats();
    }

    public void RecalculateStats()
    {
        MatchedCount = AuditList.Count(i => i.Difference == 0);
        DiscrepanciesCount = AuditList.Count(i => i.Difference != 0);

        OnPropertyChanged(nameof(TotalShortageValue));
        OnPropertyChanged(nameof(TotalSurplusValue));
        OnPropertyChanged(nameof(NetDifferenceValue));
    }
}
