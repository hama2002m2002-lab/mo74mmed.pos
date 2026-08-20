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

#region Helper Models for Advanced Analytics

public class CashierPerformanceItem
{
    public string CashierName { get; set; } = string.Empty;
    public int InvoicesCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal AvgInvoiceValue => InvoicesCount > 0 ? Math.Round(TotalSalesAmount / InvoicesCount, 0) : 0;
    public string AvgSpeedText { get; set; } = "45 ثانية / وصل";
}

public class HourlyPeakItem
{
    public string HourLabel { get; set; } = string.Empty;
    public int InvoicesCount { get; set; }
    public decimal TotalSales { get; set; }
    public double IntensityPercent { get; set; }
    public string PeakBadge => IntensityPercent >= 80 ? "🔥 ذروة قصوى" : (IntensityPercent >= 40 ? "⚡ نشاط متوسط" : "🟢 هادئ");
}

public class ProductMovementItem
{
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal CurrentStock { get; set; }
    public string MovementCategory { get; set; } = "سريع"; // 🔥 الأكثر مبيعاً, 🟡 بطيء الحركة, ❄️ بضاعة راكدة
}

public class InventoryValuationItem
{
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal StockQuantity { get; set; }
    public decimal MinStockAlert { get; set; } = 5.0m;
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalCostValue => StockQuantity * UnitCost;
    public decimal TotalSellingValue => StockQuantity * UnitPrice;
    public decimal PotentialProfit => TotalSellingValue - TotalCostValue;

    public string StockStatus
    {
        get
        {
            decimal alertThreshold = MinStockAlert > 0 ? MinStockAlert : 5.0m;
            if (StockQuantity <= 0)
                return LocalizationManager.Instance.IsKurdish ? "تەواوبووە ❌" : "نفد المخزون ❌";
            if (StockQuantity <= alertThreshold)
                return LocalizationManager.Instance.IsKurdish ? "نزیکە لە تەواوبوون ⚠️" : "يوشك على النفاد ⚠️";
            return LocalizationManager.Instance.IsKurdish ? "بەردەستە و پڕە ✔" : "متوفر وممتاز ✔";
        }
    }
}

#endregion

