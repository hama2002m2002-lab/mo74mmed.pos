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

public class PurchaseItemRow : BaseViewModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public bool IsCarton { get; set; }
    public int ItemsPerCarton { get; set; } = 1;
    public decimal Quantity { get; set; } = 1;
    public decimal OldStockQuantity { get; set; } // الكمية القديمة بالمخزن
    public decimal OldCost { get; set; } // السعر القديم
    public decimal NewCost { get; set; } // السعر الجديد
    public decimal PieceCost { get; set; } // السعر المرجح الجديد للقطعة
    public decimal CartonCost { get; set; } // السعر المرجح الجديد للكرتون
    public decimal SellingPrice { get; set; }
    public decimal TotalCost => Quantity * NewCost;
    public string PackageText => IsCarton ? $"كرتون ({ItemsPerCarton} قطعة)" : "مفرد (قطعة)";
}

public class PurchaseViewModel : BaseViewModel
{
    private readonly AppDbContext _context;
    private readonly IProductService _productService;

    #region Top Header & Invoice Meta

    private string _invoiceNumber = string.Empty;
    public string InvoiceNumber
    {
        get => _invoiceNumber;
        set => SetProperty(ref _invoiceNumber, value);
    }

    private DateTime _invoiceDate = DateTime.Today;
    public DateTime InvoiceDate
    {
        get => _invoiceDate;
        set => SetProperty(ref _invoiceDate, value);
    }

    private string _paymentType = "Cash"; // "Cash" (نقداً) or "Debt" (آجل)
    public string PaymentType
    {
        get => _paymentType;
        set
        {
            if (SetProperty(ref _paymentType, value))
            {
                OnPropertyChanged(nameof(IsCashPayment));
                OnPropertyChanged(nameof(IsDebtPayment));
                RecalculateTotals();
            }
        }
    }

    public bool IsCashPayment => PaymentType == "Cash";
    public bool IsDebtPayment => PaymentType == "Debt";

    private Supplier? _selectedSupplier;
    public Supplier? SelectedSupplier
    {
        get => _selectedSupplier;
        set => SetProperty(ref _selectedSupplier, value);
    }

    private int _historyCount;
    public int HistoryCount
    {
        get => _historyCount;
        set => SetProperty(ref _historyCount, value);
    }

    #endregion

