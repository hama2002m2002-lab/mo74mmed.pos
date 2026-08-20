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

public class CashierFilterOption
{
    public Guid? Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public class CashierCardItem : BaseViewModel
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Cashier";
    public string RoleDisplayName => Role switch
    {
        "Admin" => "مدير النظام (Admin)",
        "Manager" => "مشرف عام (Manager)",
        _ => "كاشير ومبيعات (Cashier)"
    };
    public bool IsActive { get; set; } = true;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public int TotalInvoicesCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public decimal TodaySalesAmount { get; set; }
    public decimal TodayReturnsAmount { get; set; }
    public decimal TodayNetProfit { get; set; }
}

public class UserAccountsViewModel : BaseViewModel
{
    private readonly AppDbContext _context;

    #region Top Financial Summary & Filter Controls

    public ObservableCollection<CashierFilterOption> CashierFilterOptions { get; } = new();

    private CashierFilterOption? _selectedCashierFilter;
    public CashierFilterOption? SelectedCashierFilter
    {
        get => _selectedCashierFilter;
        set
        {
            if (SetProperty(ref _selectedCashierFilter, value))
            {
                _ = RefreshDataAsync();
            }
        }
    }

    private string _summaryDatePreset = "Today";
    public string SummaryDatePreset
    {
        get => _summaryDatePreset;
        set => SetProperty(ref _summaryDatePreset, value);
    }

    private DateTime? _summaryDateFrom = DateTime.Today;
    public DateTime? SummaryDateFrom
    {
        get => _summaryDateFrom;
        set
        {
            if (SetProperty(ref _summaryDateFrom, value))
            {
                _ = RefreshDataAsync();
            }
        }
    }

    private DateTime? _summaryDateTo = DateTime.Today;
    public DateTime? SummaryDateTo
    {
        get => _summaryDateTo;
        set
        {
            if (SetProperty(ref _summaryDateTo, value))
            {
                _ = RefreshDataAsync();
            }
        }
    }

    private decimal _topTotalSales;
    public decimal TopTotalSales
    {
        get => _topTotalSales;
        set => SetProperty(ref _topTotalSales, value);
    }

    private decimal _topTotalReturns;
    public decimal TopTotalReturns
    {
        get => _topTotalReturns;
        set => SetProperty(ref _topTotalReturns, value);
    }

    private decimal _topNetSales;
    public decimal TopNetSales
    {
        get => _topNetSales;
        set => SetProperty(ref _topNetSales, value);
    }

    private decimal _topNetProfit;
    public decimal TopNetProfit
    {
        get => _topNetProfit;
        set => SetProperty(ref _topNetProfit, value);
    }

    private int _topInvoicesCount;
    public int TopInvoicesCount
    {
        get => _topInvoicesCount;
        set => SetProperty(ref _topInvoicesCount, value);
    }

    private int _topReturnedCount;
    public int TopReturnedCount
    {
        get => _topReturnedCount;
        set => SetProperty(ref _topReturnedCount, value);
    }

    #endregion

    #region Cards Grid State