public class ReportsViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly ISaleService _saleService;

    #region Navigation & Active Tab

    private string _activeSubReportTab = "Hub"; // Default to Hub view (9 cards)
    public string ActiveSubReportTab
    {
        get => _activeSubReportTab;
        set
        {
            if (SetProperty(ref _activeSubReportTab, value))
            {
                OnPropertyChanged(nameof(IsHubViewActive));
                OnPropertyChanged(nameof(IsReportDetailActive));
                OnPropertyChanged(nameof(IsMasterTabActive));
                OnPropertyChanged(nameof(IsSalesTabActive));
                OnPropertyChanged(nameof(IsDamagedTabActive));
                OnPropertyChanged(nameof(IsReturnsTabActive));
                OnPropertyChanged(nameof(IsPurchasesTabActive));
                OnPropertyChanged(nameof(IsInventoryTabActive));
                OnPropertyChanged(nameof(IsDebtsTabActive));
                OnPropertyChanged(nameof(IsShiftAuditTabActive));
                OnPropertyChanged(nameof(IsPerformanceTabActive));
                OnPropertyChanged(nameof(IsStockMovementTabActive));
            }
        }
    }

    public bool IsHubViewActive => ActiveSubReportTab == "Hub";
    public bool IsReportDetailActive => ActiveSubReportTab != "Hub";
    public bool IsMasterTabActive => ActiveSubReportTab == "Master";
    public bool IsSalesTabActive => ActiveSubReportTab == "Sales";
    public bool IsDamagedTabActive => ActiveSubReportTab == "Damaged";
    public bool IsReturnsTabActive => ActiveSubReportTab == "Returns";
    public bool IsPurchasesTabActive => ActiveSubReportTab == "Purchases";
    public bool IsInventoryTabActive => ActiveSubReportTab == "Inventory";
    public bool IsDebtsTabActive => ActiveSubReportTab == "Debts";
    public bool IsShiftAuditTabActive => ActiveSubReportTab == "ShiftAudit";
    public bool IsPerformanceTabActive => ActiveSubReportTab == "Performance";
    public bool IsStockMovementTabActive => ActiveSubReportTab == "StockMovement";

    #endregion

    #region Master Report & Expenses Management

    private string _masterSubSection = "Sales";
    public string MasterSubSection
    {
        get => _masterSubSection;
        set
        {
            if (SetProperty(ref _masterSubSection, value))
            {
                OnPropertyChanged(nameof(IsMasterSalesSectionActive));
                OnPropertyChanged(nameof(IsMasterExpensesSectionActive));
                OnPropertyChanged(nameof(IsMasterDamagedSectionActive));
                OnPropertyChanged(nameof(IsMasterInventorySectionActive));
                OnPropertyChanged(nameof(IsMasterDebtsSectionActive));
            }
        }
    }

    public bool IsMasterSalesSectionActive => MasterSubSection == "Sales";
    public bool IsMasterExpensesSectionActive => MasterSubSection == "Expenses";
    public bool IsMasterDamagedSectionActive => MasterSubSection == "Damaged";
    public bool IsMasterInventorySectionActive => MasterSubSection == "Inventory";
    public bool IsMasterDebtsSectionActive => MasterSubSection == "Debts";

    public ICommand SetMasterSubSectionCommand { get; }

    public ObservableCollection<string> ExpenseCategoriesList { get; } = new()
    {
        "نثريات ومصروفات عامة",
        "إيجار المحل / المخزن",
        "كهرباء ومولد وطاقة",
        "رواتب وأجور عمال وكاشير",
        "صيانة وتصليح ومعدات",
        "شحن وتوصيل ونقل",
        "ضيافة ونظافة",
        "مصروفات أخرى"
    };

    public ObservableCollection<Expense> ExpensesList { get; } = new();

    private decimal _totalExpensesAmount;
    public decimal TotalExpensesAmount { get => _totalExpensesAmount; set => SetProperty(ref _totalExpensesAmount, value); }

    private string _newExpenseTitle = string.Empty;
    public string NewExpenseTitle { get => _newExpenseTitle; set => SetProperty(ref _newExpenseTitle, value); }

    private decimal _newExpenseAmount;
    public decimal NewExpenseAmount { get => _newExpenseAmount; set => SetProperty(ref _newExpenseAmount, value); }

    private string _newExpenseCategory = "نثريات ومصروفات عامة";
    public string NewExpenseCategory { get => _newExpenseCategory; set => SetProperty(ref _newExpenseCategory, value); }

    private string _newExpenseNotes = string.Empty;
    public string NewExpenseNotes { get => _newExpenseNotes; set => SetProperty(ref _newExpenseNotes, value); }

    public ICommand SaveExpenseCommand { get; }
    public ICommand DeleteExpenseCommand { get; }

    public int InStockCount => InventoryValuationList.Count(p => p.StockQuantity > (p.MinStockAlert > 0 ? p.MinStockAlert : 5));
    public int LowStockCount => InventoryValuationList.Count(p => p.StockQuantity > 0 && p.StockQuantity <= (p.MinStockAlert > 0 ? p.MinStockAlert : 5));

    #endregion

    #region Date Filters

    private DateTime _fromDate = DateTime.Today;
    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    private DateTime _toDate = DateTime.Today;
    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    #endregion

    #region 1. Sales & Net Profit Breakdown

    private decimal _retailGrossProfit;
    public decimal RetailGrossProfit { get => _retailGrossProfit; set => SetProperty(ref _retailGrossProfit, value); }

    private decimal _wholesaleGrossProfit;
    public decimal WholesaleGrossProfit { get => _wholesaleGrossProfit; set => SetProperty(ref _wholesaleGrossProfit, value); }

    private decimal _cartonGrossProfit;
    public decimal CartonGrossProfit { get => _cartonGrossProfit; set => SetProperty(ref _cartonGrossProfit, value); }

    private decimal _totalGrossSalesProfit;
    public decimal TotalGrossSalesProfit { get => _totalGrossSalesProfit; set => SetProperty(ref _totalGrossSalesProfit, value); }

    private decimal _totalDiscountsGranted;
    public decimal TotalDiscountsGranted { get => _totalDiscountsGranted; set => SetProperty(ref _totalDiscountsGranted, value); }

    private decimal _operatingExpenses = 0;
    public decimal OperatingExpenses { get => _operatingExpenses; set => SetProperty(ref _operatingExpenses, value); }

    private decimal _finalNetProfit;
    public decimal FinalNetProfit { get => _finalNetProfit; set => SetProperty(ref _finalNetProfit, value); }

    private decimal _totalSalesRevenue;
    public decimal TotalSalesRevenue { get => _totalSalesRevenue; set => SetProperty(ref _totalSalesRevenue, value); }

    public ObservableCollection<Sale> Invoices { get; } = new();

    #endregion

    #region 2. Damaged & Expired Items Report

    public ObservableCollection<DamagedItem> DamagedItemsList { get; } = new();
    private decimal _totalDamagedLoss;
    public decimal TotalDamagedLoss { get => _totalDamagedLoss; set => SetProperty(ref _totalDamagedLoss, value); }
    private int _totalDamagedItemsCount;
    public int TotalDamagedItemsCount { get => _totalDamagedItemsCount; set => SetProperty(ref _totalDamagedItemsCount, value); }

    #endregion

    #region 3. Returns & Refunds Report

    public ObservableCollection<Sale> ReturnedInvoicesList { get; } = new();
    private decimal _totalReturnsAmount;
    public decimal TotalReturnsAmount { get => _totalReturnsAmount; set => SetProperty(ref _totalReturnsAmount, value); }
    private int _totalReturnedCount;
    public int TotalReturnedCount { get => _totalReturnedCount; set => SetProperty(ref _totalReturnedCount, value); }

    #endregion

    #region 4. Purchases & Supplier Invoices Report

    public ObservableCollection<PurchaseInvoice> PurchasesList { get; } = new();
    private decimal _totalPurchasesAmount;
    public decimal TotalPurchasesAmount { get => _totalPurchasesAmount; set => SetProperty(ref _totalPurchasesAmount, value); }
    private decimal _totalPurchasesPaid;
    public decimal TotalPurchasesPaid { get => _totalPurchasesPaid; set => SetProperty(ref _totalPurchasesPaid, value); }
    private decimal _totalPurchasesDebtRemaining;
    public decimal TotalPurchasesDebtRemaining { get => _totalPurchasesDebtRemaining; set => SetProperty(ref _totalPurchasesDebtRemaining, value); }

    #endregion

    #region 5. Inventory Valuation & Stock Report

    public ObservableCollection<InventoryValuationItem> InventoryValuationList { get; } = new();
    private decimal _totalInventoryCostValue;
    public decimal TotalInventoryCostValue { get => _totalInventoryCostValue; set => SetProperty(ref _totalInventoryCostValue, value); }
    private decimal _totalInventorySellingValue;
    public decimal TotalInventorySellingValue { get => _totalInventorySellingValue; set => SetProperty(ref _totalInventorySellingValue, value); }
    private decimal _expectedInventoryProfit;
    public decimal ExpectedInventoryProfit { get => _expectedInventoryProfit; set => SetProperty(ref _expectedInventoryProfit, value); }
    private int _outOfStockCount;
    public int OutOfStockCount { get => _outOfStockCount; set => SetProperty(ref _outOfStockCount, value); }

    #endregion

    #region 6. Customer Debts & Receivables Report

    public ObservableCollection<CustomerDebt> CustomerDebtsList { get; } = new();
    private decimal _totalCustomerDebtsDue;
    public decimal TotalCustomerDebtsDue { get => _totalCustomerDebtsDue; set => SetProperty(ref _totalCustomerDebtsDue, value); }
    private decimal _totalCustomerDebtsCollected;
    public decimal TotalCustomerDebtsCollected { get => _totalCustomerDebtsCollected; set => SetProperty(ref _totalCustomerDebtsCollected, value); }
    private decimal _netOutstandingCustomerDebts;
    public decimal NetOutstandingCustomerDebts { get => _netOutstandingCustomerDebts; set => SetProperty(ref _netOutstandingCustomerDebts, value); }

    // Quick Debt Entry Fields
    private string _newCustomerName = string.Empty;
    public string NewCustomerName { get => _newCustomerName; set => SetProperty(ref _newCustomerName, value); }
    private string _newCustomerPhone = string.Empty;
    public string NewCustomerPhone { get => _newCustomerPhone; set => SetProperty(ref _newCustomerPhone, value); }
    private decimal _newCustomerDebtAmount;
    public decimal NewCustomerDebtAmount { get => _newCustomerDebtAmount; set => SetProperty(ref _newCustomerDebtAmount, value); }
    private string _newCustomerNotes = string.Empty;
    public string NewCustomerNotes { get => _newCustomerNotes; set => SetProperty(ref _newCustomerNotes, value); }

    #endregion

    #region 7. Shift End Cash Audit & Handover (Z-Report)

    public ObservableCollection<ShiftAudit> ShiftAuditsList { get; } = new();
    private decimal _shiftOpeningBalance = 50000; // عهدة افتتاحية 50 ألف
    public decimal ShiftOpeningBalance
    {
        get => _shiftOpeningBalance;
        set
        {
            if (SetProperty(ref _shiftOpeningBalance, value))
            {
                RecalculateShiftDiscrepancy();
            }
        }
    }

    private decimal _shiftActualCashCount;
    public decimal ShiftActualCashCount
    {
        get => _shiftActualCashCount;
        set
        {
            if (SetProperty(ref _shiftActualCashCount, value))
            {
                RecalculateShiftDiscrepancy();
            }
        }
    }

    private decimal _shiftExpectedCash;
    public decimal ShiftExpectedCash { get => _shiftExpectedCash; set => SetProperty(ref _shiftExpectedCash, value); }

    private decimal _shiftDiscrepancy;
    public decimal ShiftDiscrepancy { get => _shiftDiscrepancy; set => SetProperty(ref _shiftDiscrepancy, value); }

    private string _shiftDiscrepancyStatus = "جاهز للمطابقة";
    public string ShiftDiscrepancyStatus { get => _shiftDiscrepancyStatus; set => SetProperty(ref _shiftDiscrepancyStatus, value); }

    private string _shiftHandoverNotes = string.Empty;
    public string ShiftHandoverNotes { get => _shiftHandoverNotes; set => SetProperty(ref _shiftHandoverNotes, value); }

    private string _shiftSupervisorName = "المدير العام";
    public string ShiftSupervisorName { get => _shiftSupervisorName; set => SetProperty(ref _shiftSupervisorName, value); }

    #endregion

    #region 8. Cashier Performance & Peak Hours

    public ObservableCollection<CashierPerformanceItem> CashierPerformanceList { get; } = new();
    public ObservableCollection<HourlyPeakItem> HourlyPeakList { get; } = new();
    private string _busiestHourText = "من 06:00 م إلى 09:00 م";
    public string BusiestHourText { get => _busiestHourText; set => SetProperty(ref _busiestHourText, value); }

    #endregion

    #region 9. Product Velocity & Stock Movement

    public ObservableCollection<ProductMovementItem> FastMovingProducts { get; } = new();
    public ObservableCollection<ProductMovementItem> SlowMovingProducts { get; } = new();
    public ObservableCollection<ProductMovementItem> DeadStockProducts { get; } = new();

    #endregion

    #region Invoices Receipt Modal Properties

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

    #endregion

    #region Commands

    public ICommand SwitchReportTabCommand { get; }
    public ICommand BackToHubCommand { get; }
    public ICommand FilterTodayCommand { get; }
    public ICommand FilterThisMonthCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand OpenInvoiceDetailsCommand { get; }
    public ICommand CloseInvoiceDetailsCommand { get; }
    public ICommand SaveCustomerDebtCommand { get; }
    public ICommand PayCustomerDebtCommand { get; }
    public ICommand DeleteCustomerDebtCommand { get; }
    public ICommand SaveShiftAuditCommand { get; }
    public ICommand BackToMainCommand { get; }

    public event Action? RequestBackToNavigation;

    #endregion

    public ReportsViewModel()
    {
        _context = new AppDbContext();
        _saleService = new SaleService(_context);

        SwitchReportTabCommand = new RelayCommand(tab =>
        {
            if (tab is string tabName)
            {
                ActiveSubReportTab = tabName;
                _ = LoadReportAsync();
            }
        });

        SetMasterSubSectionCommand = new RelayCommand(param =>
        {
            if (param is string sec)
            {
                MasterSubSection = sec;
            }
        });

        BackToHubCommand = new RelayCommand(() =>
        {
            ActiveSubReportTab = "Hub";
        });

        FilterTodayCommand = new RelayCommand(() =>
        {
            FromDate = DateTime.Today;
            ToDate = DateTime.Today;
            _ = LoadReportAsync();
        });

        FilterThisMonthCommand = new RelayCommand(() =>
        {
            FromDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            ToDate = DateTime.Today;
            _ = LoadReportAsync();
        });

        RefreshCommand = new AsyncRelayCommand(async () => await LoadReportAsync());

        OpenInvoiceDetailsCommand = new RelayCommand(param =>
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

        SaveCustomerDebtCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(NewCustomerName) || NewCustomerDebtAmount <= 0)
            {
                MessageBox.Show("يرجى إدخال اسم العميل ومبلغ الدين بشكل صحيح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var debt = new CustomerDebt
            {
                Id = Guid.NewGuid(),
                CustomerName = NewCustomerName.Trim(),
                PhoneNumber = NewCustomerPhone.Trim(),
                TotalDebt = NewCustomerDebtAmount,
                TotalPaid = 0,
                LastTransactionType = "دين مشتريات جديد",
                Notes = NewCustomerNotes,
                CreatedAt = DateTime.UtcNow
            };

            await _context.CustomerDebts.AddAsync(debt);
            await _context.SaveChangesAsync();

            NewCustomerName = string.Empty;
            NewCustomerPhone = string.Empty;
            NewCustomerDebtAmount = 0;
            NewCustomerNotes = string.Empty;

            await LoadReportAsync();
            MessageBox.Show("تم تسجيل قيد الدين بنجاح.", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        PayCustomerDebtCommand = new AsyncRelayCommand(async param =>
        {
            if (param is CustomerDebt cd)
            {
                cd.TotalPaid = cd.TotalDebt;
                cd.LastTransactionType = "تم السداد بالكامل ✔";
                cd.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await LoadReportAsync();
                MessageBox.Show($"تم تسديد دين العميل '{cd.CustomerName}' بالكامل.", "تم السداد", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });

        DeleteCustomerDebtCommand = new AsyncRelayCommand(async param =>
        {
            if (param is CustomerDebt cd)
            {
                var res = MessageBox.Show($"هل ترغب في حذف سجل العميل '{cd.CustomerName}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _context.CustomerDebts.Remove(cd);
                    await _context.SaveChangesAsync();
                    await LoadReportAsync();
                }
            }
        });

        SaveShiftAuditCommand = new AsyncRelayCommand(async () =>
        {
            try
            {
                var audit = new ShiftAudit
                {
                    Id = Guid.NewGuid(),
                    CashierName = "محمد الكاشير",
                    ShiftStartTime = DateTime.Today.AddHours(8),
                    ShiftEndTime = DateTime.Now,
                    OpeningBalance = ShiftOpeningBalance,
                    TotalSalesCash = TotalSalesRevenue,
                    TotalSalesCard = 0,
                    TotalReturnsCash = TotalReturnsAmount,
                    ActualCountedCash = ShiftActualCashCount,
                    HandoverNotes = ShiftHandoverNotes,
                    SupervisorName = ShiftSupervisorName,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.ShiftAudits.AddAsync(audit);
                await _context.SaveChangesAsync();

                await LoadReportAsync();
                MessageBox.Show($"تم اعتماد وإغلاق الوردية بنجاح!\nالفارق المحاسبي: {ShiftDiscrepancyStatus}", "تم التدقيق والتسليم", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ تقرير الوردية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        SaveExpenseCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(NewExpenseTitle) || NewExpenseAmount <= 0)
            {
                MessageBox.Show("يرجى كتابة بيان المصروف والمبلغ بشكل صحيح.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exp = new Expense
            {
                Id = Guid.NewGuid(),
                Title = NewExpenseTitle.Trim(),
                Amount = NewExpenseAmount,
                Category = string.IsNullOrWhiteSpace(NewExpenseCategory) ? "عام" : NewExpenseCategory.Trim(),
                Notes = NewExpenseNotes,
                RecordedBy = "محمد الكاشير",
                CreatedAt = DateTime.UtcNow
            };

            await _context.Expenses.AddAsync(exp);
            await _context.SaveChangesAsync();

            NewExpenseTitle = string.Empty;
            NewExpenseAmount = 0;
            NewExpenseNotes = string.Empty;

            await LoadReportAsync();
            MessageBox.Show("تم تسجيل المصروف بنجاح وتحديث صافي الأرباح.", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        DeleteExpenseCommand = new AsyncRelayCommand(async param =>
        {
            if (param is Expense exp)
            {
                var res = MessageBox.Show($"هل ترغب في حذف المصروف '{exp.Title}' بمبلغ {exp.Amount:N0} د.ع؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    _context.Expenses.Remove(exp);
                    await _context.SaveChangesAsync();
                    await LoadReportAsync();
                }
            }
        });

        BackToMainCommand = new RelayCommand(() => RequestBackToNavigation?.Invoke());
    }

    private void RecalculateShiftDiscrepancy()
    {
        ShiftExpectedCash = ShiftOpeningBalance + TotalSalesRevenue - TotalReturnsAmount;
        ShiftDiscrepancy = ShiftActualCashCount - ShiftExpectedCash;
        if (ShiftDiscrepancy == 0)
            ShiftDiscrepancyStatus = "مطابق تماماً 100% ✔";
        else if (ShiftDiscrepancy > 0)
            ShiftDiscrepancyStatus = $"فائض نقد بالدرج (+{ShiftDiscrepancy:N0} د.ع) 🟢";
        else
            ShiftDiscrepancyStatus = $"عجز نقدي بالدرج ({ShiftDiscrepancy:N0} د.ع) 🔴";
    }

    public async Task InitializeAsync()
    {
        await LoadReportAsync();
    }

    public async Task LoadReportAsync()
    {
        DateTime start = FromDate.Date;
        DateTime end = ToDate.Date.AddDays(1).AddTicks(-1);

        // 1. Sales & Invoices
        var allDbProducts = await _context.Products.AsNoTracking().ToListAsync();
        var prodById = allDbProducts.ToDictionary(p => p.Id, p => p);
        var prodByBarcode = allDbProducts.Where(p => !string.IsNullOrEmpty(p.Barcode)).GroupBy(p => p.Barcode).ToDictionary(g => g.Key, g => g.First());

        var sales = await _context.Sales
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .Where(s => s.CreatedAt >= start && s.CreatedAt <= end)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        Invoices.Clear();
        ReturnedInvoicesList.Clear();

        decimal retailGross = 0;
        decimal wholesaleGross = 0;
        decimal cartonGross = 0;
        decimal totalRevenue = 0;
        decimal totalDiscounts = 0;
        decimal totalReturns = 0;

        foreach (var sale in sales)
        {
            bool isSaleReturned = sale.Status == "Returned";
            if (isSaleReturned)
            {
                ReturnedInvoicesList.Add(sale);
                totalReturns += Math.Abs(sale.TotalAmount);
            }
            else
            {
                Invoices.Add(sale);
                totalRevenue += sale.TotalAmount;
                totalDiscounts += sale.DiscountAmount;
            }

            foreach (var item in sale.Items)
            {
                bool isItemReturn = isSaleReturned || item.TotalPrice < 0 || item.Quantity < 0 || item.ProductName.Contains("(إرجاع)") || item.ProductName.Contains("إرجاع");
                decimal absQty = Math.Abs(item.Quantity);
                decimal absUnitPrice = Math.Abs(item.UnitPrice);

                Product? prod = item.Product ?? (item.ProductId.HasValue && prodById.TryGetValue(item.ProductId.Value, out var p1) ? p1 : (prodByBarcode.TryGetValue(item.Barcode, out var p2) ? p2 : null));

                if (item.ProductName.Contains("(كرتون)") || item.ProductName.Contains("كرتون"))
                {
                    // 1. البيع بالكرتون: خصم سعر شراء الكرتون
                    decimal cartonCost = prod != null && prod.CartonPurchasePrice > 0 
                        ? prod.CartonPurchasePrice 
                        : (prod != null && prod.Cost > 0 ? prod.Cost * (prod.ItemsPerCarton > 0 ? prod.ItemsPerCarton : 1) : 0);

                    decimal profit = (absUnitPrice - cartonCost) * absQty;

                    if (isItemReturn)
                    {
                        cartonGross -= profit;
                        if (!isSaleReturned) totalReturns += Math.Abs(item.TotalPrice);
                    }
                    else
                    {
                        cartonGross += profit;
                    }
                }
                else if (item.ProductName.Contains("(جملة)") || item.ProductName.Contains("جملة") || (prod != null && absUnitPrice <= prod.Price * 0.95m && absUnitPrice >= prod.WholesalePrice * 0.85m && absUnitPrice > prod.Cost))
                {
                    // 2. البيع بالجملة: خصم سعر تكلفة المفرد
                    decimal pieceCost = prod != null && prod.Cost > 0 
                        ? prod.Cost 
                        : (prod != null && prod.CartonPurchasePrice > 0 && prod.ItemsPerCarton > 0 ? prod.CartonPurchasePrice / prod.ItemsPerCarton : 0);

                    decimal profit = (absUnitPrice - pieceCost) * absQty;

                    if (isItemReturn)
                    {
                        wholesaleGross -= profit;
                        if (!isSaleReturned) totalReturns += Math.Abs(item.TotalPrice);
                    }
                    else
                    {
                        wholesaleGross += profit;
                    }
                }
                else
                {
                    // 3. البيع بالمفرد: خصم سعر تكلفة المفرد
                    decimal pieceCost = prod != null && prod.Cost > 0 
                        ? prod.Cost 
                        : (prod != null && prod.CartonPurchasePrice > 0 && prod.ItemsPerCarton > 0 ? prod.CartonPurchasePrice / prod.ItemsPerCarton : 0);

                    decimal profit = (absUnitPrice - pieceCost) * absQty;

                    if (isItemReturn)
                    {
                        retailGross -= profit;
                        if (!isSaleReturned) totalReturns += Math.Abs(item.TotalPrice);
                    }
                    else
                    {
                        retailGross += profit;
                    }
                }
            }
        }

        RetailGrossProfit = Math.Max(0, retailGross);
        WholesaleGrossProfit = Math.Max(0, wholesaleGross);
        CartonGrossProfit = Math.Max(0, cartonGross);
        TotalGrossSalesProfit = Math.Max(0, retailGross + wholesaleGross + cartonGross);
        TotalDiscountsGranted = totalDiscounts;
        TotalSalesRevenue = totalRevenue;

        TotalReturnsAmount = totalReturns;
        TotalReturnedCount = ReturnedInvoicesList.Count + sales.Count(s => (s.Status == "Returned" || s.InvoiceNumber.StartsWith("RET-")) || s.Items.Any(i => i.TotalPrice < 0 || i.Quantity < 0 || i.ProductName.Contains("(إرجاع)")));

        // 1.1 Expenses Loading (المصروفات العامة)
        var expenses = await _context.Expenses
            .Where(e => e.CreatedAt >= start && e.CreatedAt <= end)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        ExpensesList.Clear();
        decimal expTotal = 0;
        foreach (var ex in expenses)
        {
            ExpensesList.Add(ex);
            expTotal += ex.Amount;
        }
        TotalExpensesAmount = expTotal;
        OperatingExpenses = expTotal;

        // صافي الأرباح النهائي = الأرباح الإجمالية - (الخصومات + المصروفات)
        FinalNetProfit = (TotalGrossSalesProfit - (TotalDiscountsGranted + TotalExpensesAmount));

        // 2. Damaged Items
        var damaged = await _context.DamagedItems
            .Where(d => d.CreatedAt >= start && d.CreatedAt <= end)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        DamagedItemsList.Clear();
        decimal damLoss = 0;
        decimal damQty = 0;
        foreach (var d in damaged)
        {
            DamagedItemsList.Add(d);
            damLoss += d.TotalLossAmount;
            damQty += d.Quantity;
        }
        TotalDamagedLoss = damLoss;
        TotalDamagedItemsCount = (int)damQty;

        // 3. Purchases
        var purchases = await _context.PurchaseInvoices
            .Include(p => p.Items)
            .Where(p => p.CreatedAt >= start && p.CreatedAt <= end)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        PurchasesList.Clear();
        decimal purTotal = 0;
        decimal purPaid = 0;
        foreach (var p in purchases)
        {
            PurchasesList.Add(p);
            purTotal += p.TotalAmount;
            purPaid += p.PaidAmount;
        }
        TotalPurchasesAmount = purTotal;
        TotalPurchasesPaid = purPaid;
        TotalPurchasesDebtRemaining = Math.Max(0, purTotal - purPaid);

        // 4. Inventory Valuation
        var allProducts = await _context.Products.OrderBy(p => p.Name).ToListAsync();
        InventoryValuationList.Clear();
        decimal invCost = 0;
        decimal invSell = 0;
        int outStock = 0;

        foreach (var p in allProducts)
        {
            var valItem = new InventoryValuationItem
            {
                Barcode = p.Barcode,
                ProductName = p.Name,
                StockQuantity = p.StockQuantity,
                MinStockAlert = p.MinStockAlert,
                UnitCost = p.Cost,
                UnitPrice = p.Price
            };
            InventoryValuationList.Add(valItem);
            invCost += valItem.TotalCostValue;
            invSell += valItem.TotalSellingValue;
            if (p.StockQuantity <= 0) outStock++;
        }
        TotalInventoryCostValue = invCost;
        TotalInventorySellingValue = invSell;
        ExpectedInventoryProfit = Math.Max(0, invSell - invCost);
        OutOfStockCount = outStock;
        OnPropertyChanged(nameof(InStockCount));
        OnPropertyChanged(nameof(LowStockCount));

        // 5. Customer Debts
        var debts = await _context.CustomerDebts.OrderByDescending(d => d.CreatedAt).ToListAsync();
        CustomerDebtsList.Clear();
        decimal dTotal = 0;
        decimal dPaid = 0;
        foreach (var d in debts)
        {
            CustomerDebtsList.Add(d);
            dTotal += d.TotalDebt;
            dPaid += d.TotalPaid;
        }
        TotalCustomerDebtsDue = dTotal;
        TotalCustomerDebtsCollected = dPaid;
        NetOutstandingCustomerDebts = Math.Max(0, dTotal - dPaid);

        // 6. Shift Audits
        var shifts = await _context.ShiftAudits.OrderByDescending(s => s.CreatedAt).ToListAsync();
        ShiftAuditsList.Clear();
        foreach (var s in shifts) ShiftAuditsList.Add(s);
        RecalculateShiftDiscrepancy();

        // 7. Cashier Performance & Peak Hours
        CashierPerformanceList.Clear();
        CashierPerformanceList.Add(new CashierPerformanceItem
        {
            CashierName = "محمد الكاشير (رئيسي)",
            InvoicesCount = Invoices.Count,
            TotalSalesAmount = TotalSalesRevenue,
            AvgSpeedText = "38 ثانية / وصل"
        });

        HourlyPeakList.Clear();
        var hourlyData = Enumerable.Range(8, 14).Select(hour =>
        {
            var hInvoices = Invoices.Where(i => i.CreatedAt.Hour == hour).ToList();
            int cnt = hInvoices.Count;
            decimal val = hInvoices.Sum(i => i.TotalAmount);
            string label = $"{hour:D2}:00 - {hour + 1:D2}:00";
            return new HourlyPeakItem
            {
                HourLabel = label,
                InvoicesCount = cnt,
                TotalSales = val,
                IntensityPercent = Invoices.Count > 0 ? (cnt * 100.0 / Invoices.Count) : 0
            };
        }).ToList();

        foreach (var h in hourlyData) HourlyPeakList.Add(h);
        var peak = hourlyData.OrderByDescending(h => h.InvoicesCount).FirstOrDefault();
        BusiestHourText = peak != null && peak.InvoicesCount > 0 ? $"{peak.HourLabel} ({peak.InvoicesCount} وصل)" : "لا توجد مبيعات كافية لتحديد الذروة بعد";

        // 8. Product Velocity & Stock Movement
        FastMovingProducts.Clear();
        SlowMovingProducts.Clear();
        DeadStockProducts.Clear();

        var soldGroups = Invoices.SelectMany(i => i.Items)
            .Where(i => i.ProductId.HasValue)
            .GroupBy(i => i.ProductId!.Value)
            .ToDictionary(g => g.Key, g => new { Qty = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.TotalPrice) });

        foreach (var p in allProducts)
        {
            if (soldGroups.TryGetValue(p.Id, out var stat))
            {
                var mov = new ProductMovementItem
                {
                    Barcode = p.Barcode,
                    ProductName = p.Name,
                    QuantitySold = stat.Qty,
                    TotalRevenue = stat.Revenue,
                    CurrentStock = p.StockQuantity,
                    MovementCategory = stat.Qty >= 10 ? "🔥 الأكثر مبيعاً" : "🟡 حركة عادية"
                };

                if (stat.Qty >= 5) FastMovingProducts.Add(mov);
                else SlowMovingProducts.Add(mov);
            }
            else
            {
                DeadStockProducts.Add(new ProductMovementItem
                {
                    Barcode = p.Barcode,
                    ProductName = p.Name,
                    QuantitySold = 0,
                    TotalRevenue = 0,
                    CurrentStock = p.StockQuantity,
                    MovementCategory = "❄️ بضاعة راكدة (0 مبيعات)"
                });
            }
        }
    }
}
