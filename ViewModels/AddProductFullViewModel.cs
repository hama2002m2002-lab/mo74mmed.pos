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
    public string FormHeaderTitle => IsEditMode ? "✏️ تعديل بيانات المادة" : "➕ إضافة مادة جديدة للمخزن";

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
        set => SetProperty(ref _selectedCategory, value);
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

        ExpiryStatusMessage = "جارٍ كتابة التاريخ...";
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

    private string _expiryStatusMessage = "لم يتم تحديد تاريخ صلاحية";
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
                MessageBox.Show("يرجى كتابة اسم الصنف الجديد.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cleanName = NewCategoryName.Trim();
            var existing = Categories.FirstOrDefault(c => c.Name.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                SelectedCategory = existing;
                IsAddCategoryModalOpen = false;
                return;
            }

            var newCat = new Category
            {
                Id = Guid.NewGuid(),
                Name = cleanName,
                ColorHex = "#3B82F6",
                CreatedAt = DateTime.UtcNow
            };

            _context.Categories.Add(newCat);
            await _context.SaveChangesAsync();

            Categories.Add(newCat);
            SelectedCategory = newCat;
            IsAddCategoryModalOpen = false;
            StatusMessage = $"تمت إضافة صنف '{newCat.Name}' بنجاح.";
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
    }

    public async Task InitializeAsync()
    {
        IsSupplierModalOpen = false;
        IsInventoryLookupOpen = false;
        IsAddCategoryModalOpen = false;

        var cats = await _productService.GetCategoriesAsync();
        Categories.Clear();
        foreach (var c in cats) Categories.Add(c);
        if (SelectedCategory == null && Categories.Count > 0) SelectedCategory = Categories[0];

        var sups = await _supplierService.GetSuppliersAsync();
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
        if (ExpiryDate.HasValue)
        {
            DaysRemainingUntilExpiry = (ExpiryDate.Value.Date - DateTime.Today).Days;
            if (DaysRemainingUntilExpiry < 0)
                ExpiryStatusMessage = $"❌ منتهي الصلاحية منذ {Math.Abs(DaysRemainingUntilExpiry)} يوم!";
            else if (DaysRemainingUntilExpiry <= ExpiryAlertDays)
                ExpiryStatusMessage = $"⚠️ يوشك على الانتهاء خلال {DaysRemainingUntilExpiry} يوم!";
            else
                ExpiryStatusMessage = $"✅ صالح (متبقي {DaysRemainingUntilExpiry} يوم)";
        }
        else
        {
            DaysRemainingUntilExpiry = 0;
            ExpiryStatusMessage = "لم يتم تحديد تاريخ صلاحية";
        }
    }

    public void LoadProductIntoForm(Product p)
    {
        ProductId = p.Id;
        Barcode = p.Barcode;
        Name = p.Name;
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == p.CategoryId);

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
        Barcode = string.Empty; // لا ينشئ باركود تلقائياً بل يترك الحقل فارغاً
        Name = string.Empty;
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
            MessageBox.Show("يرجى إدخال أو توليد الباركود.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("يرجى كتابة اسم المادة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Price <= 0 && CartonSellingPrice <= 0)
        {
            MessageBox.Show("يرجى إدخال سعر بيع المفرد أو سعر بيع الكرتون.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // ربط أو إنشاء المندوب تلقائياً إن وجد اسم
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
            CategoryId = SelectedCategory?.Id,
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

            ExpiryDate = ExpiryDate,
            ExpiryAlertDays = ExpiryAlertDays,
            TaxRate = 0.0m,
            IsActive = true
        };

        bool saved = await _productService.SaveProductAsync(product);
        if (saved)
        {
            StatusMessage = $"تم حفظ المادة '{product.Name}' بنجاح في قاعدة البيانات.";
            ProductSaved?.Invoke();
            MessageBox.Show($"تم حفظ المادة '{product.Name}' بنجاح!", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForm();
        }
        else
        {
            MessageBox.Show("فشل حفظ المادة، يرجى التأكد من عدم تكرار الباركود.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
