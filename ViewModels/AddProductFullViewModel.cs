using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class AddProductFullViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;

    #region Properties

    public Guid ProductId { get; set; } = Guid.Empty;

    public bool IsEditMode => ProductId != Guid.Empty;
    public string FormHeaderTitle => IsEditMode ? Loc["Add_EditTitle"] : Loc["Add_Title"];

    // 1. الباركود
    private string _barcode = string.Empty;
    public string Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    // 2. اسم المادة
    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    // 3. المندوب وتفاصيل التوريد
    private string _supplierName = string.Empty;
    public string SupplierName
    {
        get => _supplierName;
        set => SetProperty(ref _supplierName, value);
    }

    private string _supplierPhone = string.Empty;
    public string SupplierPhone
    {
        get => _supplierPhone;
        set => SetProperty(ref _supplierPhone, value);
    }

    private string _supplierCompany = string.Empty;
    public string SupplierCompany
    {
        get => _supplierCompany;
        set => SetProperty(ref _supplierCompany, value);
    }

    private string _supplierNotes = string.Empty;
    public string SupplierNotes
    {
        get => _supplierNotes;
        set => SetProperty(ref _supplierNotes, value);
    }

    private decimal _supplierBalance;
    public decimal SupplierBalance
    {
        get => _supplierBalance;
        set => SetProperty(ref _supplierBalance, value);
    }

    // 4. صنف المادة
    private Category? _selectedCategory;
    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                if (value != null)
                {
                    _categoryText = value.Name;
                    OnPropertyChanged(nameof(CategoryText));
                }
            }
        }
    }

    private string _categoryText = string.Empty;
    public string CategoryText
    {
        get => _categoryText;
        set
        {
            if (SetProperty(ref _categoryText, value))
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var match = Categories.FirstOrDefault(c => c.Name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match != null && match != SelectedCategory)
                    {
                        _selectedCategory = match;
                        OnPropertyChanged(nameof(SelectedCategory));
                    }
                }
            }
        }
    }

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Supplier> SuppliersList { get; } = new();

    // إضافة صنف جديد
    private bool _isAddCategoryModalOpen;
    public bool IsAddCategoryModalOpen
    {
        get => _isAddCategoryModalOpen;
        set => SetProperty(ref _isAddCategoryModalOpen, value);
    }

    private string _newCategoryName = string.Empty;
    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    // 5. حقول الكرتون والكميات
    private decimal _cartonsCount;
    public decimal CartonsCount
    {
        get => _cartonsCount;
        set
        {
            if (SetProperty(ref _cartonsCount, value))
            {
                RecalculateTotals();
            }
        }
    }

    private decimal _itemsPerCarton = 1.0m;
    public decimal ItemsPerCarton
    {
        get => _itemsPerCarton;
        set
        {
            if (SetProperty(ref _itemsPerCarton, value))
            {
                RecalculateTotals();
            }
        }
    }

    // قطع إضافية (مفردة - اختياري)
    private decimal _extraPiecesCount;
    public decimal ExtraPiecesCount
    {
        get => _extraPiecesCount;
        set
        {
            if (SetProperty(ref _extraPiecesCount, value))
            {
                RecalculateTotals();
            }
        }
    }

    private decimal _stockQuantity;
    public decimal StockQuantity
    {
        get => _stockQuantity;
        set => SetProperty(ref _stockQuantity, value);
    }

    private decimal _minStockAlert = 5.0m;
    public decimal MinStockAlert
    {
        get => _minStockAlert;
        set => SetProperty(ref _minStockAlert, value);
    }

    private string _unit = "قطعة";
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    // 6. حقول سعر الشراء والتكلفة
    private decimal _cartonPurchasePrice;
    public decimal CartonPurchasePrice
    {
        get => _cartonPurchasePrice;
        set
        {
            if (SetProperty(ref _cartonPurchasePrice, value))
            {
                RecalculateTotals();
            }
        }
    }

    private decimal _cost;
    public decimal Cost
    {
        get => _cost;
        set
        {
            if (SetProperty(ref _cost, value))
            {
                RecalculateProfits();
            }
        }
    }

    // 7. أسعار البيع
    private decimal _price;
    public decimal Price
    {
        get => _price;
        set
        {
            if (SetProperty(ref _price, value))
            {
                RecalculateProfits();
            }
        }
    }

    private decimal _wholesalePrice;
    public decimal WholesalePrice
    {
        get => _wholesalePrice;
        set
        {
            if (SetProperty(ref _wholesalePrice, value))
            {
                RecalculateProfits();
            }
        }
    }

    private decimal _cartonSellingPrice;
    public decimal CartonSellingPrice
    {
        get => _cartonSellingPrice;
        set
        {
            if (SetProperty(ref _cartonSellingPrice, value))
            {
                RecalculateProfits();
            }
        }
    }

    // 8. حساب الأرباح التلقائية (المبالغ والنسب المئوية)
    public decimal RetailProfitAmount => Math.Max(0, Price - Cost);
    public decimal RetailProfitPercent => Cost > 0 ? Math.Round(((Price - Cost) / Cost) * 100, 1) : 0;

    // ربح بيع الكرتون بالكامل بالمفرد (سعر بيع المفرد × عدد القطع - سعر شراء الكرتون)
    public decimal RetailCartonProfitAmount => (ItemsPerCarton > 0 && Price > 0) ? Math.Max(0, (Price * ItemsPerCarton) - CartonPurchasePrice) : 0;
    public decimal RetailCartonProfitPercent => CartonPurchasePrice > 0 ? Math.Round((RetailCartonProfitAmount / CartonPurchasePrice) * 100, 1) : 0;

    public decimal WholesaleProfitAmount => Math.Max(0, WholesalePrice - Cost);
    public decimal WholesaleProfitPercent => Cost > 0 ? Math.Round(((WholesalePrice - Cost) / Cost) * 100, 1) : 0;

    public decimal CartonProfitAmount => Math.Max(0, CartonSellingPrice - CartonPurchasePrice);
    public decimal CartonProfitPercent => CartonPurchasePrice > 0 ? Math.Round(((CartonSellingPrice - CartonPurchasePrice) / CartonPurchasePrice) * 100, 1) : 0;

    // 9. الصلاحية والتحذيرات
    private bool _hasExpiryDate = false;
    public bool HasExpiryDate
    {
        get => _hasExpiryDate;
        set
        {
            if (SetProperty(ref _hasExpiryDate, value))
            {
                if (!value)
                {
                    _expiryDate = null;
                    _expiryDateManualString = string.Empty;
                    OnPropertyChanged(nameof(ExpiryDate));
                    OnPropertyChanged(nameof(ExpiryDateManualString));
                    ExpiryStatusMessage = Loc["Add_NoExpirySet"];
                }
                else
                {
                    RecalculateExpiry();
                }
            }
        }
    }

    private DateTime? _expiryDate;
    public DateTime? ExpiryDate
    {
        get => _expiryDate;
        set
        {
            if (SetProperty(ref _expiryDate, value))
            {
                _expiryDateManualString = value.HasValue ? value.Value.ToString("yyyy/MM/dd") : string.Empty;
                OnPropertyChanged(nameof(ExpiryDateManualString));
                RecalculateExpiry();
            }
        }
    }

    private string _expiryDateManualString = string.Empty;
    public string ExpiryDateManualString
    {
        get => _expiryDateManualString;
        set
        {
            if (SetProperty(ref _expiryDateManualString, value))
            {
                ParseManualExpiryDate(value);
            }
        }
    }

    private void ParseManualExpiryDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _expiryDate = null;
            OnPropertyChanged(nameof(ExpiryDate));
            RecalculateExpiry();
            return;
        }

        string cleaned = text.Trim().Replace("-", "/").Replace(".", "/");
        string[] formats = { "yyyy/MM/dd", "yyyy/M/d", "yyyy/MM/d", "yyyy/M/dd", "dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy", "yyyyMMdd" };

        if (DateTime.TryParseExact(cleaned, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsed) ||
            DateTime.TryParse(cleaned, out parsed))
        {
            if (parsed.Year >= 2000 && parsed.Year <= 2100)
            {
                _expiryDate = parsed;
                OnPropertyChanged(nameof(ExpiryDate));
                RecalculateExpiry();
                return;
            }
        }

        ExpiryStatusMessage = Loc.IsKurdish ? "نووسینی بەروار..." : "جارٍ كتابة التاريخ...";
    }

    private int _daysRemainingUntilExpiry;
    public int DaysRemainingUntilExpiry
    {
        get => _daysRemainingUntilExpiry;
        private set => SetProperty(ref _daysRemainingUntilExpiry, value);
    }

    private int _expiryAlertDays = 30;
    public int ExpiryAlertDays
    {
        get => _expiryAlertDays;
        set
        {
            if (SetProperty(ref _expiryAlertDays, value))
            {
                RecalculateExpiry();
            }
        }
    }

    private string _expiryStatusMessage = "بدون تاريخ صلاحية";
    public string ExpiryStatusMessage
    {
        get => _expiryStatusMessage;
        private set => SetProperty(ref _expiryStatusMessage, value);
    }

    // 10. حقول التدقيق للقراءة فقط (Read-Only)
    private string _createdAtString = "--";
    public string CreatedAtString
    {
        get => _createdAtString;
        private set => SetProperty(ref _createdAtString, value);
    }

    private string _updatedAtString = "--";
    public string UpdatedAtString
    {
        get => _updatedAtString;
        private set => SetProperty(ref _updatedAtString, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // النوافذ المنبثقة (Popups / Modals)
    private bool _isSupplierModalOpen;
    public bool IsSupplierModalOpen
    {
        get => _isSupplierModalOpen;
        set => SetProperty(ref _isSupplierModalOpen, value);
    }

    private bool _isInventoryLookupOpen = false;
    public bool IsInventoryLookupOpen
    {
        get => _isInventoryLookupOpen;
        set => SetProperty(ref _isInventoryLookupOpen, value);
    }

    private string _inventoryLookupSearch = string.Empty;
    public string InventoryLookupSearch
    {
        get => _inventoryLookupSearch;
        set
        {
            if (SetProperty(ref _inventoryLookupSearch, value))
            {
                _ = FilterLookupProductsAsync();
            }
        }
    }

    public ObservableCollection<Product> LookupProductsList { get; } = new();

    #endregion

    #region Commands

    public ICommand GenerateBarcodeCommand { get; }
    public ICommand BarcodeScannedOrEnteredCommand { get; }
    public ICommand SaveProductCommand { get; }
    public ICommand ClearFormCommand { get; }
    public ICommand OpenSupplierModalCommand { get; }
    public ICommand CloseSupplierModalCommand { get; }
    public ICommand OpenInventoryLookupCommand { get; }
    public ICommand CloseInventoryLookupCommand { get; }
    public ICommand SelectProductFromLookupCommand { get; }
    public ICommand BackToNavigationCommand { get; }
    public ICommand OpenAddCategoryModalCommand { get; }
    public ICommand CloseAddCategoryModalCommand { get; }
    public ICommand SaveNewCategoryCommand { get; }

    #endregion

    public event Action? RequestFocusNameField;
    public event Action? RequestFocusBarcodeField;
    public event Action? RequestBackToNavigation;
    public event Action? ProductSaved;
    public event Action<Product>? RequestNavigateToInventoryWithProduct;

    public AddProductFullViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);
        _supplierService = new SupplierService(_context);

        GenerateBarcodeCommand = new RelayCommand(() =>
        {
            var random = new Random();
            int suffix = random.Next(1000000, 9999999);
            Barcode = $"200245{suffix}";
            StatusMessage = $"تم توليد باركود فريد: {Barcode}";
            RequestFocusNameField?.Invoke();
        });

        BarcodeScannedOrEnteredCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(Barcode))
                return;

            string clean = Barcode.Trim();
            var existing = await _productService.GetProductByBarcodeAsync(clean);
            if (existing != null)
            {
                LoadProductIntoForm(existing);
                StatusMessage = $"تم العثور على المادة: {existing.Name} وجاهزة للتعديل.";
            }
            else
            {
                StatusMessage = "باركود جديد جاهز لإدخال البيانات.";
            }

            RequestFocusNameField?.Invoke();
        });

        SaveProductCommand = new AsyncRelayCommand(async () => await SaveAsync());
        ClearFormCommand = new RelayCommand(ClearForm);

        OpenSupplierModalCommand = new RelayCommand(() => IsSupplierModalOpen = true);
        CloseSupplierModalCommand = new RelayCommand(() => IsSupplierModalOpen = false);

        OpenAddCategoryModalCommand = new RelayCommand(() =>
        {
            NewCategoryName = string.Empty;
            IsAddCategoryModalOpen = true;
        });
        CloseAddCategoryModalCommand = new RelayCommand(() => IsAddCategoryModalOpen = false);
        SaveNewCategoryCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(NewCategoryName))
            {
                MessageBox.Show(Loc.IsKurdish ? "تکایە ناوی پۆلێن بنووسە" : "يرجى كتابة اسم الصنف الجديد.", Loc.IsKurdish ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cleanName = NewCategoryName.Trim();
            using var db = new AppDbContext();
            var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.Categories, c => c.Name.ToLower() == cleanName.ToLower() && !c.IsDeleted);
            if (existing == null)
            {
                existing = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = cleanName,
                    ColorHex = "#3B82F6",
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                await db.Categories.AddAsync(existing);
                await db.SaveChangesAsync();
            }

            await InitializeAsync();
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == existing.Id) ?? existing;
            CategoryText = existing.Name;
            IsAddCategoryModalOpen = false;
            StatusMessage = $"تمت إضافة صنف '{existing.Name}' بنجاح.";
        });

        OpenInventoryLookupCommand = new AsyncRelayCommand(async () =>
        {
            IsInventoryLookupOpen = true;
            await FilterLookupProductsAsync();
        });

        CloseInventoryLookupCommand = new RelayCommand(() => IsInventoryLookupOpen = false);

        SelectProductFromLookupCommand = new RelayCommand((param) =>
        {
            if (param is Product p)
            {
                LoadProductIntoForm(p);
                IsInventoryLookupOpen = false;
                StatusMessage = $"تم تحميل المادة: {p.Name} في النموذج.";
            }
        });

        BackToNavigationCommand = new RelayCommand(() => RequestBackToNavigation?.Invoke());

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        IsSupplierModalOpen = false;
        IsInventoryLookupOpen = false;
        IsAddCategoryModalOpen = false;

        using var db = new AppDbContext();
        var cats = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Categories.Where(c => !c.IsDeleted).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name));

        if (cats.Count == 0)
        {
            var c1 = new Category { Id = Guid.NewGuid(), Name = Loc.IsKurdish ? "خۆراکی گشتی" : "مواد غذائية", ColorHex = "#10B981", DisplayOrder = 1 };
            var c2 = new Category { Id = Guid.NewGuid(), Name = Loc.IsKurdish ? "خواردنەوە و شەربەت" : "مشروبات", ColorHex = "#3B82F6", DisplayOrder = 2 };
            var c3 = new Category { Id = Guid.NewGuid(), Name = Loc.IsKurdish ? "پاكکەرەوەکان" : "منظفات", ColorHex = "#F59E0B", DisplayOrder = 3 };
            var c4 = new Category { Id = Guid.NewGuid(), Name = Loc.IsKurdish ? "کاڵای تر" : "أخرى", ColorHex = "#8B5CF6", DisplayOrder = 4 };
            await db.Categories.AddRangeAsync(c1, c2, c3, c4);
            await db.SaveChangesAsync();
            cats = new List<Category> { c1, c2, c3, c4 };
        }

        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
        if (SelectedCategory == null && Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
            CategoryText = Categories[0].Name;
        }

        var sups = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            db.Suppliers.Where(s => !s.IsDeleted).OrderBy(s => s.Name));
        SuppliersList.Clear();
        foreach (var s in sups) SuppliersList.Add(s);

        if (string.IsNullOrWhiteSpace(Barcode) && ProductId == Guid.Empty)
        {
            Barcode = await _productService.GenerateUniqueBarcodeAsync("200245");
        }

        RequestFocusBarcodeField?.Invoke();
    }

    public async Task FilterLookupProductsAsync()
    {
        var list = await _productService.GetAllProductsListAsync(InventoryLookupSearch);
        LookupProductsList.Clear();
        foreach (var p in list) LookupProductsList.Add(p);
    }

    private void RecalculateTotals()
    {
        decimal fromCartons = (CartonsCount > 0 && ItemsPerCarton > 0) ? (CartonsCount * ItemsPerCarton) : 0;
        StockQuantity = fromCartons + ExtraPiecesCount;

        if (ItemsPerCarton > 0 && CartonPurchasePrice > 0)
        {
            Cost = Math.Round(CartonPurchasePrice / ItemsPerCarton, 2);
        }
        else if (StockQuantity > 0 && CartonPurchasePrice > 0 && CartonsCount > 0)
        {
            Cost = Math.Round((CartonPurchasePrice * CartonsCount) / StockQuantity, 2);
        }

        RecalculateProfits();
    }

    private void RecalculateProfits()
    {
        OnPropertyChanged(nameof(RetailProfitAmount));
        OnPropertyChanged(nameof(RetailProfitPercent));
        OnPropertyChanged(nameof(RetailCartonProfitAmount));
        OnPropertyChanged(nameof(RetailCartonProfitPercent));
        OnPropertyChanged(nameof(WholesaleProfitAmount));
        OnPropertyChanged(nameof(WholesaleProfitPercent));
        OnPropertyChanged(nameof(CartonProfitAmount));
        OnPropertyChanged(nameof(CartonProfitPercent));
    }

    private void RecalculateExpiry()
    {
        if (!HasExpiryDate)
        {
            DaysRemainingUntilExpiry = 0;
            ExpiryStatusMessage = Loc["Add_NoExpirySet"];
            return;
        }

        if (ExpiryDate.HasValue)
        {
            DaysRemainingUntilExpiry = (ExpiryDate.Value.Date - DateTime.Today).Days;
            if (DaysRemainingUntilExpiry < 0)
                ExpiryStatusMessage = Loc.IsKurdish ? $"❌ بەسەرچووە لە پێش {Math.Abs(DaysRemainingUntilExpiry)} ڕۆژ!" : $"❌ منتهي الصلاحية منذ {Math.Abs(DaysRemainingUntilExpiry)} يوم!";
            else if (DaysRemainingUntilExpiry <= ExpiryAlertDays)
                ExpiryStatusMessage = Loc.IsKurdish ? $"⚠️ کەمتر لە {DaysRemainingUntilExpiry} ڕۆژی ماوە!" : $"⚠️ يوشك على الانتهاء خلال {DaysRemainingUntilExpiry} يوم!";
            else
                ExpiryStatusMessage = Loc.IsKurdish ? $"✅ شیاوە ({DaysRemainingUntilExpiry} ڕۆژی ماوە)" : $"✅ صالح (متبقي {DaysRemainingUntilExpiry} يوم)";
        }
        else
        {
            DaysRemainingUntilExpiry = 0;
            ExpiryStatusMessage = Loc.IsKurdish ? "بەرواری بەسەرچوون دیاری نەکراوە" : "لم يتم تحديد تاريخ صلاحية";
        }
    }

    public void LoadProductIntoForm(Product p)
    {
        ProductId = p.Id;
        Barcode = p.Barcode;
        Name = p.Name;
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == p.CategoryId);
        CategoryText = SelectedCategory?.Name ?? string.Empty;

        SupplierName = p.Supplier?.Name ?? p.SupplierName ?? string.Empty;
        SupplierPhone = p.Supplier?.Phone ?? string.Empty;
        SupplierCompany = p.Supplier?.Company ?? string.Empty;
        SupplierNotes = p.Supplier?.Notes ?? string.Empty;
        SupplierBalance = p.Supplier?.Balance ?? 0m;

        CartonsCount = p.CartonsCount;
        ItemsPerCarton = p.ItemsPerCarton > 0 ? p.ItemsPerCarton : 1.0m;
        decimal expectedFromCartons = (CartonsCount * ItemsPerCarton);
        ExtraPiecesCount = p.StockQuantity > expectedFromCartons ? (p.StockQuantity - expectedFromCartons) : 0;
        StockQuantity = p.StockQuantity;
        MinStockAlert = p.MinStockAlert;
        Unit = p.Unit;

        CartonPurchasePrice = p.CartonPurchasePrice;
        Cost = p.Cost;
        Price = p.Price;
        WholesalePrice = p.WholesalePrice;
        CartonSellingPrice = p.CartonSellingPrice;

        HasExpiryDate = p.ExpiryDate.HasValue;
        ExpiryDate = p.ExpiryDate;
        ExpiryAlertDays = p.ExpiryAlertDays;

        CreatedAtString = p.CreatedAt.ToLocalTime().ToString("yyyy/MM/dd - hh:mm tt");
        UpdatedAtString = p.UpdatedAt.HasValue ? p.UpdatedAt.Value.ToLocalTime().ToString("yyyy/MM/dd - hh:mm tt") : "لا يوجد تعديل";

        RecalculateTotals();
        RecalculateExpiry();

        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(FormHeaderTitle));
    }

    public void ClearForm()
    {
        IsInventoryLookupOpen = false;
        IsSupplierModalOpen = false;
        IsAddCategoryModalOpen = false;

        ProductId = Guid.Empty;
        Barcode = string.Empty;
        Name = string.Empty;
        CategoryText = string.Empty;
        SelectedCategory = Categories.FirstOrDefault();
        if (SelectedCategory != null) CategoryText = SelectedCategory.Name;

        SupplierName = string.Empty;
        SupplierPhone = string.Empty;
        SupplierCompany = string.Empty;
        SupplierNotes = string.Empty;
        SupplierBalance = 0m;

        CartonsCount = 0;
        ItemsPerCarton = 1;
        ExtraPiecesCount = 0;
        StockQuantity = 0;
        MinStockAlert = 5;
        Unit = "قطعة";

        CartonPurchasePrice = 0;
        Cost = 0;
        Price = 0;
        WholesalePrice = 0;
        CartonSellingPrice = 0;

        HasExpiryDate = false;
        ExpiryDate = null;
        ExpiryAlertDays = 30;
        CreatedAtString = "--";
        UpdatedAtString = "--";

        RecalculateTotals();
        RecalculateExpiry();

        StatusMessage = "تم تفريغ الحقول وجاهز لإضافة مادة جديدة.";
        OnPropertyChanged(nameof(IsEditMode));
        OnPropertyChanged(nameof(FormHeaderTitle));
        RequestFocusBarcodeField?.Invoke();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Barcode))
        {
            MessageBox.Show(Loc.IsKurdish ? "تکایە بارکۆد بنووسە یان دروستی بکە" : "يرجى إدخال أو توليد الباركود.", Loc.IsKurdish ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show(Loc.IsKurdish ? "تکایە ناوی کاڵا بنووسە" : "يرجى كتابة اسم المادة.", Loc.IsKurdish ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Price <= 0 && CartonSellingPrice <= 0)
        {
            MessageBox.Show(Loc.IsKurdish ? "تکایە نرخی فرۆشتنی تاک یان کارتۆن بنووسە" : "يرجى إدخال سعر بيع المفرد أو سعر بيع الكرتون.", Loc.IsKurdish ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // فحص ومنع البيع بخسارة إذا كان سعر البيع أقل من سعر الشراء أو التكلفة
        if (Price > 0 && Cost > 0 && Price < Cost)
        {
            string msg = Loc.IsKurdish
                ? $"ناتوانرێت کاڵاکە پاشەکەوت بکرێت چونکە نرخی فرۆشتنی تاک ({Price:N0} د.ع) کەمترە لە تێچووی کڕین ({Cost:N0} د.ع)!\nتکایە نرخەکە چاک بکە بۆ ڕێگریکردن لە زەرەر."
                : $"لا يمكن حفظ المادة لأن سعر بيع المفرد ({Price:N0} د.ع) أقل من تكلفة شراء القطعة ({Cost:N0} د.ع)!\nيرجى تصحيح السعر لمنع الخسارة.";
            MessageBox.Show(msg, Loc.IsKurdish ? "ئاگاداری نرخی فرۆشتن" : "تحذير سعر البيع", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CartonSellingPrice > 0 && CartonPurchasePrice > 0 && CartonSellingPrice < CartonPurchasePrice)
        {
            string msg = Loc.IsKurdish
                ? $"ناتوانرێت کاڵاکە پاشەکەوت بکرێت چونکە نرخی فرۆشتنی کارتۆن ({CartonSellingPrice:N0} د.ع) کەمترە لە نرخی کڕینی کارتۆن ({CartonPurchasePrice:N0} د.ع)!\nتکایە نرخەکە چاک بکە."
                : $"لا يمكن حفظ المادة لأن سعر بيع الكرتون ({CartonSellingPrice:N0} د.ع) أقل من سعر شراء الكرتون ({CartonPurchasePrice:N0} د.ع)!\nيرجى تصحيح سعر بيع الكرتون لمنع الخسارة.";
            MessageBox.Show(msg, Loc.IsKurdish ? "ئاگاداری نرخی کارتۆن" : "تحذير سعر الكرتون", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (WholesalePrice > 0 && Cost > 0 && WholesalePrice < Cost)
        {
            string msg = Loc.IsKurdish
                ? $"ناتوانرێت کاڵاکە پاشەکەوت بکرێت چونکە نرخی فرۆشتنی کۆ ({WholesalePrice:N0} د.ع) کەمترە لە تێچووی کڕین ({Cost:N0} د.ع)!\nتکایە نرخەکە چاک بکە."
                : $"لا يمكن حفظ المادة لأن سعر بيع الجملة ({WholesalePrice:N0} د.ع) أقل من تكلفة شراء القطعة ({Cost:N0} د.ع)!\nيرجى تصحيح السعر لمنع الخسارة.";
            MessageBox.Show(msg, Loc.IsKurdish ? "ئاگاداری نرخی جملە" : "تحذير سعر الجملة", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // فحص تاريخ الصلاحية عند تفعيله
        if (HasExpiryDate && !ExpiryDate.HasValue)
        {
            string msg = Loc.IsKurdish
                ? "بەرواری بەسەرچوونت چالاک کردووە، تکایە بەرواری بەسەرچوونی دروست دیاری بکە یان بنووسە پێش پاشەکەوتکردن."
                : "لقد قمت بتفعيل تاريخ الصلاحية، يرجى إدخال تاريخ انتهاء الصلاحية للمادة قبل الحفظ.";
            MessageBox.Show(msg, Loc.IsKurdish ? "ئاگاداری بەروار" : "تنبيه الصلاحية", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // 1. ربط أو إنشاء الصنف تلقائياً إن كان مختاراً أو مكتوباً يدوياً
            Guid? resolvedCategoryId = null;
            string catName = SelectedCategory?.Name ?? CategoryText;
            if (!string.IsNullOrWhiteSpace(catName))
            {
                string cleanCat = catName.Trim();
                using var dbCat = new AppDbContext();
                var catObj = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(dbCat.Categories, c => c.Name.ToLower() == cleanCat.ToLower() && !c.IsDeleted);
                if (catObj == null)
                {
                    catObj = new Category
                    {
                        Id = Guid.NewGuid(),
                        Name = cleanCat,
                        ColorHex = "#3B82F6",
                        CreatedAt = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    await dbCat.Categories.AddAsync(catObj);
                    await dbCat.SaveChangesAsync();
                }
                resolvedCategoryId = catObj.Id;
            }

            // 2. ربط أو إنشاء المندوب تلقائياً إن وجد اسم
            Supplier? supplierObj = null;
            if (!string.IsNullOrWhiteSpace(SupplierName))
            {
                supplierObj = await _supplierService.GetOrCreateSupplierByNameAsync(SupplierName.Trim(), SupplierPhone, SupplierCompany, SupplierNotes);
            }

            var product = new Product
            {
                Id = ProductId,
                Barcode = Barcode.Trim(),
                Name = Name.Trim(),
                CategoryId = resolvedCategoryId,
                SupplierId = supplierObj?.Id,
                SupplierName = supplierObj?.Name ?? (string.IsNullOrWhiteSpace(SupplierName) ? null : SupplierName.Trim()),

                CartonsCount = CartonsCount,
                ItemsPerCarton = ItemsPerCarton,
                StockQuantity = StockQuantity,
                MinStockAlert = MinStockAlert,
                Unit = string.IsNullOrWhiteSpace(Unit) ? "قطعة" : Unit.Trim(),

                CartonPurchasePrice = CartonPurchasePrice,
                Cost = Cost,
                Price = Price,
                WholesalePrice = WholesalePrice,
                CartonSellingPrice = CartonSellingPrice,

                ExpiryDate = HasExpiryDate ? ExpiryDate : null,
                ExpiryAlertDays = ExpiryAlertDays,
                TaxRate = 0.0m,
                IsActive = true
            };

            bool saved = await _productService.SaveProductAsync(product);
            if (saved)
            {
                StatusMessage = $"تم حفظ المادة '{product.Name}' بنجاح في قاعدة البيانات.";
                ProductSaved?.Invoke();
                RequestNavigateToInventoryWithProduct?.Invoke(product);
                MessageBox.Show(Loc.IsKurdish ? $"کاڵای '{product.Name}' بە سەرکەوتوویی پاشەکەوت کرا و نێردرا بۆ کۆگا!" : $"تم حفظ المادة '{product.Name}' بنجاح وإرسالها للمخزن!", Loc.IsKurdish ? "سەرکەوتوو بوو" : "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
                ClearForm();
            }
            else
            {
                MessageBox.Show(Loc.IsKurdish ? "پاشەکەوتکردنی کاڵا سەرکەوتوو نەبوو، تکایە دڵنیابەرەوە لە زانیارییەکان" : "فشل حفظ المادة، يرجى التأكد من البيانات ومحاولة الحفظ مجدداً.", Loc.IsKurdish ? "هەڵە" : "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء حفظ المادة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
