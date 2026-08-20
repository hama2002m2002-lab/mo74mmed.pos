using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class MainCashierViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;
    private readonly ISaleService _saleService;

    #region Properties

    // 1. نظام تعدد نوافذ وفواتير البيع (Multi-Tab Invoices)
    public ObservableCollection<InvoiceTabViewModel> InvoiceTabs { get; } = new();

    private InvoiceTabViewModel _selectedTab;
    public InvoiceTabViewModel SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                foreach (var tab in InvoiceTabs)
                {
                    tab.IsSelected = (tab == value);
                }
            }
        }
    }

    public enum BarcodeFeedbackState
    {
        Normal,
        Success,
        Error
    }

    private BarcodeFeedbackState _barcodeScanStatus = BarcodeFeedbackState.Normal;
    private System.Threading.CancellationTokenSource? _barcodeFeedbackCts;

    public BarcodeFeedbackState BarcodeScanStatus
    {
        get => _barcodeScanStatus;
        set
        {
            if (SetProperty(ref _barcodeScanStatus, value))
            {
                OnPropertyChanged(nameof(BarcodeBorderBrush));
                OnPropertyChanged(nameof(BarcodeBackgroundBrush));
                OnPropertyChanged(nameof(BarcodeScannerIcon));
                OnPropertyChanged(nameof(BarcodeScannerTitle));
                OnPropertyChanged(nameof(BarcodeScannerIconColor));
            }
        }
    }

    private bool _isReturnModeActive;
    public bool IsReturnModeActive
    {
        get => _isReturnModeActive;
        set
        {
            if (SetProperty(ref _isReturnModeActive, value))
            {
                OnPropertyChanged(nameof(BarcodeBorderBrush));
                OnPropertyChanged(nameof(BarcodeBackgroundBrush));
                OnPropertyChanged(nameof(BarcodeScannerIcon));
                OnPropertyChanged(nameof(BarcodeScannerTitle));
                OnPropertyChanged(nameof(BarcodeScannerIconColor));
                OnPropertyChanged(nameof(CheckoutCashButtonText));
                OnPropertyChanged(nameof(CheckoutCardButtonText));
                OnPropertyChanged(nameof(CheckoutCashButtonBackground));
                OnPropertyChanged(nameof(CheckoutCardButtonBackground));
                OnPropertyChanged(nameof(CartGrandTotalTitle));
                OnPropertyChanged(nameof(CartGrandTotalColor));
                StatusMessage = value 
                    ? (Loc.IsKurdish ? "🔄 دۆخی گەڕاندنەوەی کاڵا چالاکە. هەر بارکۆدێک لێبدرێت وەک گەڕاوە تۆمار دەکرێت." : "🔄 وضع الإرجاع نشط. أي باركود يتم مسحه سيتم قيده كإرجاع واسترداد.")
                    : (Loc.IsKurdish ? "دۆخی فرۆشتنی ئاسایی چالاکە." : "جاهز لمسح الباركود وبدء البيع.");
            }
        }
    }

    public string BarcodeScannerTitle => IsReturnModeActive 
        ? (Loc.IsKurdish ? "🔄 دۆخی گەڕاندنەوەی کاڵا" : "🔄 وضع إرجاع المواد")
        : Loc["Pos_BarcodeScanner"];

    public string CheckoutCashButtonText => IsReturnModeActive 
        ? (Loc.IsKurdish ? "💵 گەڕاندنەوەی نەقد [F1]" : "💵 إرجاع نقدي [F1]")
        : Loc["Pos_PayCash"];

    public string CheckoutCardButtonText => IsReturnModeActive 
        ? (Loc.IsKurdish ? "💳 گەڕاندنەوەی کارت [F2]" : "💳 إرجاع شبكة/بطاقة [F2]")
        : Loc["Pos_PayCard"];

    public string CheckoutCashButtonBackground => IsReturnModeActive ? "#DC2626" : "#10B981";
    public string CheckoutCardButtonBackground => IsReturnModeActive ? "#991B1B" : "#3B82F6";

    public string CartGrandTotalTitle => IsReturnModeActive 
        ? (Loc.IsKurdish ? "بڕی پارەی گەڕاوە بۆ کڕیار (قەرەبوو)" : "المبلغ المسترجع للزبون (المطلوب دفعه)")
        : Loc["Cart_FinalRequired"];

    public string CartGrandTotalColor => IsReturnModeActive ? "#EF4444" : "#10B981";

    public string BarcodeBorderBrush
    {
        get
        {
            if (IsReturnModeActive) return "#EF4444"; // Red
            return BarcodeScanStatus switch
            {
                BarcodeFeedbackState.Success => "#10B981", // Green
                BarcodeFeedbackState.Error => "#EF4444",   // Red
                _ => ThemeManager.Instance.IsDarkMode ? "#3B82F6" : "#2563EB"
            };
        }
    }

    public string BarcodeBackgroundBrush
    {
        get
        {
            bool isDark = ThemeManager.Instance.IsDarkMode;
            if (IsReturnModeActive) return isDark ? "#2D0E0E" : "#FEE2E2";
            return BarcodeScanStatus switch
            {
                BarcodeFeedbackState.Success => isDark ? "#0B261A" : "#D1FAE5",
                BarcodeFeedbackState.Error => isDark ? "#2A0E0E" : "#FEE2E2",
                _ => isDark ? "#182234" : "#FFFFFF"
            };
        }
    }

    public string BarcodeScannerIcon
    {
        get
        {
            if (IsReturnModeActive) return "🔄 ";
            return BarcodeScanStatus switch
            {
                BarcodeFeedbackState.Success => "✅ ",
                BarcodeFeedbackState.Error => "❌ ",
                _ => "📷 "
            };
        }
    }

    public string BarcodeScannerIconColor
    {
        get
        {
            if (IsReturnModeActive) return "#EF4444";
            return BarcodeScanStatus switch
            {
                BarcodeFeedbackState.Success => "#10B981",
                BarcodeFeedbackState.Error => "#EF4444",
                _ => "#60A5FA"
            };
        }
    }

    public void TriggerBarcodeFeedback(BarcodeFeedbackState state)
    {
        _barcodeFeedbackCts?.Cancel();
        _barcodeFeedbackCts = new System.Threading.CancellationTokenSource();
        var token = _barcodeFeedbackCts.Token;

        BarcodeScanStatus = state;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1800, token);
                if (!token.IsCancellationRequested)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        BarcodeScanStatus = BarcodeFeedbackState.Normal;
                    });
                }
            }
            catch (TaskCanceledException) { }
        }, token);
    }

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                if (BarcodeScanStatus != BarcodeFeedbackState.Normal && !string.IsNullOrEmpty(value))
                {
                    BarcodeScanStatus = BarcodeFeedbackState.Normal;
                }
            }
        }
    }

    private string _statusMessage = "جاهز لمسح الباركود وبدء البيع.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();

    // 2. نافذة المخزن المنبثقة (نصف الشاشة)
    private bool _isWarehouseModalOpen;
    public bool IsWarehouseModalOpen
    {
        get => _isWarehouseModalOpen;
        set
        {
            if (SetProperty(ref _isWarehouseModalOpen, value))
            {
                if (value)
                {
                    _ = FilterWarehouseProductsAsync();
                    RequestFocusWarehouseSearch?.Invoke();
                }
                else
                {
                    RequestFocusBarcodeField?.Invoke();
                }
            }
        }
    }

    private string _warehouseSearchQuery = string.Empty;
    public string WarehouseSearchQuery
    {
        get => _warehouseSearchQuery;
        set
        {
            if (SetProperty(ref _warehouseSearchQuery, value))
            {
                _ = FilterWarehouseProductsAsync();
            }
        }
    }

    public ObservableCollection<Product> WarehouseProductsList { get; } = new();

    // 3. نافذة سجل الوصلات والورديات والفواتير المسترجعة
    private bool _isSalesHistoryModalOpen;
    public bool IsSalesHistoryModalOpen
    {
        get => _isSalesHistoryModalOpen;
        set => SetProperty(ref _isSalesHistoryModalOpen, value);
    }

    public ObservableCollection<Sale> SalesHistoryList { get; } = new();
    private readonly List<Sale> _allSalesHistoryCache = new();

    private string _activeSalesHistoryFilter = "All"; // "All", "Sales", "Returns"
    public string ActiveSalesHistoryFilter
    {
        get => _activeSalesHistoryFilter;
        set
        {
            if (SetProperty(ref _activeSalesHistoryFilter, value))
            {
                OnPropertyChanged(nameof(IsAllSalesHistoryFilterActive));
                OnPropertyChanged(nameof(IsSalesOnlyFilterActive));
                OnPropertyChanged(nameof(IsReturnsOnlyFilterActive));
                ApplySalesHistoryFilter();
            }
        }
    }

    public bool IsAllSalesHistoryFilterActive => ActiveSalesHistoryFilter == "All";
    public bool IsSalesOnlyFilterActive => ActiveSalesHistoryFilter == "Sales";
    public bool IsReturnsOnlyFilterActive => ActiveSalesHistoryFilter == "Returns";

    private void ApplySalesHistoryFilter()
    {
        SalesHistoryList.Clear();
        var items = ActiveSalesHistoryFilter switch
        {
            "Sales" => _allSalesHistoryCache.Where(s => s.Status != "Returned"),
            "Returns" => _allSalesHistoryCache.Where(s => s.Status == "Returned" || s.Items.Any(i => i.Quantity < 0 || i.TotalPrice < 0)),
            _ => _allSalesHistoryCache
        };

        foreach (var s in items)
        {
            SalesHistoryList.Add(s);
        }
    }

    // إحصائيات سجل الوردية والمبيعات والمرتجعات
    private decimal _shiftGrossSales;
    public decimal ShiftGrossSales
    {
        get => _shiftGrossSales;
        set
        {
            if (SetProperty(ref _shiftGrossSales, value))
            {
                OnPropertyChanged(nameof(ShiftNetSales));
            }
        }
    }

    private decimal _shiftReturnsAmount;
    public decimal ShiftReturnsAmount
    {
        get => _shiftReturnsAmount;
        set
        {
            if (SetProperty(ref _shiftReturnsAmount, value))
            {
                OnPropertyChanged(nameof(ShiftNetSales));
            }
        }
    }

    public decimal ShiftNetSales => Math.Max(0, ShiftGrossSales - ShiftReturnsAmount);

    private int _shiftInvoicesCount;
    public int ShiftInvoicesCount
    {
        get => _shiftInvoicesCount;
        set => SetProperty(ref _shiftInvoicesCount, value);
    }

    private int _shiftReturnsCount;
    public int ShiftReturnsCount
    {
        get => _shiftReturnsCount;
        set => SetProperty(ref _shiftReturnsCount, value);
    }

    // 4. نافذة تفاصيل الوصل الكاملة
    private bool _isInvoiceDetailsModalOpen;
    public bool IsInvoiceDetailsModalOpen
    {
        get => _isInvoiceDetailsModalOpen;
        set => SetProperty(ref _isInvoiceDetailsModalOpen, value);
    }

    private Sale? _selectedInvoiceForDetails;
    public Sale? SelectedInvoiceForDetails
    {
        get => _selectedInvoiceForDetails;
        set => SetProperty(ref _selectedInvoiceForDetails, value);
    }

    // 5. إحصائيات الوردية اليومية
    private int _todaySalesCount;
    public int TodaySalesCount
    {
        get => _todaySalesCount;
        private set => SetProperty(ref _todaySalesCount, value);
    }

    private decimal _todayRevenue;
    public decimal TodayRevenue
    {
        get => _todayRevenue;
        private set => SetProperty(ref _todayRevenue, value);
    }

    // 6. إدارة الدرج والصندوق وحركة النقد
    private bool _isDrawerModalOpen;
    public bool IsDrawerModalOpen
    {
        get => _isDrawerModalOpen;
        set => SetProperty(ref _isDrawerModalOpen, value);
    }

    private decimal _drawerOpeningBalance;
    public decimal DrawerOpeningBalance
    {
        get => _drawerOpeningBalance;
        set
        {
            if (SetProperty(ref _drawerOpeningBalance, value))
            {
                OnPropertyChanged(nameof(DrawerCurrentCash));
            }
        }
    }

    private decimal _drawerCashSales;
    public decimal DrawerCashSales
    {
        get => _drawerCashSales;
        set
        {
            if (SetProperty(ref _drawerCashSales, value))
            {
                OnPropertyChanged(nameof(DrawerCurrentCash));
            }
        }
    }

    private decimal _drawerDeposits;
    public decimal DrawerDeposits
    {
        get => _drawerDeposits;
        set
        {
            if (SetProperty(ref _drawerDeposits, value))
            {
                OnPropertyChanged(nameof(DrawerCurrentCash));
            }
        }
    }

    private decimal _drawerWithdrawals;
    public decimal DrawerWithdrawals
    {
        get => _drawerWithdrawals;
        set
        {
            if (SetProperty(ref _drawerWithdrawals, value))
            {
                OnPropertyChanged(nameof(DrawerCurrentCash));
            }
        }
    }

    // إحصائيات تفصيلية لدرج الكاشير
    private int _drawerItemsSoldCount;
    public int DrawerItemsSoldCount
    {
        get => _drawerItemsSoldCount;
        set => SetProperty(ref _drawerItemsSoldCount, value);
    }

    private decimal _drawerGrossSales;
    public decimal DrawerGrossSales
    {
        get => _drawerGrossSales;
        set
        {
            if (SetProperty(ref _drawerGrossSales, value))
            {
                OnPropertyChanged(nameof(DrawerNetSales));
            }
        }
    }

    private decimal _drawerReturnsAmount;
    public decimal DrawerReturnsAmount
    {
        get => _drawerReturnsAmount;
        set
        {
            if (SetProperty(ref _drawerReturnsAmount, value))
            {
                OnPropertyChanged(nameof(DrawerNetSales));
            }
        }
    }

    public decimal DrawerNetSales => Math.Max(0, DrawerGrossSales - DrawerReturnsAmount);

    public decimal DrawerCurrentCash => Math.Max(0, DrawerOpeningBalance + DrawerCashSales + DrawerDeposits - DrawerWithdrawals - DrawerReturnsAmount);

    public ObservableCollection<CashDrawerMovement> DrawerMovements { get; } = new();
    public ObservableCollection<CashierLiveTransactionItem> LiveTransactionsList { get; } = new();
    private readonly List<CashierLiveTransactionItem> _allLiveEventsCache = new();

    private string _activeTransactionFilter = "All";
    public string ActiveTransactionFilter
    {
        get => _activeTransactionFilter;
        set
        {
            if (SetProperty(ref _activeTransactionFilter, value))
            {
                OnPropertyChanged(nameof(IsAllFilterActive));
                OnPropertyChanged(nameof(IsSalesFilterActive));
                OnPropertyChanged(nameof(IsReturnsFilterActive));
                OnPropertyChanged(nameof(IsAdjustmentsFilterActive));
                ApplyTransactionFilter();
            }
        }
    }

    public bool IsAllFilterActive => ActiveTransactionFilter == "All";
    public bool IsSalesFilterActive => ActiveTransactionFilter == "Sales";
    public bool IsReturnsFilterActive => ActiveTransactionFilter == "Returns";
    public bool IsAdjustmentsFilterActive => ActiveTransactionFilter == "Adjustments";

    private void ApplyTransactionFilter()
    {
        LiveTransactionsList.Clear();
        var items = ActiveTransactionFilter switch
        {
            "Sales" => _allLiveEventsCache.Where(x => x.TransactionType == "Sale"),
            "Returns" => _allLiveEventsCache.Where(x => x.TransactionType == "Return"),
            "Adjustments" => _allLiveEventsCache.Where(x => x.TransactionType == "Deposit" || x.TransactionType == "Withdrawal"),
            _ => _allLiveEventsCache
        };

        foreach (var item in items)
        {
            LiveTransactionsList.Add(item);
        }
    }

    private bool _isEditingOpeningBalance;
    public bool IsEditingOpeningBalance
    {
        get => _isEditingOpeningBalance;
        set => SetProperty(ref _isEditingOpeningBalance, value);
    }

    private string _openingBalanceInputText = "0";
    public string OpeningBalanceInputText
    {
        get => _openingBalanceInputText;
        set => SetProperty(ref _openingBalanceInputText, value);
    }

    private bool _isDrawerActionPopupOpen;
    public bool IsDrawerActionPopupOpen
    {
        get => _isDrawerActionPopupOpen;
        set => SetProperty(ref _isDrawerActionPopupOpen, value);
    }

    private string _drawerActionType = "Deposit"; // "Deposit" or "Withdrawal"
    public string DrawerActionType
    {
        get => _drawerActionType;
        set
        {
            if (SetProperty(ref _drawerActionType, value))
            {
                OnPropertyChanged(nameof(DrawerActionTitle));
            }
        }
    }

    public string DrawerActionTitle => DrawerActionType == "Withdrawal" ? (Loc.IsKurdish ? "📤 راکێشانی پارە لە سندووق (سحب)" : "📤 سحب مال من الدرج") : (Loc.IsKurdish ? "📥 دانانی پارە لە سندووق (إيداع)" : "📥 إيداع مال في الدرج");

    private string _drawerInputAmount = string.Empty;
    public string DrawerInputAmount
    {
        get => _drawerInputAmount;
        set => SetProperty(ref _drawerInputAmount, value);
    }

    private string _drawerInputReason = string.Empty;
    public string DrawerInputReason
    {
        get => _drawerInputReason;
        set => SetProperty(ref _drawerInputReason, value);
    }

    #region Cash Movement Voucher (سند حركة الصندوق - قبض وصرف)

    private CashDrawerMovement? _selectedCashMovementVoucher;
    public CashDrawerMovement? SelectedCashMovementVoucher
    {
        get => _selectedCashMovementVoucher;
        set
        {
            if (SetProperty(ref _selectedCashMovementVoucher, value))
            {
                OnPropertyChanged(nameof(CashMovementVoucherTitle));
                OnPropertyChanged(nameof(CashMovementVoucherBadgeBg));
                OnPropertyChanged(nameof(CashMovementVoucherBadgeFg));
            }
        }
    }

    private bool _isCashMovementVoucherOpen;
    public bool IsCashMovementVoucherOpen
    {
        get => _isCashMovementVoucherOpen;
        set => SetProperty(ref _isCashMovementVoucherOpen, value);
    }

    public string CashMovementVoucherTitle => SelectedCashMovementVoucher?.MovementType == "Withdrawal"
        ? (Loc.IsKurdish ? "📤 سەنەدی راکێشانی نەقد لە سندووق (سند صرف)" : "📤 سند سحب نقد من الصندوق (سند صرف)")
        : (Loc.IsKurdish ? "📥 سەنەدی دانانی نەقد لە سندووق (سند قبض)" : "📥 سند إيداع نقد في الصندوق (سند قبض)");

    public string CashMovementVoucherBadgeBg => SelectedCashMovementVoucher?.MovementType == "Withdrawal" ? "#7F1D1D" : "#064E3B";
    public string CashMovementVoucherBadgeFg => SelectedCashMovementVoucher?.MovementType == "Withdrawal" ? "#FECACA" : "#34D399";

    public ICommand CloseCashMovementVoucherCommand { get; }
    public ICommand PrintCashMovementVoucherCommand { get; }
    public ICommand OpenMovementVoucherFromItemCommand { get; }

    #endregion

    #region Direct Item Return Modal Properties & Collections

    private bool _isDirectReturnModalOpen;
    public bool IsDirectReturnModalOpen
    {
        get => _isDirectReturnModalOpen;
        set => SetProperty(ref _isDirectReturnModalOpen, value);
    }

    private string _directReturnSearchQuery = string.Empty;
    public string DirectReturnSearchQuery
    {
        get => _directReturnSearchQuery;
        set => SetProperty(ref _directReturnSearchQuery, value);
    }

    private string _directReturnActiveTab = "Direct";
    public string DirectReturnActiveTab
    {
        get => _directReturnActiveTab;
        set
        {
            if (SetProperty(ref _directReturnActiveTab, value))
            {
                OnPropertyChanged(nameof(IsDirectReturnTabActive));
                OnPropertyChanged(nameof(IsInvoicesReturnTabActive));
                OnPropertyChanged(nameof(IsAnalyticsReturnTabActive));
            }
        }
    }

    public bool IsDirectReturnTabActive => DirectReturnActiveTab == "Direct";
    public bool IsInvoicesReturnTabActive => DirectReturnActiveTab == "Invoices";
    public bool IsAnalyticsReturnTabActive => DirectReturnActiveTab == "Analytics";

    public ObservableCollection<DirectReturnItemViewModel> DirectReturnItems { get; } = new();

    public decimal DirectReturnGrandTotal => DirectReturnItems.Sum(x => x.TotalPrice);
    public int DirectReturnItemsCount => DirectReturnItems.Count;
    public bool HasDirectReturnItems => DirectReturnItems.Count > 0;

    public void NotifyDirectReturnChanged()
    {
        OnPropertyChanged(nameof(DirectReturnGrandTotal));
        OnPropertyChanged(nameof(DirectReturnItemsCount));
        OnPropertyChanged(nameof(HasDirectReturnItems));
    }

    #endregion

    #endregion

    #region Commands

    public ICommand SelectInvoiceTabCommand { get; }
    public ICommand AddNewInvoiceTabCommand { get; }
    public ICommand CloseInvoiceTabCommand { get; }

    public ICommand BarcodeScannedCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand ClearCartCommand { get; }

    public ICommand CheckoutCashCommand { get; }
    public ICommand CheckoutCardCommand { get; }
    public ICommand OpenDrawerCommand { get; }
    public ICommand CloseDrawerModalCommand { get; }
    public ICommand OpenAddCashPopupCommand { get; }
    public ICommand OpenTakeCashPopupCommand { get; }
    public ICommand ConfirmDrawerActionCommand { get; }
    public ICommand CloseDrawerActionPopupCommand { get; }
    public ICommand OpenEditOpeningBalanceCommand { get; }
    public ICommand SaveOpeningBalanceCommand { get; }
    public ICommand KickDrawerHardwareCommand { get; }
    public ICommand PrintDrawerReportCommand { get; }

    public ICommand OpenWarehouseModalCommand { get; }
    public ICommand CloseWarehouseModalCommand { get; }
    public ICommand AddRetailProductCommand { get; }
    public ICommand AddWholesaleProductCommand { get; }
    public ICommand AddCartonProductCommand { get; }

    public ICommand OpenSalesHistoryModalCommand { get; }
    public ICommand CloseSalesHistoryModalCommand { get; }
    public ICommand ReturnSaleInvoiceCommand { get; }

    public ICommand OpenInvoiceDetailsCommand { get; }
    public ICommand CloseInvoiceDetailsCommand { get; }

    public ICommand BackToMainCommand { get; }
    public ICommand ToggleReturnModeCommand { get; }
    public ICommand PrintShiftReportCommand { get; }
    public ICommand EndCashierShiftCommand { get; }
    public ICommand SetTransactionFilterCommand { get; }
    public ICommand SetSalesHistoryFilterCommand { get; }
    public ICommand PrintSelectedInvoiceCommand { get; }

    // Direct Return Commands
    public ICommand OpenDirectReturnModalCommand { get; }
    public ICommand CloseDirectReturnModalCommand { get; }
    public ICommand DirectReturnScanBarcodeCommand { get; }
    public ICommand DirectReturnIncreaseQtyCommand { get; }
    public ICommand DirectReturnDecreaseQtyCommand { get; }
    public ICommand DirectReturnRemoveItemCommand { get; }
    public ICommand ConfirmDirectReturnCommand { get; }
    public ICommand PrintDirectReturnReceiptCommand { get; }
    public ICommand SetDirectReturnTabCommand { get; }

    public event Action? SaleCompleted;
    public event Action? RequestBackToNavigation;
    public event Action? RequestFocusBarcodeField;
    public event Action? RequestFocusDirectReturnBarcode;
    public event Action? RequestFocusWarehouseSearch;

    #endregion

    public MainCashierViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);
        _saleService = new SaleService(_context);

        ThemeManager.Instance.PropertyChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(BarcodeBackgroundBrush));
            OnPropertyChanged(nameof(BarcodeBorderBrush));
            OnPropertyChanged(nameof(BarcodeScannerIconColor));
        };

        ToggleReturnModeCommand = new RelayCommand(() =>
        {
            IsReturnModeActive = !IsReturnModeActive;
            RequestFocusBarcodeField?.Invoke();
        });

        // إنشاء أول نافذة بيع (فاتورة #1)
        var firstTab = new InvoiceTabViewModel(1) { IsSelected = true };
        InvoiceTabs.Add(firstTab);
        _selectedTab = firstTab;

        // اختيار تبويب الفاتورة
        SelectInvoiceTabCommand = new RelayCommand(param =>
        {
            if (param is InvoiceTabViewModel tab)
            {
                SelectedTab = tab;
                StatusMessage = $"أنت الآن في {tab.Title}";
            }
        });

        // إدارة نوافذ البيع المتعددة
        AddNewInvoiceTabCommand = new RelayCommand(() =>
        {
            int nextIndex = InvoiceTabs.Count + 1;
            var newTab = new InvoiceTabViewModel(nextIndex);
            InvoiceTabs.Add(newTab);
            SelectedTab = newTab;
            ReIndexTabs();
            StatusMessage = $"تم فتح {newTab.Title}";
        });

        CloseInvoiceTabCommand = new RelayCommand((param) =>
        {
            if (param is InvoiceTabViewModel tab)
            {
                if (InvoiceTabs.Count > 1)
                {
                    int idx = InvoiceTabs.IndexOf(tab);
                    InvoiceTabs.Remove(tab);
                    ReIndexTabs();
                    SelectedTab = InvoiceTabs[Math.Min(idx, InvoiceTabs.Count - 1)];
                }
                else
                {
                    tab.CartItems.Clear();
                    tab.DiscountInputText = string.Empty;
                    tab.RecalculateTotals();
                    StatusMessage = "تم تفريغ الفاتورة.";
                }
                _ = FilterWarehouseProductsAsync();
            }
        });

        // مسح الباركود وإضافة المادة لأول القائمة (يدعم البيع العادي ووضع الإرجاع)
        BarcodeScannedCommand = new AsyncRelayCommand(async () =>
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string clean = SearchQuery.Trim();
                var product = await _productService.GetProductByBarcodeAsync(clean);
                if (product != null)
                {
                    string targetSaleType = IsReturnModeActive ? "إرجاع" : "مفرد";
                    AddProductToCart(product, targetSaleType);
                    SearchQuery = string.Empty;
                    StatusMessage = IsReturnModeActive 
                        ? (Loc.IsKurdish ? $"کاڵای گەڕاوە تۆمارکرا: {product.Name}" : $"تم تسجيل إرجاع مادة: {product.Name}")
                        : (Loc.IsKurdish ? $"کاڵا زیادکرا: {product.Name}" : $"تمت الإضافة: {product.Name}");
                    TriggerBarcodeFeedback(BarcodeFeedbackState.Success);
                }
                else
                {
                    SearchQuery = string.Empty;
                    StatusMessage = $"الباركود {clean} غير مسجل.";
                    TriggerBarcodeFeedback(BarcodeFeedbackState.Error);
                }
            }
            RequestFocusBarcodeField?.Invoke();
        });

        // زيادة أو نقص كمية المادة
        IncreaseQuantityCommand = new RelayCommand(param =>
        {
            if (param is CartItemViewModel item)
            {
                item.Quantity += 1;
                SelectedTab?.RecalculateTotals();
                _ = FilterWarehouseProductsAsync();
            }
        });

        DecreaseQuantityCommand = new RelayCommand(param =>
        {
            if (param is CartItemViewModel item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity -= 1;
                }
                else
                {
                    SelectedTab?.CartItems.Remove(item);
                }
                SelectedTab?.RecalculateTotals();
                _ = FilterWarehouseProductsAsync();
            }
        });

        RemoveItemCommand = new RelayCommand(param =>
        {
            if (param is CartItemViewModel item)
            {
                SelectedTab?.CartItems.Remove(item);
                SelectedTab?.RecalculateTotals();
                _ = FilterWarehouseProductsAsync();
            }
        });

        ClearCartCommand = new RelayCommand(() =>
        {
            if (SelectedTab != null && SelectedTab.CartItems.Count > 0)
            {
                SelectedTab.CartItems.Clear();
                SelectedTab.DiscountInputText = string.Empty;
                SelectedTab.RecalculateTotals();
                _ = FilterWarehouseProductsAsync();
                StatusMessage = "تم إفراغ السلة.";
            }
        });

        CheckoutCashCommand = new AsyncRelayCommand(async () => await ProcessCheckoutAsync("Cash"));
        CheckoutCardCommand = new AsyncRelayCommand(async () => await ProcessCheckoutAsync("Card"));

        // فتح نافذة إدارة الدرج والصندوق
        OpenDrawerCommand = new AsyncRelayCommand(async () =>
        {
            CashDrawerService.OpenViaPrinter("POS-80");
            await LoadDrawerCashDataAsync();
            IsDrawerModalOpen = true;
        });

        CloseDrawerModalCommand = new RelayCommand(() =>
        {
            IsDrawerModalOpen = false;
        });

        OpenAddCashPopupCommand = new RelayCommand(() =>
        {
            DrawerActionType = "Deposit";
            DrawerInputAmount = string.Empty;
            DrawerInputReason = string.Empty;
            IsDrawerActionPopupOpen = true;
        });

        OpenTakeCashPopupCommand = new RelayCommand(() =>
        {
            DrawerActionType = "Withdrawal";
            DrawerInputAmount = string.Empty;
            DrawerInputReason = string.Empty;
            IsDrawerActionPopupOpen = true;
        });

        CloseDrawerActionPopupCommand = new RelayCommand(() =>
        {
            IsDrawerActionPopupOpen = false;
        });

        ConfirmDrawerActionCommand = new AsyncRelayCommand(async () =>
        {
            decimal amt = ParseDecimalSafe(DrawerInputAmount);
            if (amt > 0)
            {
                var user = _context.Users.FirstOrDefault();
                string cName = user?.FullName ?? (Loc.IsKurdish ? "محەمەد کاشێر" : "محمد الكاشير");
                string defaultReason = DrawerActionType == "Withdrawal"
                    ? (Loc.IsKurdish ? "راکێشانی پارە لە سندووق" : "سحب نقدي من الدرج")
                    : (Loc.IsKurdish ? "دانانی پارە لە سندووق" : "إيداع نقدي في الدرج");

                var mov = new CashDrawerMovement
                {
                    Id = Guid.NewGuid(),
                    CashierName = cName,
                    MovementType = DrawerActionType,
                    Amount = amt,
                    Reason = string.IsNullOrWhiteSpace(DrawerInputReason) ? defaultReason : DrawerInputReason.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.CashDrawerMovements.Add(mov);
                await _context.SaveChangesAsync();

                IsDrawerActionPopupOpen = false;
                DrawerInputAmount = string.Empty;
                DrawerInputReason = string.Empty;

                await LoadDrawerCashDataAsync();
                await LoadSalesHistoryArchiveAsync();

                // فتح سند الحركة النقدية فوراً لرؤية التفاصيل وطباعة الوصل
                SelectedCashMovementVoucher = mov;
                IsCashMovementVoucherOpen = true;
            }
            else
            {
                MessageBox.Show(Loc.IsKurdish ? "تکایە بڕی پارەی دروست بنووسە" : "يرجى كتابة مبلغ صحيح أكبر من الصفر", Loc.IsKurdish ? "هەڵە" : "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });

        CloseCashMovementVoucherCommand = new RelayCommand(() =>
        {
            IsCashMovementVoucherOpen = false;
        });

        PrintCashMovementVoucherCommand = new RelayCommand(() =>
        {
            if (SelectedCashMovementVoucher != null)
            {
                PrintCashMovementVoucher(SelectedCashMovementVoucher);
            }
        });

        OpenMovementVoucherFromItemCommand = new RelayCommand((param) =>
        {
            if (param is CashierLiveTransactionItem item && item.AssociatedMovement != null)
            {
                SelectedCashMovementVoucher = item.AssociatedMovement;
                IsCashMovementVoucherOpen = true;
            }
            else if (param is CashDrawerMovement mov)
            {
                SelectedCashMovementVoucher = mov;
                IsCashMovementVoucherOpen = true;
            }
        });

        OpenEditOpeningBalanceCommand = new RelayCommand(() =>
        {
            OpeningBalanceInputText = DrawerOpeningBalance.ToString("0");
            IsEditingOpeningBalance = true;
        });

        SaveOpeningBalanceCommand = new RelayCommand(() =>
        {
            decimal bal = ParseDecimalSafe(OpeningBalanceInputText);
            if (bal >= 0)
            {
                DrawerOpeningBalance = bal;
                IsEditingOpeningBalance = false;
                StatusMessage = Loc.IsKurdish ? $"پارەی دەستپێک دیاریکرا: {bal:N0} د.ع" : $"تم تعيين الرصيد الافتتاحي: {bal:N0} د.ع";
                _ = LoadDrawerCashDataAsync();
            }
            else
            {
                MessageBox.Show(Loc.IsKurdish ? "تکایە بڕی پارەی دروست بنووسە" : "يرجى كتابة مبلغ صحيح", Loc.IsKurdish ? "هەڵە" : "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });

        KickDrawerHardwareCommand = new RelayCommand(() =>
        {
            CashDrawerService.OpenViaPrinter("POS-80");
            MessageBox.Show(Loc.IsKurdish ? "فەرمانی کردنەوەی سندووق نێردرا" : "تم إرسال نبضة فتح الدرج الإلكتروني بنجاح", Loc.IsKurdish ? "سندووق" : "الدرج", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        PrintDrawerReportCommand = new RelayCommand(() =>
        {
            MessageBox.Show(Loc.IsKurdish ? $"راپۆرتی خەزێنە:\nپارەی دەستپێک: {DrawerOpeningBalance:N0} د.ع\nفرۆشتنی نەقد: {DrawerCashSales:N0} د.ع\nپارەی زیادکراو: {DrawerDeposits:N0} د.ع\nپارەی راکێشراو: {DrawerWithdrawals:N0} د.ع\nکۆی گشتی لە سندووق: {DrawerCurrentCash:N0} د.ع" : $"تقرير الصندوق والدرج:\nالرصيد الافتتاحي: {DrawerOpeningBalance:N0} د.ع\nالمبيعات النقدية: {DrawerCashSales:N0} د.ع\nإجمالي الإيداعات: {DrawerDeposits:N0} د.ع\nإجمالي المسحوبات: {DrawerWithdrawals:N0} د.ع\nالرصيد الفعلي بالدرج: {DrawerCurrentCash:N0} د.ع", Loc.IsKurdish ? "راپۆرتی سندووق" : "تقرير الدرج", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        // نافذة المخزن المنبثقة: تصفير البحث دائماً لعرض كل المواد عند الفتح
        OpenWarehouseModalCommand = new AsyncRelayCommand(async () =>
        {
            WarehouseSearchQuery = string.Empty;
            IsWarehouseModalOpen = true;
            await FilterWarehouseProductsAsync();
        });

        CloseWarehouseModalCommand = new RelayCommand(() =>
        {
            IsWarehouseModalOpen = false;
        });

        AddRetailProductCommand = new RelayCommand((param) =>
        {
            if (param is Product p)
            {
                string targetType = IsReturnModeActive ? "إرجاع" : "مفرد";
                AddProductToCart(p, targetType);
                StatusMessage = IsReturnModeActive
                    ? (Loc.IsKurdish ? $"کاڵای گەڕاوە تۆمارکرا: '{p.Name}'" : $"تم تسجيل إرجاع مادة: '{p.Name}'")
                    : $"تمت إضافة '{p.Name}' بسعر المفرد ({p.Price:N0} د.ع)";
            }
        });

        AddWholesaleProductCommand = new RelayCommand((param) =>
        {
            if (param is Product p)
            {
                string targetType = IsReturnModeActive ? "إرجاع" : "جملة";
                AddProductToCart(p, targetType);
                StatusMessage = IsReturnModeActive
                    ? (Loc.IsKurdish ? $"کاڵای گەڕاوە تۆمارکرا: '{p.Name}'" : $"تم تسجيل إرجاع مادة: '{p.Name}'")
                    : $"تمت إضافة '{p.Name}' بسعر الجملة ({p.WholesalePrice:N0} د.ع)";
            }
        });

        AddCartonProductCommand = new RelayCommand((param) =>
        {
            if (param is Product p)
            {
                string targetType = IsReturnModeActive ? "إرجاع" : "كرتون";
                AddProductToCart(p, targetType);
                StatusMessage = IsReturnModeActive
                    ? (Loc.IsKurdish ? $"کاڵای گەڕاوە تۆمارکرا: '{p.Name}'" : $"تم تسجيل إرجاع مادة: '{p.Name}'")
                    : (p.CartonSellingPrice > 0
                        ? $"تمت إضافة '{p.Name}' بسعر الكرتون ({p.CartonSellingPrice:N0} د.ع)"
                        : $"تمت إضافة '{p.Name}' بالكرتون (سعر الكرتون 0 د.ع - يرجى تحديد السعر)");
            }
        });

        // نافذة سجل الوصلات والورديات - تفتح دائماً فوراً
        OpenSalesHistoryModalCommand = new AsyncRelayCommand(async () =>
        {
            IsSalesHistoryModalOpen = true;
            try
            {
                await LoadSalesHistoryArchiveAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSalesHistoryArchiveAsync error: {ex.Message}");
            }
        });

        CloseSalesHistoryModalCommand = new RelayCommand(() =>
        {
            IsSalesHistoryModalOpen = false;
        });

        ReturnSaleInvoiceCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is Sale sale)
            {
                if (sale.Status == "Returned")
                {
                    MessageBox.Show("هذا الوصل مسترجع مسبقاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var res = MessageBox.Show($"هل ترغب في استرجاع الوصل رقم '{sale.InvoiceNumber}' بقيمة {sale.TotalAmount:N0} د.ع وإعادة المواد للمخزن؟",
                                          "تأكيد استرجاع الوصل", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    bool returned = await _saleService.ReturnSaleAsync(sale.Id);
                    if (returned)
                    {
                        await LoadSalesHistoryArchiveAsync();
                        await LoadTodayStatsAsync();
                        SaleCompleted?.Invoke();
                        MessageBox.Show($"تم استرجاع الوصل '{sale.InvoiceNumber}' بنجاح وإعادة الكميات للمخزن.", "تم الاسترجاع", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        });

        // تفاصيل الوصل كاملاً
        OpenInvoiceDetailsCommand = new RelayCommand((param) =>
        {
            if (param is Sale sale)
            {
                SelectedInvoiceForDetails = sale;
                IsInvoiceDetailsModalOpen = true;
            }
        });

        CloseInvoiceDetailsCommand = new RelayCommand(() =>
        {
            IsInvoiceDetailsModalOpen = false;
        });

        SetTransactionFilterCommand = new RelayCommand(param =>
        {
            ActiveTransactionFilter = param?.ToString() ?? "All";
        });

        SetSalesHistoryFilterCommand = new RelayCommand(param =>
        {
            ActiveSalesHistoryFilter = param?.ToString() ?? "All";
        });

        PrintSelectedInvoiceCommand = new RelayCommand(() =>
        {
            if (SelectedInvoiceForDetails != null)
            {
                try
                {
                    PrintDialog printDialog = new PrintDialog();
                    if (printDialog.ShowDialog() == true)
                    {
                        FlowDocument doc = CreateSingleInvoiceReceiptFlowDocument(SelectedInvoiceForDetails, printDialog.PrintableAreaWidth);
                        IDocumentPaginatorSource idpSource = doc;
                        printDialog.PrintDocument(idpSource.DocumentPaginator, $"وصل مبيعات {SelectedInvoiceForDetails.InvoiceNumber}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء طباعة الوصل: {ex.Message}", "خطأ في الطباعة", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        });

        PrintShiftReportCommand = new RelayCommand(() => PrintShiftReport());

        EndCashierShiftCommand = new AsyncRelayCommand(async () => await EndCashierShiftAsync());

        // Direct Return Commands
        OpenDirectReturnModalCommand = new RelayCommand(() =>
        {
            DirectReturnItems.Clear();
            DirectReturnSearchQuery = string.Empty;
            DirectReturnActiveTab = "Direct";
            NotifyDirectReturnChanged();
            IsDirectReturnModalOpen = true;
            RequestFocusDirectReturnBarcode?.Invoke();
        });

        CloseDirectReturnModalCommand = new RelayCommand(() =>
        {
            IsDirectReturnModalOpen = false;
        });

        SetDirectReturnTabCommand = new RelayCommand(param =>
        {
            string tab = param?.ToString() ?? "Direct";
            DirectReturnActiveTab = tab;
            if (tab == "Invoices")
            {
                IsDirectReturnModalOpen = false;
                IsSalesHistoryModalOpen = true;
            }
        });

        DirectReturnScanBarcodeCommand = new AsyncRelayCommand(async () => await ProcessDirectReturnScanAsync());

        DirectReturnIncreaseQtyCommand = new RelayCommand(param =>
        {
            if (param is DirectReturnItemViewModel item)
            {
                item.Quantity++;
                NotifyDirectReturnChanged();
            }
        });

        DirectReturnDecreaseQtyCommand = new RelayCommand(param =>
        {
            if (param is DirectReturnItemViewModel item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                }
                else
                {
                    DirectReturnItems.Remove(item);
                }
                NotifyDirectReturnChanged();
            }
        });

        DirectReturnRemoveItemCommand = new RelayCommand(param =>
        {
            if (param is DirectReturnItemViewModel item)
            {
                DirectReturnItems.Remove(item);
                NotifyDirectReturnChanged();
            }
        });

        ConfirmDirectReturnCommand = new AsyncRelayCommand(async () => await ConfirmDirectReturnAsync());

        PrintDirectReturnReceiptCommand = new RelayCommand(() => PrintDirectReturnReceipt());

        BackToMainCommand = new RelayCommand(() => RequestBackToNavigation?.Invoke());
    }

    public async Task InitializeAsync()
    {
        IsWarehouseModalOpen = false;
        IsSalesHistoryModalOpen = false;
        IsInvoiceDetailsModalOpen = false;
        await FilterProductsAsync();
        await LoadTodayStatsAsync();
        RequestFocusBarcodeField?.Invoke();
    }

    private void ReIndexTabs()
    {
        for (int i = 0; i < InvoiceTabs.Count; i++)
        {
            InvoiceTabs[i].TabIndex = i + 1;
        }
    }

    public void AddProductToCart(Product product, string saleType = "مفرد")
    {
        if (SelectedTab == null)
            return;

        // إزالة حالة "جديد" من بقية المواد السابقة
        foreach (var item in SelectedTab.CartItems)
        {
            item.IsNewlyAdded = false;
        }

        var existing = SelectedTab.CartItems.FirstOrDefault(i => 
            ((i.ProductId.HasValue && product.Id != Guid.Empty && i.ProductId == product.Id) || 
             (!string.IsNullOrEmpty(i.Barcode) && !string.IsNullOrEmpty(product.Barcode) && i.Barcode == product.Barcode)) 
            && i.SaleType == saleType);

        if (existing != null)
        {
            existing.Quantity += 1;
            existing.IsNewlyAdded = true;

            if (existing.IsBelowCost)
            {
                StatusMessage = $"⚠️ إنذار: سعر البيع لمادة ({existing.ProductName}) أقل من سعر التكلفة!";
            }
        }
        else
        {
            var newItem = new CartItemViewModel(product, 1, saleType)
            {
                IsNewlyAdded = true
            };
            
            newItem.SaleTypeChanged += () =>
            {
                ConsolidateCartItems(SelectedTab);
            };

            SelectedTab.CartItems.Insert(0, newItem);

            if (newItem.IsBelowCost)
            {
                StatusMessage = $"⚠️ إنذار: سعر البيع لمادة ({newItem.ProductName}) أقل من سعر التكلفة!";
            }
        }

        SelectedTab.RecalculateTotals();
        _ = FilterWarehouseProductsAsync();
    }

    private void ConsolidateCartItems(InvoiceTabViewModel? tab)
    {
        if (tab == null) return;

        for (int i = 0; i < tab.CartItems.Count; i++)
        {
            var current = tab.CartItems[i];
            for (int j = tab.CartItems.Count - 1; j > i; j--)
            {
                var other = tab.CartItems[j];
                bool isSameProduct = (current.ProductId.HasValue && other.ProductId.HasValue && current.ProductId == other.ProductId) ||
                                     (!string.IsNullOrEmpty(current.Barcode) && !string.IsNullOrEmpty(other.Barcode) && current.Barcode == other.Barcode);

                if (isSameProduct && other.SaleType == current.SaleType && other.UnitPrice == current.UnitPrice)
                {
                    current.Quantity += other.Quantity;
                    tab.CartItems.RemoveAt(j);
                }
            }
        }

        tab.RecalculateTotals();
        _ = FilterWarehouseProductsAsync();
    }

    public async Task FilterProductsAsync()
    {
        var list = await _productService.GetProductsAsync(null, null, 100);
        Products.Clear();
        foreach (var p in list) Products.Add(p);
    }

    public async Task FilterWarehouseProductsAsync()
    {
        var list = await _productService.GetAllProductsListAsync(WarehouseSearchQuery);
        
        // الخصم اللحظي لكميات المواد والكراتين الموجودة في السلة الحالية
        WarehouseProductsList.Clear();
        foreach (var p in list)
        {
            decimal cartPieces = 0;
            if (SelectedTab != null)
            {
                foreach (var itm in SelectedTab.CartItems.Where(x => x.ProductId == p.Id))
                {
                    if (itm.SaleType == "كرتون")
                    {
                        decimal ipc = p.ItemsPerCarton > 0 ? p.ItemsPerCarton : 1;
                        cartPieces += (itm.Quantity * ipc);
                    }
                    else
                    {
                        cartPieces += itm.Quantity;
                    }
                }
            }

            // إنشاء نسخة معروضة تحسب المخزون اللحظي المتبقي
            var displayProduct = new Product
            {
                Id = p.Id,
                Barcode = p.Barcode,
                Name = p.Name,
                SupplierName = p.SupplierName,
                Price = p.Price,
                WholesalePrice = p.WholesalePrice,
                CartonSellingPrice = p.CartonSellingPrice,
                Cost = p.Cost,
                CartonPurchasePrice = p.CartonPurchasePrice,
                ItemsPerCarton = p.ItemsPerCarton,
                StockQuantity = Math.Max(0, p.StockQuantity - cartPieces),
                CartonsCount = p.ItemsPerCarton > 0 ? (int)(Math.Max(0, p.StockQuantity - cartPieces) / p.ItemsPerCarton) : p.CartonsCount
            };

            WarehouseProductsList.Add(displayProduct);
        }
    }

    public async Task LoadSalesHistoryArchiveAsync()
    {
        try
        {
            var todayStart = DateTime.Today.ToUniversalTime();
            var list = await _context.Sales
                .Include(s => s.Items)
                .Include(s => s.User)
                .AsNoTracking()
                .Where(s => s.CreatedAt >= todayStart)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            if (list.Count == 0)
            {
                // إذا لم تكن هناك مبيعات اليوم، جلب آخر 50 فاتورة سابقة لتمكين الكاشير من استعراض الفواتير والطباعة والإرجاع
                list = await _context.Sales
                    .Include(s => s.Items)
                    .Include(s => s.User)
                    .AsNoTracking()
                    .OrderByDescending(s => s.CreatedAt)
                    .Take(50)
                    .ToListAsync();
            }

            _allSalesHistoryCache.Clear();
            _allSalesHistoryCache.AddRange(list);
            ApplySalesHistoryFilter();

            var completed = list.Where(s => s.Status == "Completed").ToList();
            var returned = list.Where(s => s.Status == "Returned").ToList();

            ShiftGrossSales = completed.Sum(s => s.TotalAmount);
            ShiftReturnsAmount = returned.Sum(s => s.TotalAmount) + completed.SelectMany(s => s.Items).Where(i => i.TotalPrice < 0).Sum(i => Math.Abs(i.TotalPrice));
            ShiftInvoicesCount = completed.Count;
            ShiftReturnsCount = returned.Count;

            OnPropertyChanged(nameof(ShiftNetSales));
            OnPropertyChanged(nameof(HasNoSalesHistory));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadSalesHistoryArchiveAsync error: {ex.Message}");
            _allSalesHistoryCache.Clear();
            ApplySalesHistoryFilter();
        }
    }

    public bool HasNoSalesHistory => SalesHistoryList.Count == 0;

    private FlowDocument CreateShiftReceiptFlowDocument(double printableWidth)
    {
        FlowDocument doc = new FlowDocument
        {
            PageWidth = 280,
            ColumnWidth = 280,
            PagePadding = new Thickness(6, 4, 6, 4),
            FontFamily = new FontFamily("Segoe UI, Tahoma, Arial"),
            FlowDirection = FlowDirection.RightToLeft
        };

        var user = _context.Users.FirstOrDefault();
        string cashierName = user?.FullName ?? (Loc.IsKurdish ? "محەمەد کاشێر" : "محمد الكاشير");
        string nowStr = DateTime.Now.ToString("yyyy/MM/dd - hh:mm tt");
        bool isKu = Loc.IsKurdish;

        // Header Section
        Paragraph pHeader = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6),
            LineHeight = 16
        };
        pHeader.Inlines.Add(new Bold(new Run("⚡ 7AMO POS SYSTEM\n")) { FontSize = 15 });
        pHeader.Inlines.Add(new Bold(new Run(isKu ? "ڕاپۆرتی داخستنی نۆبەت (Z-Report)\n" : "تقرير إغلاق وردية الكاشير (Z-Report)\n")) { FontSize = 12 });
        pHeader.Inlines.Add(new Run("----------------------------------------\n") { Foreground = Brushes.Gray });
        pHeader.Inlines.Add(new Run($"👤 {(isKu ? "کاشێر:" : "الكاشير:")} {cashierName}\n") { FontSize = 10, FontWeight = FontWeights.Bold });
        pHeader.Inlines.Add(new Run($"⏰ {(isKu ? "کاتی داخستن:" : "وقت الإغلاق:")} {nowStr}\n") { FontSize = 9.5 });
        pHeader.Inlines.Add(new Run($"🏢 {(isKu ? "خاڵی فرۆشتن:" : "نقطة البيع:")} POS-01 (سەرەکی)\n") { FontSize = 9.5 });
        pHeader.Inlines.Add(new Run("----------------------------------------\n") { Foreground = Brushes.Gray });
        doc.Blocks.Add(pHeader);

        // Sales & Returns Summary Section
        Paragraph pSales = new Paragraph
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 0, 6),
            LineHeight = 17
        };
        pSales.Inlines.Add(new Bold(new Run(isKu ? "📊 پوختەی فرۆش و گەڕاوە:\n" : "📊 ملخص المبيعات والمرتجعات:\n")) { FontSize = 10.5 });
        pSales.Inlines.Add(new Run($"• {(isKu ? "کۆی گشتی فرۆش:" : "إجمالي المبيعات:")} {ShiftGrossSales:N0} د.ع\n") { FontSize = 10 });
        pSales.Inlines.Add(new Run($"• {(isKu ? "کۆی گشتی گەڕاوە:" : "إجمالي المرجوعات:")} -{ShiftReturnsAmount:N0} د.ع\n") { FontSize = 10, Foreground = Brushes.DarkRed });
        pSales.Inlines.Add(new Run("----------------------------------------\n") { Foreground = Brushes.Gray });
        pSales.Inlines.Add(new Bold(new Run($"💎 {(isKu ? "فرۆشی سافی:" : "صافي المبيعات:")} {ShiftNetSales:N0} د.ع\n")) { FontSize = 13, Foreground = Brushes.DarkGreen });
        pSales.Inlines.Add(new Run("========================================\n") { Foreground = Brushes.Gray });
        doc.Blocks.Add(pSales);

        // Payment Breakdown & Cash Float
        Paragraph pCash = new Paragraph
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 0, 6),
            LineHeight = 17
        };
        pCash.Inlines.Add(new Bold(new Run(isKu ? "💵 جووڵەی نەقد و حسابی سندووق:\n" : "💵 حركة النقد وحساب الدرج والخزنة:\n")) { FontSize = 10.5 });
        pCash.Inlines.Add(new Run($"• {(isKu ? "پارەی دەستپێکی سندووق:" : "الرصيد الافتتاحي:")} {DrawerOpeningBalance:N0} د.ع\n") { FontSize = 9.5 });
        pCash.Inlines.Add(new Run($"• {(isKu ? "فرۆشتنی نەقد (+):" : "المقبوضات النقدية (+):")} {DrawerCashSales:N0} د.ع\n") { FontSize = 9.5 });
        pCash.Inlines.Add(new Run($"• {(isKu ? "پارەی زیادکراو/ئیيداع (+):" : "إجمالي الإيداعات (+):")} {DrawerDeposits:N0} د.ع\n") { FontSize = 9.5 });
        pCash.Inlines.Add(new Run($"• {(isKu ? "پارەی راکێشراو/سحب (-):" : "إجمالي المسحوبات (-):")} -{DrawerWithdrawals:N0} د.ع\n") { FontSize = 9.5, Foreground = Brushes.DarkRed });
        pCash.Inlines.Add(new Run($"• {(isKu ? "پارەی گەڕاوە نەقد (-):" : "المرتجعات النقدية (-):")} -{DrawerReturnsAmount:N0} د.ع\n") { FontSize = 9.5, Foreground = Brushes.DarkRed });
        pCash.Inlines.Add(new Run("----------------------------------------\n") { Foreground = Brushes.Gray });
        pCash.Inlines.Add(new Bold(new Run($"💰 {(isKu ? "نەقدی ناو سندووق:" : "النقد الفعلي بالدرج:")} {DrawerCurrentCash:N0} د.ع\n")) { FontSize = 12.5, Foreground = Brushes.DarkGreen });
        pCash.Inlines.Add(new Run("----------------------------------------\n") { Foreground = Brushes.Gray });
        doc.Blocks.Add(pCash);

        // Transaction Counts & Signatures
        Paragraph pFooter = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4),
            LineHeight = 16
        };
        pFooter.Inlines.Add(new Run($"🧾 {(isKu ? "پسوولەی فرۆش:" : "فواتير البيع:")} {ShiftInvoicesCount} | {(isKu ? "پسوولەی گەڕاوە:" : "المرتجعات:")} {ShiftReturnsCount}\n") { FontSize = 9.5 });
        pFooter.Inlines.Add(new Run("========================================\n") { Foreground = Brushes.Gray });
        pFooter.Inlines.Add(new Run(isKu ? "واژۆی کاشێر: ................................\n\n" : "توقيع الكاشير: ................................\n\n") { FontSize = 9.5 });
        pFooter.Inlines.Add(new Run(isKu ? "واژۆی سەرپەرشتیار: ............................\n" : "توقيع المشرف:  ................................\n") { FontSize = 9.5 });
        pFooter.Inlines.Add(new Run("========================================\n") { Foreground = Brushes.Gray });
        pFooter.Inlines.Add(new Bold(new Run(isKu ? "نۆبەت بە سەرکەوتوویی داخرا ✔\n" : "تم تدقيق وإغلاق الوردية بنجاح ✔\n")) { FontSize = 10.5 });
        doc.Blocks.Add(pFooter);

        return doc;
    }

    private void PrintShiftReport()
    {
        try
        {
            // إرسال نبضة لفتح الدرج عند طباعة تقرير الوردية
            CashDrawerService.OpenViaPrinter("POS-80");

            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateShiftReceiptFlowDocument(printDialog.PrintableAreaWidth);
                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, $"تقرير وردية الكاشير - {DateTime.Now:yyyyMMdd-HHmm}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء طباعة تقرير الوردية: {ex.Message}", "خطأ في الطباعة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task EndCashierShiftAsync()
    {
        string confirmMsg = Loc.IsKurdish
            ? $"ئایا دڵنیایت لە کۆتاییهێنان بەم وردیە و داخستنی حسابات؟\n\n- کۆی گشتی فرۆش: {ShiftGrossSales:N0} د.ع\n- کۆی گەڕاوە: {ShiftReturnsAmount:N0} د.ع\n- فرۆشی سافی: {ShiftNetSales:N0} د.ع"
            : $"هل أنت متأكد من إنهاء الوردية الحالية وإغلاق حساب الكاشير؟\n\n- إجمالي المبيعات: {ShiftGrossSales:N0} د.ع\n- إجمالي المرجوعات: {ShiftReturnsAmount:N0} د.ع\n- صافي المبيعات: {ShiftNetSales:N0} د.ع";

        var res = MessageBox.Show(confirmMsg, Loc.IsKurdish ? "کۆتاییهێنان بە وردیە" : "إنهاء وقت الكاشير", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res == MessageBoxResult.Yes)
        {
            PrintShiftReport();
            IsSalesHistoryModalOpen = false;
            StatusMessage = Loc.IsKurdish ? "وردیەی کاشێر بەسەرکەوتوویی داخرا" : "تم إنهاء وردية الكاشير بنجاح";
            RequestBackToNavigation?.Invoke();
        }
    }

    private async Task ProcessCheckoutAsync(string paymentMethod)
    {
        if (SelectedTab == null || SelectedTab.CartItems.Count == 0)
        {
            MessageBox.Show("السلة فارغة، يرجى إضافة منتجات لإتمام البيع.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            RequestFocusBarcodeField?.Invoke();
            return;
        }

        // 1. فحص ما إذا كانت هناك مواد سعر بيعها 0 د.ع وتنبيه الكاشير لتعديل السعر
        var zeroPriceItems = SelectedTab.CartItems.Where(i => !i.IsReturn && i.UnitPrice <= 0).ToList();
        if (zeroPriceItems.Any())
        {
            string itemNames = string.Join("\n- ", zeroPriceItems.Select(x => $"{x.ProductName} ({x.SaleType})"));
            string msg = Loc.IsKurdish
                ? $"⚠️ ئاگاداری: کاڵاکانی خوارەوە لەناو سەبەتەدا نرخی فرۆشتنیان (0 د.ع) دانراوە:\n- {itemNames}\n\nتکایە پێش تەواوکردنی فرۆشتن نرخەکە چاک بکە."
                : $"⚠️ تنبيه: هناك مواد داخل السلة سعر بيعها (0 د.ع):\n- {itemNames}\n\nيرجى تعديل سعر البيع للمواد قبل إتمام عملية البيع.";
            MessageBox.Show(msg, Loc.IsKurdish ? "ئاگاداری نرخی 0" : "تنبيه سعر المادة 0", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 2. فحص ما إذا كان هناك مواد تباع بأقل من التكلفة وتنبيه الكاشير
        var lossItems = SelectedTab.CartItems.Where(i => i.IsBelowCost).ToList();
        if (lossItems.Any())
        {
            string itemNames = string.Join("\n- ", lossItems.Select(x => $"{x.ProductName} ({x.SaleType}): بيع {x.UnitPrice:N0} د.ع / تكلفة {(x.SaleType == "كرتون" ? x.CartonCost : x.PieceCost):N0} د.ع"));
            var res = MessageBox.Show($"⚠️ تحذير: المواد التالية تُباع بأقل من سعر التكلفة:\n- {itemNames}\n\nهل ترغب بالاستمرار وإتمام عملية البيع رغم الخسارة؟",
                                      "تنبيه بيع بخسارة", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            var user = _context.Users.FirstOrDefault();
            bool isReturnProcess = IsReturnModeActive || SelectedTab.CartItems.All(i => i.IsReturn) || SelectedTab.CartGrandTotal < 0;
            decimal finalAbsTotal = Math.Abs(SelectedTab.CartGrandTotal);

            var sale = new Sale
            {
                Id = Guid.NewGuid(),
                UserId = user?.Id,
                SubTotal = Math.Abs(SelectedTab.CartSubTotal),
                TaxAmount = SelectedTab.CartTaxTotal,
                DiscountAmount = SelectedTab.CartDiscountTotal,
                TotalAmount = finalAbsTotal,
                PaidAmount = finalAbsTotal,
                ChangeAmount = 0.0m,
                PaymentMethod = paymentMethod,
                Status = isReturnProcess ? "Returned" : "Completed",
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in SelectedTab.CartItems)
            {
                bool itemIsReturn = isReturnProcess || item.IsReturn;
                string pName = itemIsReturn && !item.ProductName.Contains("(إرجاع)") 
                    ? $"{item.ProductName} (إرجاع)" 
                    : $"{item.ProductName} ({item.SaleType})";

                sale.Items.Add(new SaleItem
                {
                    Id = Guid.NewGuid(),
                    SaleId = sale.Id,
                    ProductId = item.ProductId,
                    ProductName = pName,
                    Barcode = item.Barcode,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    DiscountAmount = item.DiscountAmount,
                    TaxRate = item.TaxRate,
                    TaxAmount = item.TaxAmount,
                    TotalPrice = itemIsReturn ? -Math.Abs(item.TotalPrice) : Math.Abs(item.TotalPrice)
                });
            }

            await _saleService.CompleteSaleAsync(sale);

            if (paymentMethod == "Cash")
            {
                CashDrawerService.OpenViaPrinter("POS-80");
            }

            string invoiceNum = sale.InvoiceNumber;
            decimal totalPaid = sale.TotalAmount;

            SelectedTab.CartItems.Clear();
            SelectedTab.DiscountInputText = string.Empty;
            SelectedTab.RecalculateTotals();
            await LoadTodayStatsAsync();
            await LoadDrawerCashDataAsync();
            await FilterProductsAsync();
            await FilterWarehouseProductsAsync();

            if (isReturnProcess)
            {
                StatusMessage = Loc.IsKurdish
                    ? $"پرۆسەی گەڕاندنەوە ئەنجامدرا: {invoiceNum} بە بڕی {totalPaid:N0} د.ع"
                    : $"تم تسجيل إرجاع الفاتورة {invoiceNum} بنجاح بقيمة مسترجعة {totalPaid:N0} د.ع";

                SaleCompleted?.Invoke();
                RequestFocusBarcodeField?.Invoke();

                string msg = Loc.IsKurdish
                    ? $"پرۆسەی گەڕاندنەوەی کاڵا بە سەرکەوتوویی تەواو بوو!\n\nژمارەی پسوولە: {invoiceNum}\nبڕی پارەی گەڕاوە بۆ کڕیار: {totalPaid:N0} د.ع\nشێوازی گەڕاندنەوە: {(paymentMethod == "Cash" ? "نەقد (Cash)" : "کارت (Card)")}"
                    : $"تمت عملية إرجاع المواد واسترداد المبلغ بنجاح!\n\nرقم الوصل: {invoiceNum}\nالمبلغ المسترجع للزبون: {totalPaid:N0} د.ع\nطريقة الاسترداد: {(paymentMethod == "Cash" ? "نقداً (Cash)" : "شبكة / بطاقة (Card)")}";

                MessageBox.Show(msg, Loc.IsKurdish ? "گەڕاندنەوەی کاڵا" : "إرجاع مواد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusMessage = $"تم إصدار الفاتورة {invoiceNum} بنجاح بقيمة {totalPaid:N0} د.ع ({paymentMethod})";
                SaleCompleted?.Invoke();
                RequestFocusBarcodeField?.Invoke();

                MessageBox.Show($"تم إتمام عملية البيع بنجاح!\n\nرقم الفاتورة: {invoiceNum}\nالمبلغ: {totalPaid:N0} د.ع\nطريقة الدفع: {(paymentMethod == "Cash" ? "نقدي (Cash)" : "شبكة / بطاقة (Card)")}",
                    "نجاح العملية", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            
            RequestFocusBarcodeField?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء حفظ الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            RequestFocusBarcodeField?.Invoke();
        }
    }

    private async Task LoadTodayStatsAsync()
    {
        var stats = await _saleService.GetTodayStatsAsync();
        TodaySalesCount = stats.TotalSalesCount;
        TodayRevenue = stats.TotalRevenue;
    }

    public async Task LoadDrawerCashDataAsync()
    {
        var todayStart = DateTime.Today.ToUniversalTime();
        var allTodaySales = await _context.Sales
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.CreatedAt >= todayStart)
            .ToListAsync();

        var completedSales = allTodaySales.Where(s => s.Status == "Completed").ToList();
        var returnedSales = allTodaySales.Where(s => s.Status == "Returned").ToList();

        // حساب عدد المواد المباعة، المبيعات الإجمالية، المرتجعات، وصافي المبيعات
        DrawerItemsSoldCount = (int)completedSales.SelectMany(s => s.Items).Where(i => i.Quantity > 0).Sum(i => i.Quantity);
        DrawerGrossSales = completedSales.Sum(s => s.TotalAmount);
        DrawerReturnsAmount = returnedSales.Sum(s => s.TotalAmount) + completedSales.SelectMany(s => s.Items).Where(i => i.TotalPrice < 0).Sum(i => Math.Abs(i.TotalPrice));
        DrawerCashSales = completedSales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);

        var movements = await _context.CashDrawerMovements
            .AsNoTracking()
            .Where(m => m.CreatedAt >= todayStart)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        DrawerDeposits = movements.Where(m => m.MovementType == "Deposit").Sum(m => m.Amount);
        DrawerWithdrawals = movements.Where(m => m.MovementType == "Withdrawal").Sum(m => m.Amount);

        DrawerMovements.Clear();
        foreach (var m in movements)
        {
            DrawerMovements.Add(m);
        }

        _allLiveEventsCache.Clear();

        // 1. إضافة الرصيد الافتتاحي كحركة أولية إذا كان موجوداً
        if (DrawerOpeningBalance > 0)
        {
            _allLiveEventsCache.Add(new CashierLiveTransactionItem
            {
                Id = Guid.NewGuid(),
                TransactionType = "Deposit",
                Title = Loc.IsKurdish ? "باڵانسی سەرەتایی زیادکراوی خەزێنە" : "الرصيد الافتتاحي المودع بالخزينة",
                BadgeText = Loc.IsKurdish ? "دانانی دەستی" : "إيداع يدوي",
                BadgeBackground = "#064E3B",
                BadgeForeground = "#34D399",
                CashierAndDate = $"{DateTime.Today:yyyy/MM/dd} 08:00 AM • " + (_context.Users.FirstOrDefault()?.FullName ?? "كاشير عام"),
                Amount = DrawerOpeningBalance
            });
        }

        // 2. دمج كافة المبيعات والمرتجعات والحركات وترتيبها زمنياً
        foreach (var sale in allTodaySales)
        {
            bool isReturn = sale.Status == "Returned";
            string cashierName = sale.User?.FullName ?? "كاشير عام";
            string dateStr = sale.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd hh:mm tt");

            _allLiveEventsCache.Add(new CashierLiveTransactionItem
            {
                Id = sale.Id,
                TransactionType = isReturn ? "Return" : "Sale",
                Title = isReturn 
                    ? (Loc.IsKurdish ? $"گەڕاندنەوەی کاڵای پسوولەی ({sale.InvoiceNumber})" : $"إرجاع مواد الوصل ({sale.InvoiceNumber})")
                    : (Loc.IsKurdish ? $"پسوولەی فرۆشتنی ژمارە ({sale.InvoiceNumber})" : $"فاتورة مبيعات رقم ({sale.InvoiceNumber})"),
                BadgeText = isReturn 
                    ? (Loc.IsKurdish ? "بڕینی گەڕێنراوە" : "خصم مسترجع")
                    : (Loc.IsKurdish ? "پسوولەی فرۆشتن" : "فاتورة بيع"),
                BadgeBackground = isReturn ? "#7F1D1D" : "#064E3B",
                BadgeForeground = isReturn ? "#FECACA" : "#34D399",
                CashierAndDate = $"{dateStr} • " + (Loc.IsKurdish ? $"کاشێر: {cashierName}" : $"الكاشير: {cashierName}"),
                Amount = isReturn ? -sale.TotalAmount : sale.TotalAmount,
                AssociatedSale = sale,
                CreatedAt = sale.CreatedAt
            });
        }

        foreach (var m in movements)
        {
            bool isDeposit = m.MovementType == "Deposit";
            string dateStr = m.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd hh:mm tt");

            _allLiveEventsCache.Add(new CashierLiveTransactionItem
            {
                Id = m.Id,
                TransactionType = m.MovementType,
                Title = isDeposit 
                    ? (Loc.IsKurdish ? $"باڵانسی زیادکراوی خەزێنە ({m.Reason})" : $"إيداع نقد بالخزينة ({m.Reason})")
                    : (Loc.IsKurdish ? $"ڕاکێشانی نەقد لە خەزێنە ({m.Reason})" : $"سحب نقد من الخزينة ({m.Reason})"),
                BadgeText = isDeposit 
                    ? (Loc.IsKurdish ? "دانانی دەستی" : "إيداع يدوي")
                    : (Loc.IsKurdish ? "ڕاکێشانی دەستی" : "سحب يدوي"),
                BadgeBackground = isDeposit ? "#064E3B" : "#7F1D1D",
                BadgeForeground = isDeposit ? "#34D399" : "#FECACA",
                CashierAndDate = $"{dateStr} • " + (_context.Users.FirstOrDefault()?.FullName ?? "كاشير عام"),
                Amount = isDeposit ? m.Amount : -m.Amount,
                AssociatedMovement = m,
                CreatedAt = m.CreatedAt
            });
        }

        var sorted = _allLiveEventsCache.OrderByDescending(x => x.CreatedAt).ToList();
        _allLiveEventsCache.Clear();
        _allLiveEventsCache.AddRange(sorted);

        ApplyTransactionFilter();

        OnPropertyChanged(nameof(DrawerNetSales));
        OnPropertyChanged(nameof(DrawerCurrentCash));
    }

    private FlowDocument CreateSingleInvoiceReceiptFlowDocument(Sale sale, double printableWidth)
    {
        FlowDocument doc = new FlowDocument
        {
            PageWidth = printableWidth > 0 ? Math.Min(printableWidth, 320) : 300,
            PagePadding = new Thickness(10),
            FontFamily = new FontFamily("Segoe UI, Tahoma, Arial, Times New Roman"),
            FlowDirection = FlowDirection.RightToLeft
        };

        // Header
        Paragraph pHeader = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8)
        };
        pHeader.Inlines.Add(new Bold(new Run("⚡ 7amo.pos\n")) { FontSize = 16 });
        pHeader.Inlines.Add(new Run("نظام نقاط البيع والمخازن المتكامل\n") { FontSize = 10, Foreground = Brushes.DimGray });
        pHeader.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });
        pHeader.Inlines.Add(new Run($"رقم الوصل: {sale.InvoiceNumber}\n") { FontSize = 11, FontWeight = FontWeights.Bold });
        pHeader.Inlines.Add(new Run($"التاريخ: {sale.CreatedAt.ToLocalTime():yyyy-MM-dd hh:mm tt}\n") { FontSize = 10 });
        pHeader.Inlines.Add(new Run($"الحالة: {(sale.Status == "Returned" ? "مسترجع (Returned) 🔄" : "مكتمل (Completed) ✔")}\n") { FontSize = 10, FontWeight = FontWeights.Bold });
        pHeader.Inlines.Add(new Run($"الكاشير: {sale.User?.FullName ?? "كاشير عام"} | الدفع: {(sale.PaymentMethod == "Cash" ? "نقداً" : "بطاقة")}\n") { FontSize = 10 });
        pHeader.Inlines.Add(new Run("-------------------------------------------") { Foreground = Brushes.Gray });
        doc.Blocks.Add(pHeader);

        // Items Table
        Table table = new Table { CellSpacing = 2, Margin = new Thickness(0, 0, 0, 8) };
        table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

        TableRowGroup rowGroup = new TableRowGroup();
        TableRow headerRow = new TableRow { FontWeight = FontWeights.Bold };
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run("المادة"))));
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run("العدد"))));
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run("السعر"))));
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run("الإجمالي"))));
        rowGroup.Rows.Add(headerRow);

        foreach (var item in sale.Items)
        {
            TableRow row = new TableRow { FontSize = 10 };
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.ProductName))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.Quantity.ToString("N0")))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.UnitPrice.ToString("N0")))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.TotalPrice.ToString("N0")))));
            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        // Totals & Footer
        Paragraph pTotals = new Paragraph
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 0, 8)
        };
        pTotals.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });
        pTotals.Inlines.Add(new Run($"المجموع الفرعي: {sale.SubTotal:N0} د.ع\n") { FontSize = 10 });
        if (sale.DiscountAmount > 0)
        {
            pTotals.Inlines.Add(new Run($"الخصم الممنوح: {sale.DiscountAmount:N0} د.ع\n") { FontSize = 10, Foreground = Brushes.DarkRed });
        }
        pTotals.Inlines.Add(new Bold(new Run($"المبلغ الإجمالي: {sale.TotalAmount:N0} د.ع\n")) { FontSize = 14 });
        pTotals.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });
        pTotals.Inlines.Add(new Run("شكراً لتعاملكم معنا ونتشرف بزيارتكم دائماً") { FontSize = 9, Foreground = Brushes.DimGray });
        doc.Blocks.Add(pTotals);

        return doc;
    }

    #region Direct Item Return Processing Logic

    private async Task ProcessDirectReturnScanAsync()
    {
        if (string.IsNullOrWhiteSpace(DirectReturnSearchQuery)) return;

        string query = DirectReturnSearchQuery.Trim();
        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Barcode == query || p.Name.ToLower().Contains(query.ToLower()));

        if (product == null)
        {
            MessageBox.Show(Loc.IsKurdish ? $"کاڵا بە بارکۆد یان ناوی '{query}' نەدۆزرایەوە." : $"المادة بالباركود أو الاسم '{query}' غير موجودة.", 
                            Loc.IsKurdish ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = DirectReturnItems.FirstOrDefault(x => x.Product.Id == product.Id);
        if (existing != null)
        {
            existing.Quantity++;
        }
        else
        {
            DirectReturnItems.Add(new DirectReturnItemViewModel
            {
                Product = product,
                ProductName = product.Name,
                Barcode = product.Barcode,
                UnitPrice = product.Price,
                Quantity = 1
            });
        }

        DirectReturnSearchQuery = string.Empty;
        NotifyDirectReturnChanged();
        RequestFocusDirectReturnBarcode?.Invoke();
    }

    private async Task ConfirmDirectReturnAsync()
    {
        if (DirectReturnItems.Count == 0)
        {
            MessageBox.Show(Loc.IsKurdish ? "تکایە سەرەتا کاڵاکان دیاریبکە بۆ گەڕاندنەوە." : "يرجى مسح أو إضافة المواد أولاً لإرجاعها.", 
                            Loc.IsKurdish ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string msg = Loc.IsKurdish
            ? $"ئایا دڵنیایت لە گەڕاندنەوەی ({DirectReturnItems.Count}) کاڵا بە بڕی گشتی ({DirectReturnGrandTotal:N0} د.ع) و زیادکردنەوەیان بۆ کۆگا؟"
            : $"هل ترغب في تأكيد إرجاع ({DirectReturnItems.Count}) مواد بقيمة إجمالية ({DirectReturnGrandTotal:N0} د.ع) وإعادتها للمخزن؟";

        var res = MessageBox.Show(msg, Loc.IsKurdish ? "پشتڕاستکردنەوەی گەڕاندنەوە" : "تأكيد الإرجاع المباشر", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            var currentUser = await _context.Users.FirstOrDefaultAsync();
            var sale = new Sale
            {
                InvoiceNumber = "RET-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                CreatedAt = DateTime.UtcNow,
                Status = "Returned",
                PaymentMethod = "Cash",
                SubTotal = -DirectReturnGrandTotal,
                TotalAmount = -DirectReturnGrandTotal,
                UserId = currentUser?.Id ?? Guid.Empty,
                User = currentUser
            };

            foreach (var item in DirectReturnItems)
            {
                var saleItem = new SaleItem
                {
                    ProductId = item.Product.Id,
                    ProductName = item.ProductName,
                    Barcode = item.Barcode,
                    UnitPrice = item.UnitPrice,
                    Quantity = -item.Quantity,
                    TotalPrice = -item.TotalPrice
                };
                sale.Items.Add(saleItem);

                // Update stock in DB
                var dbProd = await _context.Products.FindAsync(item.Product.Id);
                if (dbProd != null)
                {
                    dbProd.StockQuantity += item.Quantity;
                }
            }

            _context.Sales.Add(sale);

            // Cash Drawer withdrawal movement for customer refund
            var movement = new CashDrawerMovement
            {
                MovementType = "Withdrawal",
                Amount = DirectReturnGrandTotal,
                Reason = Loc.IsKurdish ? $"گەڕاندنەوەی ڕاستەوخۆ ({DirectReturnItems.Count} کاڵا)" : $"إرجاع مواد مباشر ({DirectReturnItems.Count} أصناف)",
                CreatedAt = DateTime.UtcNow
            };
            _context.CashDrawerMovements.Add(movement);

            await _context.SaveChangesAsync();

            // Kick drawer open
            if (KickDrawerHardwareCommand.CanExecute(null))
            {
                KickDrawerHardwareCommand.Execute(null);
            }

            // Print Return Receipt
            PrintDirectReturnReceiptDocument(sale);

            await LoadTodayStatsAsync();
            await LoadDrawerCashDataAsync();
            SaleCompleted?.Invoke();

            MessageBox.Show(Loc.IsKurdish ? "گەڕاندنەوەی کاڵا بە سەرکەوتوویی جێبەجێکرا و کۆگا نوێکرایەوە." : "تم إرجاع المواد بنجاح وتحديث المخزن وخصم المبلغ من الصندوق.",
                            Loc.IsKurdish ? "سەرکەوتوو بوو" : "تم بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);

            DirectReturnItems.Clear();
            NotifyDirectReturnChanged();
            IsDirectReturnModalOpen = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء حفظ الإرجاع: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintDirectReturnReceipt()
    {
        if (DirectReturnItems.Count == 0) return;
        try
        {
            var tempSale = new Sale
            {
                InvoiceNumber = "RET-PREVIEW",
                CreatedAt = DateTime.UtcNow,
                Status = "Returned",
                PaymentMethod = "Cash",
                SubTotal = -DirectReturnGrandTotal,
                TotalAmount = -DirectReturnGrandTotal,
                User = _context.Users.FirstOrDefault()
            };
            foreach (var item in DirectReturnItems)
            {
                tempSale.Items.Add(new SaleItem
                {
                    ProductName = item.ProductName,
                    Barcode = item.Barcode,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    TotalPrice = item.TotalPrice
                });
            }
            PrintDirectReturnReceiptDocument(tempSale);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء الطباعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintDirectReturnReceiptDocument(Sale sale)
    {
        try
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateSingleInvoiceReceiptFlowDocument(sale, printDialog.PrintableAreaWidth);
                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, $"وصل إرجاع {sale.InvoiceNumber}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء طباعة الوصل: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintCashMovementVoucher(CashDrawerMovement movement)
    {
        try
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateCashMovementFlowDocument(movement, printDialog.PrintableAreaWidth);
                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, $"سند حركة صندوق {movement.Id.ToString().Substring(0, 5)}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء طباعة السند: {ex.Message}", "خطأ في الطباعة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FlowDocument CreateCashMovementFlowDocument(CashDrawerMovement movement, double printableWidth)
    {
        FlowDocument doc = new FlowDocument
        {
            PageWidth = printableWidth > 0 ? Math.Min(printableWidth, 320) : 300,
            PagePadding = new Thickness(10),
            FontFamily = new FontFamily("Segoe UI, Tahoma, Arial"),
            FlowDirection = FlowDirection.RightToLeft
        };

        bool isKu = Loc.IsKurdish;
        bool isWithdrawal = movement.MovementType == "Withdrawal";
        string docTitle = isWithdrawal 
            ? (isKu ? "سەندی راکێشانی پارە لە سندووق (صرف)" : "سند سحب نقد من الصندوق (سند صرف)")
            : (isKu ? "سەندی دانانی پارە لە سندووق (قبض)" : "سند إيداع نقد في الصندوق (سند قبض)");

        Paragraph p = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        p.Inlines.Add(new Bold(new Run("⚡ 7AMO POS SYSTEM\n")) { FontSize = 15 });
        p.Inlines.Add(new Bold(new Run($"{docTitle}\n")) { FontSize = 12, Foreground = isWithdrawal ? Brushes.DarkRed : Brushes.DarkGreen });
        p.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });
        p.Inlines.Add(new Run($"{(isKu ? "ژمارەی سەند:" : "رقم السند:")} MOV-{movement.CreatedAt.ToLocalTime():yyyyMMdd}-{movement.Id.ToString().Substring(0, 5).ToUpper()}\n") { FontSize = 9.5 });
        p.Inlines.Add(new Run($"{(isKu ? "بەروار و کات:" : "التاريخ والوقت:")} {movement.CreatedAt.ToLocalTime():yyyy-MM-dd hh:mm tt}\n") { FontSize = 9.5 });
        p.Inlines.Add(new Run($"{(isKu ? "کاشێر:" : "الكاشير:")} {movement.CashierName}\n") { FontSize = 9.5, FontWeight = FontWeights.Bold });
        p.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });

        p.Inlines.Add(new Bold(new Run($"{(isKu ? "بڕی پارە:" : "المبلغ:")} {movement.Amount:N0} د.ع\n")) { FontSize = 16, Foreground = isWithdrawal ? Brushes.DarkRed : Brushes.DarkGreen });
        p.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });
        p.Inlines.Add(new Run($"{(isKu ? "هۆکار و تێبینی:" : "السبب / البيان:")}\n") { FontSize = 10, FontWeight = FontWeights.Bold });
        p.Inlines.Add(new Run($"{movement.Reason}\n\n") { FontSize = 10.5 });
        p.Inlines.Add(new Run("===========================================\n") { Foreground = Brushes.Gray });
        p.Inlines.Add(new Run(isKu ? "واژۆی وەرگر / کاشێر: ........................\n" : "توقيع المستلم / الكاشير: ........................\n") { FontSize = 9 });
        p.Inlines.Add(new Run("===========================================\n") { Foreground = Brushes.Gray });
        doc.Blocks.Add(p);

        return doc;
    }

    public static decimal ParseDecimalSafe(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return 0;
        string cleaned = input.Trim()
            .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4")
            .Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9")
            .Replace(",", "").Replace(" ", "");
        return decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
    }

    #endregion
}

public class DirectReturnItemViewModel : BaseViewModel
{
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }

    private decimal _quantity = 1;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(TotalPrice));
            }
        }
    }

    public decimal TotalPrice => Quantity * UnitPrice;
    public string ReturnReason { get; set; } = "إرجاع مباشر";
}