    #region Product Search & Live Calculation Fields

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = FilterProductsAsync();
            }
        }
    }

    private Product? _selectedProduct;
    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                OnProductSelected();
            }
        }
    }

    private decimal _quantity = 1.0m;
    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                RecalculateCurrentItemMetrics();
            }
        }
    }

    private bool _isCartonMode = true; // Default to Carton as in screenshot
    public bool IsCartonMode
    {
        get => _isCartonMode;
        set
        {
            if (SetProperty(ref _isCartonMode, value))
            {
                OnPropertyChanged(nameof(IsSingleMode));
                OnPropertyChanged(nameof(PackageBadgeText));
                OnPropertyChanged(nameof(CostHeaderLabel));
                OnPropertyChanged(nameof(OldCostHeaderLabel));
                OnProductSelected();
            }
        }
    }

    public bool IsSingleMode => !IsCartonMode;
    public string PackageBadgeText => IsCartonMode ? $"كرتون ({ItemsPerCarton} قطعة)" : "مفرد (قطعة)";
    public string CostHeaderLabel => IsCartonMode ? "سعر الشراء الجديد (للكرتون):" : "سعر الشراء الجديد (للقطعة):";
    public string OldCostHeaderLabel => IsCartonMode ? "الشراء القديم (للكرتون):" : "الشراء القديم (للقطعة):";

    private int _itemsPerCarton = 24;
    public int ItemsPerCarton
    {
        get => _itemsPerCarton;
        set
        {
            if (SetProperty(ref _itemsPerCarton, value))
            {
                OnPropertyChanged(nameof(PackageBadgeText));
                RecalculateCurrentItemMetrics();
            }
        }
    }

    private decimal _currentWarehouseStock = 0m;
    public decimal CurrentWarehouseStock
    {
        get => _currentWarehouseStock;
        set => SetProperty(ref _currentWarehouseStock, value);
    }

    private decimal _oldCost = 12000m;
    public decimal OldCost
    {
        get => _oldCost;
        set => SetProperty(ref _oldCost, value);
    }

    private decimal _newCost = 12000m;
    public decimal NewCost
    {
        get => _newCost;
        set
        {
            if (SetProperty(ref _newCost, value))
            {
                RecalculateCurrentItemMetrics();
            }
        }
    }

    // ===============================================================
    // WEIGHTED AVERAGE COST METRICS (المتوسط المرجح للبضاعة)
    // ===============================================================
    private decimal _calculatedPieceCost = 500m;
    public decimal CalculatedPieceCost
    {
        get => _calculatedPieceCost;
        set => SetProperty(ref _calculatedPieceCost, value);
    }

    private decimal _calculatedCartonCost = 12000m;
    public decimal CalculatedCartonCost
    {
        get => _calculatedCartonCost;
        set => SetProperty(ref _calculatedCartonCost, value);
    }

    private decimal _calculatedItemTotal = 12000m;
    public decimal CalculatedItemTotal
    {
        get => _calculatedItemTotal;
        set => SetProperty(ref _calculatedItemTotal, value);
    }

    private decimal _sellingPrice = 750m;
    public decimal SellingPrice
    {
        get => _sellingPrice;
        set => SetProperty(ref _sellingPrice, value);
    }

    private string _costCalculationMethod = "المتوسط المرجح";
    public string CostCalculationMethod
    {
        get => _costCalculationMethod;
        set => SetProperty(ref _costCalculationMethod, value);
    }

    #endregion

    #region Collections & Lists

    public ObservableCollection<Supplier> SuppliersList { get; } = new();
    public ObservableCollection<Product> FilteredProducts { get; } = new();
    public ObservableCollection<PurchaseItemRow> InvoiceItems { get; } = new();
    public ObservableCollection<PurchaseInvoice> PurchaseHistoryList { get; } = new();

    public bool HasItems => InvoiceItems.Any();
    public bool IsEmpty => !HasItems;

    #endregion

    #region Bottom Summary

    private decimal _totalInvoiceAmount;
    public decimal TotalInvoiceAmount
    {
        get => _totalInvoiceAmount;
        set
        {
            if (SetProperty(ref _totalInvoiceAmount, value))
            {
                OnPropertyChanged(nameof(RemainingDebtAmount));
            }
        }
    }

    private decimal _paidAmount;
    public decimal PaidAmount
    {
        get => _paidAmount;
        set
        {
            if (SetProperty(ref _paidAmount, value))
            {
                OnPropertyChanged(nameof(RemainingDebtAmount));
            }
        }
    }

    public decimal RemainingDebtAmount => Math.Max(0, TotalInvoiceAmount - PaidAmount);

    private int _totalItemsCount;
    public int TotalItemsCount
    {
        get => _totalItemsCount;
        set => SetProperty(ref _totalItemsCount, value);
    }

    private bool _isHistoryModalOpen;
    public bool IsHistoryModalOpen
    {
        get => _isHistoryModalOpen;
        set => SetProperty(ref _isHistoryModalOpen, value);
    }

    #endregion

    #region Barcode & Package Modal (تعديل الباركود وعدد القطع داخل الكرتون)

    private bool _isBarcodePackageModalOpen;
    public bool IsBarcodePackageModalOpen
    {
        get => _isBarcodePackageModalOpen;
        set => SetProperty(ref _isBarcodePackageModalOpen, value);
    }

    private string _editModalBarcode = string.Empty;
    public string EditModalBarcode
    {
        get => _editModalBarcode;
        set => SetProperty(ref _editModalBarcode, value);
    }

    private int _editModalItemsPerCarton = 24;
    public int EditModalItemsPerCarton
    {
        get => _editModalItemsPerCarton;
        set => SetProperty(ref _editModalItemsPerCarton, value);
    }

    private string _currentItemBarcode = string.Empty;
    public string CurrentItemBarcode
    {
        get => _currentItemBarcode;
        set => SetProperty(ref _currentItemBarcode, value);
    }

    #endregion

    #region Commands

    public ICommand SelectCartonModeCommand { get; }
    public ICommand SelectSingleModeCommand { get; }
    public ICommand SetCashPaymentCommand { get; }
    public ICommand SetDebtPaymentCommand { get; }
    public ICommand AddItemToInvoiceCommand { get; }
    public ICommand RemoveItemCommand { get; }
    public ICommand ResetNewInvoiceCommand { get; }
    public ICommand OpenHistoryModalCommand { get; }
    public ICommand CloseHistoryModalCommand { get; }
    public ICommand OpenBarcodePackageModalCommand { get; }
    public ICommand SaveBarcodePackageModalCommand { get; }
    public ICommand CloseBarcodePackageModalCommand { get; }
    public ICommand CompletePurchaseInvoiceCommand { get; }
    public ICommand BackToMainCommand { get; }
    public ICommand NavigateToCashierCommand { get; }
    public ICommand NavigateToReportsCommand { get; }
    public ICommand NavigateToSuppliersCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public event Action? RequestBackToNavigation;
    public event Action? RequestNavigateToCashier;
    public event Action? RequestNavigateToReports;
    public event Action? RequestNavigateToSuppliers;
    public event Action? PurchaseCompleted;

    #endregion

    public PurchaseViewModel()
    {
        _context = new AppDbContext();
        _productService = new ProductService(_context);

        SelectCartonModeCommand = new RelayCommand(() => IsCartonMode = true);
        SelectSingleModeCommand = new RelayCommand(() => IsCartonMode = false);

        SetCashPaymentCommand = new RelayCommand(() => PaymentType = "Cash");
        SetDebtPaymentCommand = new RelayCommand(() => PaymentType = "Debt");

        OpenBarcodePackageModalCommand = new RelayCommand(() =>
        {
            EditModalBarcode = !string.IsNullOrWhiteSpace(CurrentItemBarcode) 
                ? CurrentItemBarcode 
                : (SelectedProduct != null ? SelectedProduct.Barcode : string.Empty);
            EditModalItemsPerCarton = ItemsPerCarton > 0 ? ItemsPerCarton : 24;
            IsBarcodePackageModalOpen = true;
        });

        SaveBarcodePackageModalCommand = new RelayCommand(() =>
        {
            if (EditModalItemsPerCarton <= 0)
            {
                EditModalItemsPerCarton = 1;
            }

            ItemsPerCarton = EditModalItemsPerCarton;
            CurrentItemBarcode = EditModalBarcode.Trim();

            if (SelectedProduct != null)
            {
                if (!string.IsNullOrWhiteSpace(CurrentItemBarcode))
                {
                    SelectedProduct.Barcode = CurrentItemBarcode;
                }
                SelectedProduct.ItemsPerCarton = ItemsPerCarton;
            }

            RecalculateCurrentItemMetrics();
            IsBarcodePackageModalOpen = false;
        });

        CloseBarcodePackageModalCommand = new RelayCommand(() =>
        {
            IsBarcodePackageModalOpen = false;
        });

        NavigateToCashierCommand = new RelayCommand(() => RequestNavigateToCashier?.Invoke());
        NavigateToReportsCommand = new RelayCommand(() => RequestNavigateToReports?.Invoke());
        NavigateToSuppliersCommand = new RelayCommand(() => RequestNavigateToSuppliers?.Invoke());
        OpenSettingsCommand = new RelayCommand(() => RequestNavigateToSuppliers?.Invoke());

        AddItemToInvoiceCommand = new RelayCommand(() =>
        {
            if (SelectedProduct == null && string.IsNullOrWhiteSpace(SearchQuery))
            {
                MessageBox.Show("يرجى اختيار أو كتابة اسم المادة أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Quantity <= 0)
            {
                MessageBox.Show("يرجى إدخال كمية صحيحة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string prodName = SelectedProduct != null ? SelectedProduct.Name : SearchQuery.Trim();
            string barcode = !string.IsNullOrWhiteSpace(CurrentItemBarcode)
                ? CurrentItemBarcode
                : (SelectedProduct != null ? SelectedProduct.Barcode : $"GEN-{new Random().Next(100000, 999999)}");
            Guid prodId = SelectedProduct != null ? SelectedProduct.Id : Guid.NewGuid();

            var row = new PurchaseItemRow
            {
                ProductId = prodId,
                Barcode = barcode,
                ProductName = prodName,
                IsCarton = IsCartonMode,
                ItemsPerCarton = ItemsPerCarton > 0 ? ItemsPerCarton : 1,
                Quantity = Quantity,
                OldStockQuantity = CurrentWarehouseStock,
                OldCost = OldCost,
                NewCost = NewCost,
                PieceCost = CalculatedPieceCost,
                CartonCost = CalculatedCartonCost,
                SellingPrice = SellingPrice
            };

            InvoiceItems.Add(row);
            RecalculateTotals();

            // Clear entry for next product
            SelectedProduct = null;
            SearchQuery = string.Empty;
            CurrentItemBarcode = string.Empty;
            Quantity = 1;
        });

        RemoveItemCommand = new RelayCommand(param =>
        {
            if (param is PurchaseItemRow row)
            {
                InvoiceItems.Remove(row);
                RecalculateTotals();
            }
        });

        ResetNewInvoiceCommand = new RelayCommand(() =>
        {
            InvoiceNumber = string.Empty;
            InvoiceDate = DateTime.Today;
            InvoiceItems.Clear();
            SelectedSupplier = null;
            SelectedProduct = null;
            SearchQuery = string.Empty;
            Quantity = 1;
            NewCost = 12000;
            OldCost = 12000;
            SellingPrice = 750;
            RecalculateTotals();
        });

        OpenHistoryModalCommand = new RelayCommand(() => IsHistoryModalOpen = true);
        CloseHistoryModalCommand = new RelayCommand(() => IsHistoryModalOpen = false);

        CompletePurchaseInvoiceCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("يرجى اختيار المورد / المندوب للفاتورة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!InvoiceItems.Any())
            {
                MessageBox.Show("فاتورة الشراء فارغة! يرجى إضافة مواد أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string finalInvNum = !string.IsNullOrWhiteSpace(InvoiceNumber) ? InvoiceNumber.Trim() : $"INV-{new Random().Next(10000, 99999)}";

                var invoice = new PurchaseInvoice
                {
                    Id = Guid.NewGuid(),
                    InvoiceNumber = finalInvNum,
                    SupplierId = SelectedSupplier.Id,
                    SupplierName = SelectedSupplier.Name,
                    TotalAmount = TotalInvoiceAmount,
                    PaidAmount = PaidAmount,
                    PaymentMethod = PaymentType,
                    CreatedAt = DateTime.UtcNow
                };

                foreach (var row in InvoiceItems)
                {
                    var item = new PurchaseInvoiceItem
                    {
                        Id = Guid.NewGuid(),
                        PurchaseInvoiceId = invoice.Id,
                        ProductId = row.ProductId,
                        ProductName = row.ProductName + (row.IsCarton ? " (كرتون)" : ""),
                        Barcode = row.Barcode,
                        Quantity = row.Quantity,
                        UnitCost = row.NewCost,
                        SellingPrice = row.SellingPrice,
                        IsCarton = row.IsCarton
                    };
                    invoice.Items.Add(item);

                    // ===============================================================
                    // تطبيق معادلة المتوسط المرجح للبضاعة عند التوريد
                    // ===============================================================
                    var prod = await _context.Products.FindAsync(row.ProductId);
                    if (prod != null)
                    {
                        int perC = row.ItemsPerCarton > 0 ? row.ItemsPerCarton : 1;
                        decimal oldQty = Math.Max(0, prod.StockQuantity);
                        decimal oldUnitCost = prod.Cost;
                        
                        decimal newAddedPieces = row.IsCarton ? (row.Quantity * perC) : row.Quantity;
                        decimal newUnitCost = row.IsCarton ? (row.NewCost / perC) : row.NewCost;

                        // 1. قيمة الشراء القديم = الكمية القديمة × السعر القديم
                        decimal oldTotalValue = oldQty * oldUnitCost;
                        // 2. قيمة الشراء الجديد = الكمية الجديدة × السعر الجديد
                        decimal newTotalValue = newAddedPieces * newUnitCost;
                        // 3. التكلفة الإجمالية = قيمة الشراء القديم + قيمة الشراء الجديد
                        decimal combinedTotalCost = oldTotalValue + newTotalValue;
                        // 4. الكمية الإجمالية = الكمية القديمة + الكمية الجديدة
                        decimal combinedTotalQty = oldQty + newAddedPieces;
                        // 5. السعر المرجح الجديد = التكلفة الإجمالية ÷ الكمية الإجمالية
                        decimal weightedAvgUnitCost = combinedTotalQty > 0 ? Math.Round(combinedTotalCost / combinedTotalQty, 2) : newUnitCost;

                        prod.StockQuantity = combinedTotalQty;
                        prod.Cost = weightedAvgUnitCost;
                        prod.CartonPurchasePrice = Math.Round(weightedAvgUnitCost * perC, 2);
                        prod.ItemsPerCarton = perC;
                        if (prod.ItemsPerCarton > 0)
                        {
                            prod.CartonsCount = (int)(prod.StockQuantity / prod.ItemsPerCarton);
                        }

                        if (row.SellingPrice > 0)
                        {
                            prod.Price = row.SellingPrice;
                            prod.CartonSellingPrice = row.SellingPrice * perC;
                        }
                        prod.SupplierId = SelectedSupplier.Id;
                        prod.SupplierName = SelectedSupplier.Name;
                        prod.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Create product if new
                        int perC = row.ItemsPerCarton > 0 ? row.ItemsPerCarton : 1;
                        decimal initialPieces = row.IsCarton ? (row.Quantity * perC) : row.Quantity;
                        decimal pieceCost = row.IsCarton ? Math.Round(row.NewCost / perC, 2) : row.NewCost;

                        var newProd = new Product
                        {
                            Id = row.ProductId,
                            Name = row.ProductName,
                            Barcode = row.Barcode,
                            Cost = pieceCost,
                            Price = row.SellingPrice > 0 ? row.SellingPrice : (pieceCost * 1.25m),
                            StockQuantity = initialPieces,
                            CartonsCount = row.IsCarton ? (int)row.Quantity : (int)(initialPieces / perC),
                            ItemsPerCarton = perC,
                            CartonPurchasePrice = row.IsCarton ? row.NewCost : (pieceCost * perC),
                            CartonSellingPrice = row.SellingPrice * perC,
                            SupplierId = SelectedSupplier.Id,
                            SupplierName = SelectedSupplier.Name,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _context.Products.AddAsync(newProd);
                    }
                }

                await _context.PurchaseInvoices.AddAsync(invoice);

                // Add transaction for supplier ledger
                var trans = new SupplierTransaction
                {
                    Id = Guid.NewGuid(),
                    SupplierId = SelectedSupplier.Id,
                    TransactionType = "فاتورة شراء بضاعة",
                    Amount = TotalInvoiceAmount,
                    InvoiceNumber = InvoiceNumber,
                    Description = $"فاتورة شراء ({InvoiceItems.Count}) أصناف - الدفع: {PaymentType} - المسدد: {PaidAmount:N0} د.ع - المتبقي: {RemainingDebtAmount:N0} د.ع",
                    TransactionDate = DateTime.UtcNow
                };
                await _context.SupplierTransactions.AddAsync(trans);

                await _context.SaveChangesAsync();

                MessageBox.Show($"تم حفظ واعتماد فاتورة الشراء [{InvoiceNumber}] بنجاح، وتطبيق المتوسط المرجح على تكلفة المواد وتحديث المخزون!",
                    "تم الشراء وتحديث المخزن بالمتوسط المرجح", MessageBoxButton.OK, MessageBoxImage.Information);

                ResetNewInvoiceCommand.Execute(null);
                await LoadDataAsync();
                PurchaseCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ الفاتورة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        BackToMainCommand = new RelayCommand(() => RequestBackToNavigation?.Invoke());
    }

    private void OnProductSelected()
    {
        if (SelectedProduct != null)
        {
            ItemsPerCarton = (int)(SelectedProduct.ItemsPerCarton > 0 ? SelectedProduct.ItemsPerCarton : 24);
            CurrentWarehouseStock = SelectedProduct.StockQuantity;
            if (IsCartonMode)
            {
                OldCost = SelectedProduct.CartonPurchasePrice > 0 ? SelectedProduct.CartonPurchasePrice : (SelectedProduct.Cost * ItemsPerCarton);
                NewCost = OldCost;
                SellingPrice = SelectedProduct.Price;
            }
            else
            {
                OldCost = SelectedProduct.Cost;
                NewCost = OldCost;
                SellingPrice = SelectedProduct.Price;
            }
        }
        else
        {
            CurrentWarehouseStock = 0;
        }
        RecalculateCurrentItemMetrics();
    }

    /// <summary>
    /// حساب المتوسط المرجح اللحظي للبضاعة أثناء كتابة الكميات والأسعار
    /// </summary>
    private void RecalculateCurrentItemMetrics()
    {
        CalculatedItemTotal = Quantity * NewCost;

        int perC = ItemsPerCarton > 0 ? ItemsPerCarton : 1;
        decimal oldQty = Math.Max(0, CurrentWarehouseStock);
        decimal oldUnitCost = (SelectedProduct != null && SelectedProduct.Cost > 0) ? SelectedProduct.Cost : (IsCartonMode ? Math.Round(OldCost / perC, 2) : OldCost);

        decimal newAddedPieces = IsCartonMode ? (Quantity * perC) : Quantity;
        decimal newUnitCost = IsCartonMode ? (NewCost / perC) : NewCost;

        // تطبيق خطوات المتوسط المرجح:
        // • قيمة الشراء القديم = الكمية القديمة × السعر القديم
        decimal oldTotalValue = oldQty * oldUnitCost;
        // • قيمة الشراء الجديد = الكمية الجديدة × السعر الجديد
        decimal newTotalValue = newAddedPieces * newUnitCost;
        // • التكلفة الإجمالية = قيمة الشراء القديم + قيمة الشراء الجديد
        decimal combinedCost = oldTotalValue + newTotalValue;
        // • الكمية الإجمالية = الكمية القديمة + الكمية الجديدة
        decimal combinedQty = oldQty + newAddedPieces;
        // • السعر المرجح الجديد = التكلفة الإجمالية ÷ الكمية الإجمالية
        decimal weightedPieceCost = combinedQty > 0 ? Math.Round(combinedCost / combinedQty, 2) : newUnitCost;

        CalculatedPieceCost = weightedPieceCost;
        CalculatedCartonCost = Math.Round(weightedPieceCost * perC, 2);
    }

    private void RecalculateTotals()
    {
        TotalInvoiceAmount = InvoiceItems.Sum(i => i.TotalCost);
        TotalItemsCount = InvoiceItems.Count;
        if (PaymentType == "Cash")
        {
            PaidAmount = TotalInvoiceAmount;
        }
        else
        {
            PaidAmount = 0;
        }
        OnPropertyChanged(nameof(RemainingDebtAmount));
        OnPropertyChanged(nameof(HasItems));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private bool _isProductDropdownOpen;
    public bool IsProductDropdownOpen
    {
        get => _isProductDropdownOpen;
        set => SetProperty(ref _isProductDropdownOpen, value);
    }

    private async Task FilterProductsAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            FilteredProducts.Clear();
            IsProductDropdownOpen = false;
            return;
        }

        var list = await _productService.GetProductsAsync(null, SearchQuery);
        FilteredProducts.Clear();
        foreach (var p in list.Take(10)) FilteredProducts.Add(p);
        IsProductDropdownOpen = FilteredProducts.Any();
    }

    public async Task InitializeAsync()
    {
        await LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        var sups = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
        SuppliersList.Clear();
        foreach (var s in sups) SuppliersList.Add(s);

        var history = await _context.PurchaseInvoices
            .Include(p => p.Items)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        PurchaseHistoryList.Clear();
        foreach (var h in history) PurchaseHistoryList.Add(h);
        HistoryCount = PurchaseHistoryList.Count;
    }
}
