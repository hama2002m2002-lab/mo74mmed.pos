using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class MainShellViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;
    private readonly DispatcherTimer _clockTimer;

    public DashboardViewModel DashboardVM { get; }
    public MainCashierViewModel CashierVM { get; }
    public SalesHistoryViewModel SalesHistoryVM { get; }
    public InventoryViewModel InventoryVM { get; }
    public StockViewModel StockVM { get; }
    public StockAuditViewModel StockAuditVM { get; }
    public DamagedItemsViewModel DamagedItemsVM { get; }
    public PurchaseViewModel PurchaseVM { get; }
    public AddProductFullViewModel AddProductVM { get; }
    public SuppliersViewModel SuppliersVM { get; }
    public SupplierOrdersViewModel SupplierOrdersVM { get; }
    public UserAccountsViewModel UserAccountsVM { get; }
    public ReportsViewModel ReportsVM { get; }
    public WarehouseHubViewModel WarehouseHubVM { get; }
    public PrintingViewModel PrintingVM { get; }
    public NetworkSettingsViewModel SettingsVM { get; }

    private object _currentView;
    public object CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    private string _activeTab = "Dashboard";
    public string ActiveTab
    {
        get => _activeTab;
        set => SetProperty(ref _activeTab, value);
    }

    private bool _isSidebarVisible = true;
    public bool IsSidebarVisible
    {
        get => _isSidebarVisible;
        set => SetProperty(ref _isSidebarVisible, value);
    }

    private string _currentCashierName = "محمد الكاشير";
    public string CurrentCashierName
    {
        get => _currentCashierName;
        set => SetProperty(ref _currentCashierName, value);
    }

    private string _currentDateTimeString = string.Empty;
    public string CurrentDateTimeString
    {
        get => _currentDateTimeString;
        set => SetProperty(ref _currentDateTimeString, value);
    }

    private bool _isWarehouseExpanded = true;
    public bool IsWarehouseExpanded
    {
        get => _isWarehouseExpanded;
        set => SetProperty(ref _isWarehouseExpanded, value);
    }

    #region Navigation Commands

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowCashierCommand { get; }
    public ICommand ShowSalesHistoryCommand { get; }
    public ICommand ShowInventoryCommand { get; }
    public ICommand ShowWarehouseHubCommand { get; }
    public ICommand ShowStockCommand { get; }
    public ICommand ShowStockAuditCommand { get; }
    public ICommand ToggleWarehouseMenuCommand { get; }
    public ICommand ShowDamagedItemsCommand { get; }
    public ICommand ShowPurchaseCommand { get; }
    public ICommand ShowAddProductCommand { get; }
    public ICommand ShowSuppliersCommand { get; }
    public ICommand ShowSupplierOrdersCommand { get; }
    public ICommand ShowUserAccountsCommand { get; }
    public ICommand ShowPrintingCommand { get; }
    public ICommand ShowReportsCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand ToggleLanguageCommand { get; }
    public ICommand ToggleThemeCommand { get; }
    public ICommand OpenNetworkSettingsCommand { get; }
    public ICommand CheckUpdatesCommand { get; }

    public ThemeManager Theme => ThemeManager.Instance;

    #endregion

    public MainShellViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);

        DashboardVM = new DashboardViewModel();
        CashierVM = new MainCashierViewModel();
        SalesHistoryVM = new SalesHistoryViewModel();
        InventoryVM = new InventoryViewModel();
        StockVM = new StockViewModel();
        StockAuditVM = new StockAuditViewModel();
        DamagedItemsVM = new DamagedItemsViewModel();
        PurchaseVM = new PurchaseViewModel();
        AddProductVM = new AddProductFullViewModel();
        SuppliersVM = new SuppliersViewModel();
        SupplierOrdersVM = new SupplierOrdersViewModel();
        UserAccountsVM = new UserAccountsViewModel();
        ReportsVM = new ReportsViewModel();
        WarehouseHubVM = new WarehouseHubViewModel();
        PrintingVM = new PrintingViewModel();
        SettingsVM = new NetworkSettingsViewModel();

        _currentView = DashboardVM;

        ShowWarehouseHubCommand = new RelayCommand(() =>
        {
            ActiveTab = "WarehouseHub";
            IsSidebarVisible = true;
            CurrentView = WarehouseHubVM;
            _ = WarehouseHubVM.LoadHubDataAsync();
        });

        ToggleWarehouseMenuCommand = new RelayCommand(() =>
        {
            ActiveTab = "WarehouseHub";
            IsSidebarVisible = true;
            CurrentView = WarehouseHubVM;
            _ = WarehouseHubVM.LoadHubDataAsync();
        });

        // WarehouseHub Sub-navigation Events
        WarehouseHubVM.RequestOpenInventory += () =>
        {
            ActiveTab = "Inventory";
            IsSidebarVisible = true;
            CurrentView = InventoryVM;
            _ = InventoryVM.LoadProductsAsync();
        };

        WarehouseHubVM.RequestOpenAddProduct += () =>
        {
            ActiveTab = "AddProduct";
            IsSidebarVisible = true;
            CurrentView = AddProductVM;
            AddProductVM.ClearForm();
        };

        WarehouseHubVM.RequestOpenDamagedItems += () =>
        {
            ActiveTab = "DamagedItems";
            IsSidebarVisible = true;
            CurrentView = DamagedItemsVM;
            _ = DamagedItemsVM.LoadDataAsync();
        };

        WarehouseHubVM.RequestOpenStock += () =>
        {
            ActiveTab = "Stock";
            IsSidebarVisible = true;
            CurrentView = StockVM;
            _ = StockVM.LoadStockAsync();
        };

        WarehouseHubVM.RequestOpenStockAudit += () =>
        {
            ActiveTab = "StockAudit";
            IsSidebarVisible = true;
            CurrentView = StockAuditVM;
            _ = StockAuditVM.LoadAllForAuditAsync();
        };

        WarehouseHubVM.RequestEditProduct += (p) =>
        {
            ActiveTab = "AddProduct";
            IsSidebarVisible = true;
            CurrentView = AddProductVM;
            AddProductVM.LoadProductIntoForm(p);
        };

        ToggleLanguageCommand = new RelayCommand(() => Loc.ToggleLanguage());
        ToggleThemeCommand = new RelayCommand(() => Theme.ToggleTheme());

        // 1. عند النقر على إضافة مادة من المخزن
        InventoryVM.RequestAddProduct += () =>
        {
            ActiveTab = "AddProduct";
            IsSidebarVisible = true;
            CurrentView = AddProductVM;
            AddProductVM.ClearForm();
        };

        // 2. عند النقر على تعديل مادة من المخزن
        InventoryVM.RequestEditProduct += (p) =>
        {
            ActiveTab = "AddProduct";
            IsSidebarVisible = true;
            CurrentView = AddProductVM;
            AddProductVM.LoadProductIntoForm(p);
        };

        StockVM.RequestEditProduct += (p) =>
        {
            ActiveTab = "AddProduct";
            IsSidebarVisible = true;
            CurrentView = AddProductVM;
            AddProductVM.LoadProductIntoForm(p);
        };

        // 3. زر العودة من شاشة إدارة وتفاصيل المخزن
        InventoryVM.RequestBackToNavigation += () =>
        {
            IsSidebarVisible = true;
            ActiveTab = "WarehouseHub";
            CurrentView = WarehouseHubVM;
            _ = WarehouseHubVM.LoadHubDataAsync();
        };

        // 4. زر العودة من شاشة إضافة المواد
        AddProductVM.RequestBackToNavigation += () =>
        {
            IsSidebarVisible = true;
            ActiveTab = "WarehouseHub";
            CurrentView = WarehouseHubVM;
            _ = WarehouseHubVM.LoadHubDataAsync();
        };

        // 5. زر العودة من شاشة المناديب
        SuppliersVM.RequestBackToNavigation += () =>
        {
            IsSidebarVisible = true;
            ActiveTab = "Dashboard";
            CurrentView = DashboardVM;
            _ = DashboardVM.LoadDashboardDataAsync();
        };

        // 6. زر العودة من شاشة الكاشير
        CashierVM.RequestBackToNavigation += () =>
        {
            IsSidebarVisible = true;
            ActiveTab = "Dashboard";
            CurrentView = DashboardVM;
            _ = DashboardVM.LoadDashboardDataAsync();
        };

        // 7. زر العودة من شاشة التقارير
        ReportsVM.RequestBackToNavigation += () =>
        {
            IsSidebarVisible = true;
            ActiveTab = "Dashboard";
            CurrentView = DashboardVM;
            _ = DashboardVM.LoadDashboardDataAsync();
        };

        // 8. زر العودة من شاشة المواد التالفة
        DamagedItemsVM.RequestBackToNavigation += () =>
        {
            IsSidebarVisible = true;
            ActiveTab = "Dashboard";
            CurrentView = DashboardVM;
            _ = DashboardVM.LoadDashboardDataAsync();
        };

        // 9. زر العودة من شاشة الشراء والتوريد
        PurchaseVM.RequestBackToNavigation += () =>
        {
            IsSidebarVisible = true;
            ActiveTab = "Dashboard";
            CurrentView = DashboardVM;
            _ = DashboardVM.LoadDashboardDataAsync();
        };

        PurchaseVM.RequestNavigateToCashier += () =>
        {
            ActiveTab = "Cashier";
            IsSidebarVisible = false;
            CurrentView = CashierVM;
            _ = CashierVM.InitializeAsync();
        };

        PurchaseVM.RequestNavigateToReports += () =>
        {
            ActiveTab = "Reports";
            IsSidebarVisible = false;
            CurrentView = ReportsVM;
            _ = ReportsVM.LoadReportAsync();
        };

        PurchaseVM.RequestNavigateToSuppliers += () =>
        {
            ActiveTab = "Suppliers";
            IsSidebarVisible = false;
            CurrentView = SuppliersVM;
            _ = SuppliersVM.InitializeAsync();
        };

        // 10. ربط الشراء والتوريد مع تحديث المخزن والتقارير والمناديب
        PurchaseVM.PurchaseCompleted += () =>
        {
            _ = InventoryVM.LoadProductsAsync();
            _ = StockVM.LoadStockAsync();
            _ = ReportsVM.LoadReportAsync();
            _ = SuppliersVM.LoadSuppliersAsync();
            _ = DashboardVM.LoadDashboardDataAsync();
        };

        // 11. ربط المواد التالفة مع تحديث المخزون
        DamagedItemsVM.DamagedRecordAdded += () =>
        {
            _ = InventoryVM.LoadProductsAsync();
            _ = StockVM.LoadStockAsync();
            _ = DashboardVM.LoadDashboardDataAsync();
        };

        // 12. ربط واجهة إضافة المواد مع واجهات المخزن والبيع والداشبورد
        AddProductVM.ProductSaved += () =>
        {
            _ = InventoryVM.LoadProductsAsync();
            _ = StockVM.LoadStockAsync();
            _ = CashierVM.FilterProductsAsync();
            _ = CashierVM.FilterWarehouseProductsAsync();
            _ = DashboardVM.LoadDashboardDataAsync();
            _ = SuppliersVM.LoadSuppliersAsync();
        };

        // 13. ربط واجهة البيع (الكاشير) مع المخزن والتقارير والداشبورد والمبيعات
        CashierVM.SaleCompleted += () =>
        {
            _ = InventoryVM.LoadProductsAsync();
            _ = StockVM.LoadStockAsync();
            _ = ReportsVM.LoadReportAsync();
            _ = DashboardVM.LoadDashboardDataAsync();
            _ = SuppliersVM.LoadSuppliersAsync();
            _ = SalesHistoryVM.LoadSalesDataAsync();
        };

        ShowDashboardCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Dashboard";
            IsSidebarVisible = true;
            CurrentView = DashboardVM;
            await DashboardVM.LoadDashboardDataAsync();
        });

        ShowCashierCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Cashier";
            IsSidebarVisible = false;
            CurrentView = CashierVM;
            await CashierVM.InitializeAsync();
        });

        ShowSalesHistoryCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "SalesHistory";
            IsSidebarVisible = true;
            CurrentView = SalesHistoryVM;
            await SalesHistoryVM.LoadSalesDataAsync();
        });

        ShowInventoryCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Inventory";
            IsSidebarVisible = true;
            CurrentView = InventoryVM;
            await InventoryVM.LoadProductsAsync();
        });

        ShowStockCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Stock";
            IsSidebarVisible = true;
            CurrentView = StockVM;
            await StockVM.LoadStockAsync();
        });

        ShowStockAuditCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "StockAudit";
            IsSidebarVisible = true;
            CurrentView = StockAuditVM;
            await StockAuditVM.LoadAllForAuditAsync();
        });

        ShowDamagedItemsCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "DamagedItems";
            IsSidebarVisible = true;
            CurrentView = DamagedItemsVM;
            await DamagedItemsVM.LoadDataAsync();
        });

        ShowPurchaseCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Purchase";
            IsSidebarVisible = true;
            CurrentView = PurchaseVM;
            await PurchaseVM.InitializeAsync();
        });

        ShowAddProductCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "AddProduct";
            IsSidebarVisible = true;
            CurrentView = AddProductVM;
            await AddProductVM.InitializeAsync();
        });

        ShowSuppliersCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Suppliers";
            IsSidebarVisible = true;
            CurrentView = SuppliersVM;
            await SuppliersVM.InitializeAsync();
        });

        ShowSupplierOrdersCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "SupplierOrders";
            IsSidebarVisible = true;
            CurrentView = SupplierOrdersVM;
            await SupplierOrdersVM.LoadOrdersAsync();
        });

        SupplierOrdersVM.OrderConvertedToPurchase += () =>
        {
            _ = InventoryVM.LoadProductsAsync();
            _ = StockVM.LoadStockAsync();
            _ = DashboardVM.LoadDashboardDataAsync();
            _ = SuppliersVM.LoadSuppliersAsync();
            _ = PurchaseVM.InitializeAsync();
        };

        // Start Mobile Rep Portal Service on local WiFi port 5000
        try
        {
            RepWebPortalService.Instance.Start(5000);
            RepWebPortalService.Instance.OrderReceived += () =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    _ = SupplierOrdersVM.LoadOrdersAsync();
                });
            };

            // Start 24/7 Background Cloud Sync (every 30 seconds)
            CloudSyncService.Instance.StartBackgroundSync(30);
        }
        catch { }

        ShowUserAccountsCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "UserAccounts";
            IsSidebarVisible = true;
            CurrentView = UserAccountsVM;
            await UserAccountsVM.LoadUsersAsync();
        });

        ShowPrintingCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Printing";
            IsSidebarVisible = true;
            CurrentView = PrintingVM;
            await PrintingVM.InitializeAsync();
        });

        ShowReportsCommand = new AsyncRelayCommand(async () =>
        {
            ActiveTab = "Reports";
            IsSidebarVisible = false;
            CurrentView = ReportsVM;
            await ReportsVM.LoadReportAsync();
        });

        ShowSettingsCommand = new RelayCommand(() =>
        {
            ActiveTab = "Settings";
            IsSidebarVisible = true;
            CurrentView = SettingsVM;
        });

        ToggleSidebarCommand = new RelayCommand(() =>
        {
            IsSidebarVisible = !IsSidebarVisible;
        });

        OpenNetworkSettingsCommand = new RelayCommand(() =>
        {
            var window = new HamoPos.Views.NetworkSettingsWindow();
            window.ShowDialog();
        });

        CheckUpdatesCommand = new RelayCommand(() =>
        {
            HamoPos.Services.UpdateService.Instance.CheckForUpdates(isManual: true);
        });

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (s, e) =>
        {
            CurrentDateTimeString = DateTime.Now.ToString("dddd, dd MMMM yyyy - hh:mm:ss tt", new System.Globalization.CultureInfo("ar-SA"));
        };
        _clockTimer.Start();
        CurrentDateTimeString = DateTime.Now.ToString("dddd, dd MMMM yyyy - hh:mm:ss tt", new System.Globalization.CultureInfo("ar-SA"));
    }

    public async Task InitializeAsync()
    {
        await DbInitializer.InitializeAsync(_context);
        await DashboardVM.LoadDashboardDataAsync();
        await CashierVM.InitializeAsync();
        await SalesHistoryVM.InitializeAsync();
        await InventoryVM.InitializeAsync();
        await StockVM.InitializeAsync();
        await StockAuditVM.InitializeAsync();
        await DamagedItemsVM.InitializeAsync();
        await PurchaseVM.InitializeAsync();
        await AddProductVM.InitializeAsync();
        await SuppliersVM.InitializeAsync();
        await SupplierOrdersVM.InitializeAsync();
        await UserAccountsVM.InitializeAsync();
        await PrintingVM.InitializeAsync();
        await ReportsVM.InitializeAsync();
    }
}