    public ObservableCollection<CashierCardItem> CashierCards { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = LoadCashierCardsAsync();
            }
        }
    }

    private bool _isDetailsActive;
    public bool IsDetailsActive
    {
        get => _isDetailsActive;
        set
        {
            if (SetProperty(ref _isDetailsActive, value))
            {
                OnPropertyChanged(nameof(IsCardsGridActive));
            }
        }
    }

    public bool IsCardsGridActive => !IsDetailsActive;

    #endregion

    #region Selected Cashier Details & Sales History

    private CashierCardItem? _selectedCashier;
    public CashierCardItem? SelectedCashier
    {
        get => _selectedCashier;
        set => SetProperty(ref _selectedCashier, value);
    }

    public ObservableCollection<Sale> CashierSalesHistory { get; } = new();

    private DateTime? _dateFrom;
    public DateTime? DateFrom
    {
        get => _dateFrom;
        set
        {
            if (SetProperty(ref _dateFrom, value))
            {
                _ = FilterCashierSalesAsync();
            }
        }
    }

    private DateTime? _dateTo;
    public DateTime? DateTo
    {
        get => _dateTo;
        set
        {
            if (SetProperty(ref _dateTo, value))
            {
                _ = FilterCashierSalesAsync();
            }
        }
    }

    private decimal _cashierPeriodTotalSales;
    public decimal CashierPeriodTotalSales
    {
        get => _cashierPeriodTotalSales;
        set => SetProperty(ref _cashierPeriodTotalSales, value);
    }

    private int _cashierPeriodInvoicesCount;
    public int CashierPeriodInvoicesCount
    {
        get => _cashierPeriodInvoicesCount;
        set => SetProperty(ref _cashierPeriodInvoicesCount, value);
    }

    private decimal _cashierPeriodCashSales;
    public decimal CashierPeriodCashSales
    {
        get => _cashierPeriodCashSales;
        set => SetProperty(ref _cashierPeriodCashSales, value);
    }

    private decimal _cashierPeriodCardSales;
    public decimal CashierPeriodCardSales
    {
        get => _cashierPeriodCardSales;
        set => SetProperty(ref _cashierPeriodCardSales, value);
    }

    public ObservableCollection<CashDrawerMovement> DrawerMovements { get; } = new();

    #endregion

    #region Add / Edit Modal Form

    private bool _isAddEditModalOpen;
    public bool IsAddEditModalOpen
    {
        get => _isAddEditModalOpen;
        set => SetProperty(ref _isAddEditModalOpen, value);
    }

    private string _modalTitle = "إنشاء حساب كاشير جديد";
    public string ModalTitle
    {
        get => _modalTitle;
        set => SetProperty(ref _modalTitle, value);
    }

    private Guid? _editingUserId;
    public Guid? EditingUserId
    {
        get => _editingUserId;
        set => SetProperty(ref _editingUserId, value);
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _fullName = string.Empty;
    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    private string _pinPassword = string.Empty;
    public string PinPassword
    {
        get => _pinPassword;
        set => SetProperty(ref _pinPassword, value);
    }

    private string _role = "Cashier";
    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    private bool _isActive = true;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    #endregion

    #region Invoice Items Modal

    private bool _isInvoiceItemsModalOpen;
    public bool IsInvoiceItemsModalOpen
    {
        get => _isInvoiceItemsModalOpen;
        set => SetProperty(ref _isInvoiceItemsModalOpen, value);
    }

    private Sale? _selectedSale;
    public Sale? SelectedSale
    {
        get => _selectedSale;
        set => SetProperty(ref _selectedSale, value);
    }

    public ObservableCollection<SaleItem> SelectedSaleItems { get; } = new();

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand OpenAddUserModalCommand { get; }
    public ICommand OpenEditUserModalCommand { get; }
    public ICommand CloseAddEditModalCommand { get; }
    public ICommand SaveUserCommand { get; }
    public ICommand DeleteUserCommand { get; }
    public ICommand ToggleActiveCommand { get; }
    public ICommand OpenCashierDetailsCommand { get; }
    public ICommand BackToCardsGridCommand { get; }
    
    // Top Summary Preset Commands
    public ICommand SetTodaySummaryCommand { get; }
    public ICommand SetYesterdaySummaryCommand { get; }
    public ICommand SetWeekSummaryCommand { get; }
    public ICommand SetMonthSummaryCommand { get; }
    public ICommand SetAllSummaryCommand { get; }

    // Detail View Filter Commands
    public ICommand FilterTodaySalesCommand { get; }
    public ICommand FilterWeekSalesCommand { get; }
    public ICommand FilterMonthSalesCommand { get; }
    public ICommand FilterAllSalesCommand { get; }
    public ICommand OpenSaleItemsModalCommand { get; }
    public ICommand CloseSaleItemsModalCommand { get; }

    #endregion

    public UserAccountsViewModel()
    {
        _context = new AppDbContext();

        RefreshCommand = new AsyncRelayCommand(async () => await RefreshDataAsync());

        // Top Summary Presets
        SetTodaySummaryCommand = new AsyncRelayCommand(async () =>
        {
            SummaryDatePreset = "Today";
            SummaryDateFrom = DateTime.Today;
            SummaryDateTo = DateTime.Today;
            await RefreshDataAsync();
        });

        SetYesterdaySummaryCommand = new AsyncRelayCommand(async () =>
        {
            SummaryDatePreset = "Yesterday";
            SummaryDateFrom = DateTime.Today.AddDays(-1);
            SummaryDateTo = DateTime.Today.AddDays(-1);
            await RefreshDataAsync();
        });

        SetWeekSummaryCommand = new AsyncRelayCommand(async () =>
        {
            SummaryDatePreset = "Week";
            SummaryDateFrom = DateTime.Today.AddDays(-7);
            SummaryDateTo = DateTime.Today;
            await RefreshDataAsync();
        });

        SetMonthSummaryCommand = new AsyncRelayCommand(async () =>
        {
            SummaryDatePreset = "Month";
            SummaryDateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            SummaryDateTo = DateTime.Today;
            await RefreshDataAsync();
        });

        SetAllSummaryCommand = new AsyncRelayCommand(async () =>
        {
            SummaryDatePreset = "All";
            SummaryDateFrom = null;
            SummaryDateTo = null;
            await RefreshDataAsync();
        });

        OpenAddUserModalCommand = new RelayCommand(() =>
        {
            ClearForm();
            ModalTitle = "➕ إنشاء حساب كاشير / موظف جديد";
            IsAddEditModalOpen = true;
        });

        OpenEditUserModalCommand = new RelayCommand(param =>
        {
            CashierCardItem? target = param as CashierCardItem ?? SelectedCashier;
            if (target != null)
            {
                EditingUserId = target.Id;
                Username = target.Username;
                FullName = target.FullName;
                PinPassword = target.PasswordHash;
                Role = target.Role;
                IsActive = target.IsActive;
                ModalTitle = $"✏️ تعديل بيانات الحساب: {target.FullName}";
                IsAddEditModalOpen = true;
            }
        });

        CloseAddEditModalCommand = new RelayCommand(() =>
        {
            IsAddEditModalOpen = false;
        });

        SaveUserCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(FullName))
            {
                MessageBox.Show(Loc.IsKurdish ? "تکایە ناوی بەکارهێنەر و ناوی تەواو بنووسە." : "يرجى إدخال اسم المستخدم والاسم الكامل.", Loc.IsKurdish ? "ئاگاداری" : "بيانات ناقصة", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var db = new AppDbContext();
                if (EditingUserId.HasValue)
                {
                    var existing = await db.Users.FindAsync(EditingUserId.Value);
                    if (existing != null)
                    {
                        existing.Username = Username.Trim();
                        existing.FullName = FullName.Trim();
                        if (!string.IsNullOrWhiteSpace(PinPassword))
                        {
                            existing.PasswordHash = PinPassword.Trim();
                        }
                        existing.Role = Role;
                        existing.IsActive = IsActive;
                        existing.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    bool exists = await db.Users.AnyAsync(u => u.Username.ToLower() == Username.Trim().ToLower());
                    if (exists)
                    {
                        MessageBox.Show(Loc.IsKurdish ? "ئەم ناوی بەکارهێنەرە پێشتر بەکارهاتووە، تکایە ناوێکی تر هەڵبژێرە." : "اسم المستخدم هذا مستخدم مسبقاً، يرجى اختيار اسم آخر.", Loc.IsKurdish ? "ئاگاداری" : "تكرار", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var newUser = new User
                    {
                        Id = Guid.NewGuid(),
                        Username = Username.Trim(),
                        FullName = FullName.Trim(),
                        PasswordHash = string.IsNullOrWhiteSpace(PinPassword) ? "1234" : PinPassword.Trim(),
                        Role = Role,
                        IsActive = IsActive,
                        CreatedAt = DateTime.UtcNow
                    };
                    await db.Users.AddAsync(newUser);
                }

                await db.SaveChangesAsync();
                IsAddEditModalOpen = false;
                ClearForm();
                await RefreshDataAsync();

                if (SelectedCashier != null && EditingUserId == SelectedCashier.Id)
                {
                    SelectedCashier = CashierCards.FirstOrDefault(c => c.Id == EditingUserId.Value);
                }

                MessageBox.Show(Loc.IsKurdish ? "زانیاری هەژمار بە سەرکەوتوویی پاشەکەوت کرا!" : "تم حفظ بيانات الحساب بنجاح!", Loc.IsKurdish ? "سەرکەوتوو بوو" : "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ المستخدم: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        DeleteUserCommand = new AsyncRelayCommand(async param =>
        {
            CashierCardItem? target = param as CashierCardItem ?? SelectedCashier;
            if (target != null)
            {
                if (CashierCards.Count <= 1)
                {
                    MessageBox.Show(Loc.IsKurdish ? "ناتوانرێت تەنها بەکارهێنەری سیستەم بسڕدرێتەوە." : "لا يمكن حذف المستخدم الوحيد في النظام.", Loc.IsKurdish ? "ئاگاداری" : "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var res = MessageBox.Show(Loc.IsKurdish ? $"ئایا دڵنیایت لە سڕینەوەی هەژماری '{target.FullName}'؟" : $"هل ترغب في حذف حساب المستخدم '{target.FullName}' نهائياً؟", Loc.IsKurdish ? "سڕینەوە" : "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    using var db = new AppDbContext();
                    var user = await db.Users.FindAsync(target.Id);
                    if (user != null)
                    {
                        db.Users.Remove(user);
                        await db.SaveChangesAsync();
                    }

                    if (IsDetailsActive && SelectedCashier?.Id == target.Id)
                    {
                        IsDetailsActive = false;
                        SelectedCashier = null;
                    }

                    await RefreshDataAsync();
                }
            }
        });

        ToggleActiveCommand = new AsyncRelayCommand(async param =>
        {
            if (param is CashierCardItem target)
            {
                using var db = new AppDbContext();
                var user = await db.Users.FindAsync(target.Id);
                if (user != null)
                {
                    user.IsActive = !user.IsActive;
                    user.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    await RefreshDataAsync();
                }
            }
        });

        OpenCashierDetailsCommand = new AsyncRelayCommand(async param =>
        {
            if (param is CashierCardItem item)
            {
                SelectedCashier = item;
                IsDetailsActive = true;
                _dateFrom = SummaryDateFrom;
                _dateTo = SummaryDateTo;
                OnPropertyChanged(nameof(DateFrom));
                OnPropertyChanged(nameof(DateTo));
                await FilterCashierSalesAsync();
            }
        });

        BackToCardsGridCommand = new RelayCommand(() =>
        {
            IsDetailsActive = false;
            SelectedCashier = null;
            CashierSalesHistory.Clear();
            _ = RefreshDataAsync();
        });

        FilterTodaySalesCommand = new AsyncRelayCommand(async () =>
        {
            DateFrom = DateTime.Today;
            DateTo = DateTime.Today.AddDays(1).AddTicks(-1);
            await FilterCashierSalesAsync();
        });

        FilterWeekSalesCommand = new AsyncRelayCommand(async () =>
        {
            DateFrom = DateTime.Today.AddDays(-7);
            DateTo = DateTime.Today.AddDays(1).AddTicks(-1);
            await FilterCashierSalesAsync();
        });

        FilterMonthSalesCommand = new AsyncRelayCommand(async () =>
        {
            DateFrom = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTo = DateTime.Today.AddDays(1).AddTicks(-1);
            await FilterCashierSalesAsync();
        });

        FilterAllSalesCommand = new AsyncRelayCommand(async () =>
        {
            DateFrom = null;
            DateTo = null;
            await FilterCashierSalesAsync();
        });

        OpenSaleItemsModalCommand = new RelayCommand(param =>
        {
            if (param is Sale s)
            {
                SelectedSale = s;
                SelectedSaleItems.Clear();
                foreach (var item in s.Items)
                {
                    SelectedSaleItems.Add(item);
                }
                IsInvoiceItemsModalOpen = true;
            }
        });

        CloseSaleItemsModalCommand = new RelayCommand(() =>
        {
            IsInvoiceItemsModalOpen = false;
            SelectedSale = null;
            SelectedSaleItems.Clear();
        });
    }

    public async Task InitializeAsync()
    {
        await RefreshDataAsync();
    }

    public async Task RefreshDataAsync()
    {
        await LoadCashierFilterOptionsAsync();
        await CalculateTopSummaryAsync();
        await LoadCashierCardsAsync();
        await LoadDrawerMovementsAsync();
    }

    private async Task LoadDrawerMovementsAsync()
    {
        var movements = await _context.CashDrawerMovements
            .AsNoTracking()
            .OrderByDescending(m => m.CreatedAt)
            .Take(50)
            .ToListAsync();

        DrawerMovements.Clear();
        foreach (var m in movements)
        {
            DrawerMovements.Add(m);
        }
    }

    public async Task LoadUsersAsync()
    {
        await RefreshDataAsync();
    }

    private async Task LoadCashierFilterOptionsAsync()
    {
        var users = await _context.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
        
        Guid? currentSelectedId = SelectedCashierFilter?.Id;
        
        CashierFilterOptions.Clear();
        CashierFilterOptions.Add(new CashierFilterOption { Id = null, DisplayName = "👥 جميع الكاشير والموظفين (الكل)" });
        
        foreach (var u in users)
        {
            CashierFilterOptions.Add(new CashierFilterOption { Id = u.Id, DisplayName = $"👤 {u.FullName} (@{u.Username})" });
        }

        if (currentSelectedId.HasValue)
        {
            _selectedCashierFilter = CashierFilterOptions.FirstOrDefault(o => o.Id == currentSelectedId.Value) ?? CashierFilterOptions.First();
        }
        else
        {
            _selectedCashierFilter = CashierFilterOptions.First();
        }
        OnPropertyChanged(nameof(SelectedCashierFilter));
    }

    private async Task CalculateTopSummaryAsync()
    {
        var query = _context.Sales
            .Include(s => s.Items)
            .ThenInclude(i => i.Product)
            .AsNoTracking()
            .AsQueryable();

        // Cashier filter
        if (SelectedCashierFilter != null && SelectedCashierFilter.Id.HasValue)
        {
            Guid cashierId = SelectedCashierFilter.Id.Value;
            query = query.Where(s => s.UserId == cashierId);
        }

        // Date range filter
        if (SummaryDateFrom.HasValue)
        {
            DateTime dfUtc = DateTime.SpecifyKind(SummaryDateFrom.Value.Date, DateTimeKind.Utc);
            query = query.Where(s => s.CreatedAt >= dfUtc);
        }

        if (SummaryDateTo.HasValue)
        {
            DateTime dtUtc = DateTime.SpecifyKind(SummaryDateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(s => s.CreatedAt <= dtUtc);
        }

        var salesList = await query.ToListAsync();

        var completedSales = salesList.Where(s => s.Status != "Returned" && s.Status != "Refunded").ToList();
        var returnedSales = salesList.Where(s => s.Status == "Returned" || s.Status == "Refunded").ToList();

        decimal totalSales = completedSales.Sum(s => s.TotalAmount);
        decimal totalReturns = returnedSales.Sum(s => s.TotalAmount);
        decimal netSales = totalSales - totalReturns;

        decimal netProfit = 0m;
        foreach (var s in completedSales)
        {
            netProfit += s.InvoiceNetProfit;
        }

        TopTotalSales = totalSales;
        TopTotalReturns = totalReturns;
        TopNetSales = netSales;
        TopNetProfit = netProfit;
        TopInvoicesCount = completedSales.Count;
        TopReturnedCount = returnedSales.Count;
    }

    public async Task LoadCashierCardsAsync()
    {
        var query = _context.Users.AsNoTracking().AsQueryable();
        
        // If specific cashier filter is selected, filter cards
        if (SelectedCashierFilter != null && SelectedCashierFilter.Id.HasValue)
        {
            query = query.Where(u => u.Id == SelectedCashierFilter.Id.Value);
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string q = SearchQuery.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(q) || u.Username.ToLower().Contains(q));
        }

        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        
        var allSalesQuery = _context.Sales.Include(s => s.Items).ThenInclude(i => i.Product).AsNoTracking().AsQueryable();
        
        if (SummaryDateFrom.HasValue)
        {
            DateTime dfUtc = DateTime.SpecifyKind(SummaryDateFrom.Value.Date, DateTimeKind.Utc);
            allSalesQuery = allSalesQuery.Where(s => s.CreatedAt >= dfUtc);
        }
        if (SummaryDateTo.HasValue)
        {
            DateTime dtUtc = DateTime.SpecifyKind(SummaryDateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            allSalesQuery = allSalesQuery.Where(s => s.CreatedAt <= dtUtc);
        }

        var allFilteredSales = await allSalesQuery.ToListAsync();

        CashierCards.Clear();
        foreach (var u in users)
        {
            var userSales = allFilteredSales.Where(s => s.UserId == u.Id || (s.UserId == null && u.Role == "Admin")).ToList();
            var userCompleted = userSales.Where(s => s.Status != "Returned" && s.Status != "Refunded").ToList();
            var userReturned = userSales.Where(s => s.Status == "Returned" || s.Status == "Refunded").ToList();

            decimal userProfit = 0m;
            foreach (var s in userCompleted)
            {
                userProfit += s.InvoiceNetProfit;
            }

            var card = new CashierCardItem
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive,
                PasswordHash = u.PasswordHash,
                CreatedAt = u.CreatedAt,
                TotalInvoicesCount = userCompleted.Count,
                TotalSalesAmount = userCompleted.Sum(s => s.TotalAmount),
                TodaySalesAmount = userCompleted.Sum(s => s.TotalAmount),
                TodayReturnsAmount = userReturned.Sum(s => s.TotalAmount),
                TodayNetProfit = userProfit
            };
            CashierCards.Add(card);
        }
    }

    public async Task FilterCashierSalesAsync()
    {
        if (SelectedCashier == null) return;

        var salesQuery = _context.Sales
            .Include(s => s.Items)
            .AsNoTracking()
            .Where(s => s.UserId == SelectedCashier.Id || (s.UserId == null && SelectedCashier.Role == "Admin"))
            .AsQueryable();

        if (DateFrom.HasValue)
        {
            DateTime dfUtc = DateTime.SpecifyKind(DateFrom.Value.Date, DateTimeKind.Utc);
            salesQuery = salesQuery.Where(s => s.CreatedAt >= dfUtc);
        }

        if (DateTo.HasValue)
        {
            DateTime dtUtc = DateTime.SpecifyKind(DateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            salesQuery = salesQuery.Where(s => s.CreatedAt <= dtUtc);
        }

        var list = await salesQuery.OrderByDescending(s => s.CreatedAt).ToListAsync();

        CashierSalesHistory.Clear();
        foreach (var s in list)
        {
            CashierSalesHistory.Add(s);
        }

        var completed = list.Where(s => s.Status != "Returned" && s.Status != "Refunded").ToList();
        CashierPeriodInvoicesCount = completed.Count;
        CashierPeriodTotalSales = completed.Sum(s => s.TotalAmount);
        CashierPeriodCashSales = completed.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);
        CashierPeriodCardSales = completed.Where(s => s.PaymentMethod != "Cash").Sum(s => s.TotalAmount);
    }

    public void ClearForm()
    {
        EditingUserId = null;
        Username = string.Empty;
        FullName = string.Empty;
        PinPassword = string.Empty;
        Role = "Cashier";
        IsActive = true;
    }
}