public class CashierLiveTransactionItem
{
    public Guid Id { get; set; }
    public string TransactionType { get; set; } = string.Empty; // "Sale", "Return", "Deposit", "Withdrawal"
    public string Title { get; set; } = string.Empty;
    public string BadgeText { get; set; } = string.Empty;
    public string BadgeBackground { get; set; } = "#064E3B";
    public string BadgeForeground { get; set; } = "#34D399";
    public string CashierAndDate { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string FormattedAmount => (Amount >= 0 ? "+ د.ع " : "- د.ع ") + $"{Math.Abs(Amount):N0}";
    public string AmountColor => Amount >= 0 ? "#10B981" : "#EF4444";
    public string CardBackground => Amount >= 0 ? "#0C1929" : "#240C12";
    public string CardBorder => Amount >= 0 ? "#1E293B" : "#4C0519";
    public string IconText => TransactionType switch
    {
        "Return" => "🔄",
        "Withdrawal" => "↗",
        "Deposit" => "↘",
        _ => "💵"
    };
    public string IconBackground => Amount >= 0 ? "#064E3B" : "#450A0A";
    public string IconForeground => Amount >= 0 ? "#34D399" : "#F87171";
    public Sale? AssociatedSale { get; set; }
    public CashDrawerMovement? AssociatedMovement { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
