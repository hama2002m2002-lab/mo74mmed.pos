using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.ViewModels;

public class SupplierCardItem : BaseViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int ProductsCount { get; set; }
    public int InvoicesCount { get; set; }
    public decimal Balance { get; set; }
    public decimal TotalStockValue { get; set; }
}

public class SuppliersViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly ISupplierService _supplierService;

    #region Cards Grid State

    public ObservableCollection<SupplierCardItem> SupplierCards { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = LoadSuppliersAsync();
            }
        }
    }

    private bool _isSupplierDetailsActive;
    public bool IsSupplierDetailsActive
    {
        get => _isSupplierDetailsActive;
        set
        {
            if (SetProperty(ref _isSupplierDetailsActive, value))
            {
                OnPropertyChanged(nameof(IsCardsGridActive));
            }
        }
    }

    public bool IsCardsGridActive => !IsSupplierDetailsActive;

    #endregion

    #region Active Supplier Details

    private Supplier? _selectedSupplier;
    public Supplier? SelectedSupplier
    {
        get => _selectedSupplier;
        set => SetProperty(ref _selectedSupplier, value);
    }

    public ObservableCollection<Product> SupplierProducts { get; } = new();
    public ObservableCollection<PurchaseInvoice> SupplierInvoices { get; } = new();
    public ObservableCollection<SupplierTransaction> SupplierTransactions { get; } = new();

    private decimal _supplierTotalStockValue;
    public decimal SupplierTotalStockValue
    {
        get => _supplierTotalStockValue;
        set => SetProperty(ref _supplierTotalStockValue, value);
    }

    private decimal _supplierTotalPaid;
    public decimal SupplierTotalPaid
    {
        get => _supplierTotalPaid;
        set => SetProperty(ref _supplierTotalPaid, value);
    }

    private decimal _supplierNetBalance;
    public decimal SupplierNetBalance
    {
        get => _supplierNetBalance;
        set => SetProperty(ref _supplierNetBalance, value);
    }

    private int _supplierProductsCount;
    public int SupplierProductsCount
    {
        get => _supplierProductsCount;
        set => SetProperty(ref _supplierProductsCount, value);
    }

    private int _supplierInvoicesCount;
    public int SupplierInvoicesCount
    {
        get => _supplierInvoicesCount;
        set => SetProperty(ref _supplierInvoicesCount, value);
    }

    // Detail Tabs ("Invoices", "Products", "Ledger")
    private string _activeSupplierTab = "Invoices";
    public string ActiveSupplierTab
    {
        get => _activeSupplierTab;
        set
        {
            if (SetProperty(ref _activeSupplierTab, value))
            {
                OnPropertyChanged(nameof(IsInvoicesTabActive));
                OnPropertyChanged(nameof(IsProductsTabActive));
                OnPropertyChanged(nameof(IsLedgerTabActive));
            }
        }
    }

    public bool IsInvoicesTabActive => ActiveSupplierTab == "Invoices";
    public bool IsProductsTabActive => ActiveSupplierTab == "Products";
    public bool IsLedgerTabActive => ActiveSupplierTab == "Ledger";

    #endregion

    #region Add Supplier Modal Form

    private bool _isAddSupplierModalOpen;
    public bool IsAddSupplierModalOpen
    {
        get => _isAddSupplierModalOpen;
        set => SetProperty(ref _isAddSupplierModalOpen, value);
    }

    private string _inputName = string.Empty;
    public string InputName { get => _inputName; set => SetProperty(ref _inputName, value); }

    private string _inputPhone = string.Empty;
    public string InputPhone { get => _inputPhone; set => SetProperty(ref _inputPhone, value); }

    private string _inputCompany = string.Empty;
    public string InputCompany { get => _inputCompany; set => SetProperty(ref _inputCompany, value); }

    private string _inputAddress = string.Empty;
    public string InputAddress { get => _inputAddress; set => SetProperty(ref _inputAddress, value); }

    private decimal _inputOpeningBalance;
    public decimal InputOpeningBalance { get => _inputOpeningBalance; set => SetProperty(ref _inputOpeningBalance, value); }

    private string _inputNotes = string.Empty;
    public string InputNotes { get => _inputNotes; set => SetProperty(ref _inputNotes, value); }

    #endregion

    #region Payment Modal

    private bool _isPaymentModalOpen;
    public bool IsPaymentModalOpen
    {
        get => _isPaymentModalOpen;
        set => SetProperty(ref _isPaymentModalOpen, value);
    }

    private decimal _paymentAmount;
    public decimal PaymentAmount { get => _paymentAmount; set => SetProperty(ref _paymentAmount, value); }

    private string _paymentNotes = string.Empty;
    public string PaymentNotes { get => _paymentNotes; set => SetProperty(ref _paymentNotes, value); }

    private string _paymentReceiptNumber = string.Empty;
    public string PaymentReceiptNumber { get => _paymentReceiptNumber; set => SetProperty(ref _paymentReceiptNumber, value); }

    #endregion

    #region Receipt Image Modal Viewer

    private bool _isReceiptImageModalOpen;
    public bool IsReceiptImageModalOpen
    {
        get => _isReceiptImageModalOpen;
        set => SetProperty(ref _isReceiptImageModalOpen, value);
    }

    private string _currentReceiptImagePath = string.Empty;
    public string CurrentReceiptImagePath
    {
        get => _currentReceiptImagePath;
        set => SetProperty(ref _currentReceiptImagePath, value);
    }

    private string _currentReceiptInvoiceNumber = string.Empty;
    public string CurrentReceiptInvoiceNumber
    {
        get => _currentReceiptInvoiceNumber;
        set => SetProperty(ref _currentReceiptInvoiceNumber, value);
    }

    private PurchaseInvoice? _selectedInvoiceForImage;
    public PurchaseInvoice? SelectedInvoiceForImage
    {
        get => _selectedInvoiceForImage;
        set => SetProperty(ref _selectedInvoiceForImage, value);
    }

    #endregion

    #region Commands

    public ICommand RefreshCommand { get; }
    public ICommand OpenAddSupplierModalCommand { get; }
    public ICommand CloseAddSupplierModalCommand { get; }
    public ICommand SaveNewSupplierCommand { get; }
    public ICommand ClearNewSupplierFormCommand { get; }
    public ICommand OpenSupplierDetailsCommand { get; }
    public ICommand BackToCardsGridCommand { get; }
    public ICommand DeleteSupplierCommand { get; }

    public ICommand ShowInvoicesTabCommand { get; }
    public ICommand ShowProductsTabCommand { get; }
    public ICommand ShowLedgerTabCommand { get; }

    public ICommand OpenPaymentModalCommand { get; }
    public ICommand ClosePaymentModalCommand { get; }
    public ICommand ConfirmPaymentCommand { get; }

    public ICommand AttachReceiptImageCommand { get; }
    public ICommand ViewReceiptImageCommand { get; }
    public ICommand DeleteReceiptImageCommand { get; }
    public ICommand CloseReceiptImageModalCommand { get; }

    public ICommand BackToMainCommand { get; }

    public event Action? RequestBackToNavigation;

    #endregion

    public SuppliersViewModel()
    {
        _context = new AppDbContext();
        _supplierService = new SupplierService(_context);

        RefreshCommand = new AsyncRelayCommand(async () =>
        {
            if (IsSupplierDetailsActive && SelectedSupplier != null)
            {
                await LoadSupplierDetailsAsync(SelectedSupplier.Id);
            }
            else
            {
                await LoadSuppliersAsync();
            }
        });

        OpenAddSupplierModalCommand = new RelayCommand(() =>
        {
            ClearNewSupplierForm();
            IsAddSupplierModalOpen = true;
        });

        CloseAddSupplierModalCommand = new RelayCommand(() =>
        {
            IsAddSupplierModalOpen = false;
        });

        SaveNewSupplierCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(InputName))
            {
                MessageBox.Show("يرجى إدخال اسم المندوب.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var supplier = new Supplier
            {
                Id = Guid.NewGuid(),
                Name = InputName.Trim(),
                Phone = InputPhone.Trim(),
                Company = InputCompany.Trim(),
                Address = InputAddress.Trim(),
                OpeningBalance = InputOpeningBalance,
                Balance = InputOpeningBalance,
                Notes = InputNotes.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _supplierService.SaveSupplierAsync(supplier);
            ClearNewSupplierForm();
            IsAddSupplierModalOpen = false;
            await LoadSuppliersAsync();
            MessageBox.Show($"تم حفظ المندوب '{supplier.Name}' بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        ClearNewSupplierFormCommand = new RelayCommand(ClearNewSupplierForm);

        OpenSupplierDetailsCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is SupplierCardItem card)
            {
                await LoadSupplierDetailsAsync(card.Id);
                IsSupplierDetailsActive = true;
            }
        });

        BackToCardsGridCommand = new AsyncRelayCommand(async () =>
        {
            IsSupplierDetailsActive = false;
            await LoadSuppliersAsync();
        });

        DeleteSupplierCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedSupplier == null) return;
            var res = MessageBox.Show($"هل ترغب في حذف المندوب '{SelectedSupplier.Name}'؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                await _supplierService.DeleteSupplierAsync(SelectedSupplier.Id);
                IsSupplierDetailsActive = false;
                await LoadSuppliersAsync();
            }
        });

        ShowInvoicesTabCommand = new RelayCommand(() => ActiveSupplierTab = "Invoices");
        ShowProductsTabCommand = new RelayCommand(() => ActiveSupplierTab = "Products");
        ShowLedgerTabCommand = new RelayCommand(() => ActiveSupplierTab = "Ledger");

        OpenPaymentModalCommand = new RelayCommand(() =>
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("يرجى اختيار مندوب أولاً لتسجيل الدفعة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            PaymentAmount = 0;
            PaymentNotes = $"سداد دفعة نقدية للمندوب {SelectedSupplier.Name}";
            PaymentReceiptNumber = $"PAY-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
            IsPaymentModalOpen = true;
        });

        ClosePaymentModalCommand = new RelayCommand(() =>
        {
            IsPaymentModalOpen = false;
        });

        ConfirmPaymentCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedSupplier == null || PaymentAmount <= 0)
            {
                MessageBox.Show("يرجى إدخال مبلغ صحيح للدفعة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _supplierService.AddTransactionAsync(SelectedSupplier.Id, "Payment", PaymentAmount, PaymentNotes, PaymentReceiptNumber);
            IsPaymentModalOpen = false;
            await LoadSupplierDetailsAsync(SelectedSupplier.Id);
            MessageBox.Show($"تم تسجيل دفعة بقيمة {PaymentAmount:N0} د.ع بنجاح للمندوب '{SelectedSupplier.Name}'.", "نجاح السداد", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        AttachReceiptImageCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is PurchaseInvoice invoice)
            {
                var dialog = new OpenFileDialog
                {
                    Title = $"اختيار صورة وصل المندوب - فاتورة {invoice.InvoiceNumber}",
                    Filter = "ملفات الصور (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|كل الملفات (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        string receiptsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts");
                        if (!Directory.Exists(receiptsDir))
                        {
                            Directory.CreateDirectory(receiptsDir);
                        }

                        string ext = Path.GetExtension(dialog.FileName);
                        string newFileName = $"receipt_{invoice.Id}_{DateTime.Now.Ticks}{ext}";
                        string destPath = Path.Combine(receiptsDir, newFileName);

                        File.Copy(dialog.FileName, destPath, true);

                        invoice.ReceiptImagePath = destPath;

                        var dbInvoice = await _context.PurchaseInvoices.FindAsync(invoice.Id);
                        if (dbInvoice != null)
                        {
                            dbInvoice.ReceiptImagePath = destPath;
                            dbInvoice.UpdatedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                        }

                        if (SelectedSupplier != null)
                        {
                            await LoadSupplierDetailsAsync(SelectedSupplier.Id);
                        }

                        MessageBox.Show("تم إرفاق وحفظ صورة وصل المندوب بنجاح!", "تم الحفظ", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"حدث خطأ أثناء حفظ الصورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        });

        ViewReceiptImageCommand = new RelayCommand((param) =>
        {
            if (param is PurchaseInvoice invoice)
            {
                if (string.IsNullOrWhiteSpace(invoice.ReceiptImagePath) || !File.Exists(invoice.ReceiptImagePath))
                {
                    MessageBox.Show("لم يتم إرفاق صورة لهذا الوصل بعد.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                SelectedInvoiceForImage = invoice;
                CurrentReceiptImagePath = invoice.ReceiptImagePath;
                CurrentReceiptInvoiceNumber = invoice.InvoiceNumber;
                IsReceiptImageModalOpen = true;
            }
        });

        DeleteReceiptImageCommand = new AsyncRelayCommand(async (param) =>
        {
            if (param is PurchaseInvoice invoice)
            {
                var res = MessageBox.Show("هل ترغب في حذف صورة الوصل المرفقة؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    invoice.ReceiptImagePath = null;
                    var dbInvoice = await _context.PurchaseInvoices.FindAsync(invoice.Id);
                    if (dbInvoice != null)
                    {
                        dbInvoice.ReceiptImagePath = null;
                        await _context.SaveChangesAsync();
                    }
                    IsReceiptImageModalOpen = false;
                    if (SelectedSupplier != null) await LoadSupplierDetailsAsync(SelectedSupplier.Id);
                }
            }
        });

        CloseReceiptImageModalCommand = new RelayCommand(() =>
        {
            IsReceiptImageModalOpen = false;
        });

        BackToMainCommand = new RelayCommand(() => RequestBackToNavigation?.Invoke());
    }

    public async Task InitializeAsync()
    {
        IsSupplierDetailsActive = false;
        IsAddSupplierModalOpen = false;
        IsPaymentModalOpen = false;
        IsReceiptImageModalOpen = false;
        await LoadSuppliersAsync();
    }

    public async Task LoadSuppliersAsync()
    {
        var suppliers = await _supplierService.GetSuppliersAsync(SearchQuery);
        var invoices = await _context.PurchaseInvoices.AsNoTracking().ToListAsync();

        SupplierCards.Clear();
        foreach (var s in suppliers)
        {
            decimal totalStockVal = s.Products.Where(p => !p.IsDeleted).Sum(p => p.Cost * p.StockQuantity);
            int invCount = invoices.Count(i => i.SupplierId == s.Id || (i.SupplierName != null && i.SupplierName.Equals(s.Name, StringComparison.OrdinalIgnoreCase)));

            SupplierCards.Add(new SupplierCardItem
            {
                Id = s.Id,
                Name = s.Name,
                Company = string.IsNullOrWhiteSpace(s.Company) ? "غير محدد" : s.Company,
                Phone = string.IsNullOrWhiteSpace(s.Phone) ? "--" : s.Phone,
                Address = string.IsNullOrWhiteSpace(s.Address) ? "--" : s.Address,
                Notes = s.Notes ?? string.Empty,
                ProductsCount = s.Products.Count(p => !p.IsDeleted),
                InvoicesCount = invCount,
                Balance = s.Balance,
                TotalStockValue = totalStockVal
            });
        }
    }

    public async Task LoadSupplierDetailsAsync(Guid supplierId)
    {
        SupplierProducts.Clear();
        SupplierInvoices.Clear();
        SupplierTransactions.Clear();

        var fullSupplier = await _supplierService.GetSupplierByIdAsync(supplierId);
        if (fullSupplier == null)
        {
            SelectedSupplier = null;
            return;
        }

        SelectedSupplier = fullSupplier;

        // 1. Products
        decimal totalGoodsValue = 0;
        foreach (var p in fullSupplier.Products.Where(p => !p.IsDeleted))
        {
            SupplierProducts.Add(p);
            totalGoodsValue += (p.Cost * p.StockQuantity);
        }

        // 2. Invoices
        var invList = await _context.PurchaseInvoices
            .AsNoTracking()
            .Where(pi => !pi.IsDeleted && (pi.SupplierId == fullSupplier.Id || pi.SupplierName == fullSupplier.Name))
            .Include(pi => pi.Items)
            .OrderByDescending(pi => pi.CreatedAt)
            .ToListAsync();

        foreach (var inv in invList)
        {
            SupplierInvoices.Add(inv);
        }

        // 3. Transactions
        var txs = await _supplierService.GetSupplierTransactionsAsync(fullSupplier.Id);
        decimal totalPaid = 0;
        foreach (var t in txs)
        {
            SupplierTransactions.Add(t);
            if (t.TransactionType == "Payment") totalPaid += t.Amount;
        }

        SupplierProductsCount = SupplierProducts.Count;
        SupplierInvoicesCount = SupplierInvoices.Count;
        SupplierTotalStockValue = totalGoodsValue;
        SupplierTotalPaid = totalPaid;
        SupplierNetBalance = fullSupplier.OpeningBalance + totalGoodsValue - totalPaid;
    }

    private void ClearNewSupplierForm()
    {
        InputName = string.Empty;
        InputPhone = string.Empty;
        InputCompany = string.Empty;
        InputAddress = string.Empty;
        InputOpeningBalance = 0;
        InputNotes = string.Empty;
    }
}
