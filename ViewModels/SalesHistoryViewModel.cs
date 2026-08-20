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

public class SalesHistoryViewModel : BaseViewModel
{
    private readonly AppDbContext _context;

    #region Search & Filter Properties

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                _ = FilterSalesAsync();
            }
        }
    }

    private DateTime? _customStartDate;
    public DateTime? CustomStartDate
    {
        get => _customStartDate;
        set
        {
            if (SetProperty(ref _customStartDate, value))
            {
                if (value.HasValue)
                {
                    _selectedDateFilter = "Custom";
                    OnPropertyChanged(nameof(SelectedDateFilter));
                }
                _ = FilterSalesAsync();
            }
        }
    }

    private DateTime? _customEndDate;
    public DateTime? CustomEndDate
    {
        get => _customEndDate;
        set
        {
            if (SetProperty(ref _customEndDate, value))
            {
                if (value.HasValue)
                {
                    _selectedDateFilter = "Custom";
                    OnPropertyChanged(nameof(SelectedDateFilter));
                }
                _ = FilterSalesAsync();
            }
        }
    }

    private string _selectedDateFilter = "Today"; // "Today", "Yesterday", "Week", "Month", "Custom", "All"
    public string SelectedDateFilter
    {
        get => _selectedDateFilter;
        set
        {
            if (SetProperty(ref _selectedDateFilter, value))
            {
                if (value != "Custom")
                {
                    _customStartDate = null;
                    _customEndDate = null;
                    OnPropertyChanged(nameof(CustomStartDate));
                    OnPropertyChanged(nameof(CustomEndDate));
                }
                _ = FilterSalesAsync();
            }
        }
    }

    private string _selectedPaymentMethod = "All"; // "All", "Cash", "Card"
    public string SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                _ = FilterSalesAsync();
            }
        }
    }

    public ObservableCollection<string> CashierNames { get; } = new();

    private string _selectedCashier = "الكل";
    public string SelectedCashier
    {
        get => _selectedCashier;
        set
        {
            if (SetProperty(ref _selectedCashier, value))
            {
                _ = FilterSalesAsync();
            }
        }
    }

    #endregion

    #region KPI Stats

    private decimal _totalSalesAmount;
    public decimal TotalSalesAmount
    {
        get => _totalSalesAmount;
        set => SetProperty(ref _totalSalesAmount, value);
    }

    private int _totalInvoicesCount;
    public int TotalInvoicesCount
    {
        get => _totalInvoicesCount;
        set => SetProperty(ref _totalInvoicesCount, value);
    }

    private decimal _totalProfitAmount;
    public decimal TotalProfitAmount
    {
        get => _totalProfitAmount;
        set => SetProperty(ref _totalProfitAmount, value);
    }

    private decimal _averageInvoiceAmount;
    public decimal AverageInvoiceAmount
    {
        get => _averageInvoiceAmount;
        set => SetProperty(ref _averageInvoiceAmount, value);
    }

    #endregion

    #region Collections & Selected Invoice

    public ObservableCollection<Sale> FilteredSales { get; } = new();

    private Sale? _selectedSale;
    public Sale? SelectedSale
    {
        get => _selectedSale;
        set => SetProperty(ref _selectedSale, value);
    }

    private bool _isInvoiceModalOpen;
    public bool IsInvoiceModalOpen
    {
        get => _isInvoiceModalOpen;
        set => SetProperty(ref _isInvoiceModalOpen, value);
    }

    #endregion

    #region Commands

    public ICommand FilterTodayCommand { get; }
    public ICommand FilterYesterdayCommand { get; }
    public ICommand FilterWeekCommand { get; }
    public ICommand FilterMonthCommand { get; }
    public ICommand FilterAllCommand { get; }
    public ICommand ViewInvoiceCommand { get; }
    public ICommand CloseInvoiceModalCommand { get; }
    public ICommand PrintInvoiceCommand { get; }
    public ICommand PrintCurrentModalInvoiceCommand { get; }
    public ICommand ReturnSaleInvoiceCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ClearSearchCommand { get; }

    #endregion

    public SalesHistoryViewModel()
    {
        _context = new AppDbContext();

        ClearSearchCommand = new RelayCommand(() => SearchQuery = string.Empty);
        FilterTodayCommand = new RelayCommand(() => SelectedDateFilter = "Today");
        FilterYesterdayCommand = new RelayCommand(() => SelectedDateFilter = "Yesterday");
        FilterWeekCommand = new RelayCommand(() => SelectedDateFilter = "Week");
        FilterMonthCommand = new RelayCommand(() => SelectedDateFilter = "Month");
        FilterAllCommand = new RelayCommand(() => SelectedDateFilter = "All");

        ViewInvoiceCommand = new RelayCommand(param =>
        {
            if (param is Sale sale)
            {
                SelectedSale = sale;
                IsInvoiceModalOpen = true;
            }
        });

        CloseInvoiceModalCommand = new RelayCommand(() => IsInvoiceModalOpen = false);

        PrintInvoiceCommand = new RelayCommand(param =>
        {
            if (param is Sale sale)
            {
                PrintReceipt(sale);
            }
        });

        PrintCurrentModalInvoiceCommand = new RelayCommand(() =>
        {
            if (SelectedSale != null)
            {
                PrintReceipt(SelectedSale);
            }
        });

        ReturnSaleInvoiceCommand = new AsyncRelayCommand(async (param) =>
        {
            var targetSale = param as Sale ?? SelectedSale;
            if (targetSale != null)
            {
                if (targetSale.Status == "Returned")
                {
                    MessageBox.Show("هذا الوصل تم استرجاعه مسبقاً بالفعل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var res = MessageBox.Show($"هل ترغب في استرجاع الوصل رقم '{targetSale.InvoiceNumber}' بقيمة {targetSale.TotalAmount:N0} د.ع وإعادة كميات المواد للمخزن؟",
                                          "تأكيد استرجاع الوصل", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    var saleService = new SaleService(_context);
                    bool returned = await saleService.ReturnSaleAsync(targetSale.Id);
                    if (returned)
                    {
                        await LoadSalesDataAsync();
                        IsInvoiceModalOpen = false;
                        MessageBox.Show($"تم استرجاع الوصل '{targetSale.InvoiceNumber}' بنجاح وإعادة كافة المواد إلى المخزن.", "تم الاسترجاع بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        });

        RefreshCommand = new AsyncRelayCommand(async () => await LoadSalesDataAsync());
    }

    public async Task InitializeAsync()
    {
        await LoadSalesDataAsync();
    }

    public async Task LoadSalesDataAsync()
    {
        var users = await _context.Users.Select(u => u.FullName).Distinct().ToListAsync();
        CashierNames.Clear();
        CashierNames.Add("الكل");
        foreach (var u in users)
        {
            if (!string.IsNullOrWhiteSpace(u)) CashierNames.Add(u);
        }

        await FilterSalesAsync();
    }

    public async Task FilterSalesAsync()
    {
        IQueryable<Sale> query = _context.Sales
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product);

        // 1. Date Filter using Local Time converted to UTC
        DateTime localNow = DateTime.Now;
        DateTime todayLocalStart = localNow.Date.ToUniversalTime();
        DateTime todayLocalEnd = localNow.Date.AddDays(1).ToUniversalTime();

        switch (SelectedDateFilter)
        {
            case "Today":
                query = query.Where(s => (s.CreatedAt >= todayLocalStart && s.CreatedAt < todayLocalEnd) || (s.UpdatedAt != null && s.UpdatedAt >= todayLocalStart && s.UpdatedAt < todayLocalEnd));
                break;
            case "Yesterday":
                DateTime yestStart = localNow.Date.AddDays(-1).ToUniversalTime();
                query = query.Where(s => (s.CreatedAt >= yestStart && s.CreatedAt < todayLocalStart) || (s.UpdatedAt != null && s.UpdatedAt >= yestStart && s.UpdatedAt < todayLocalStart));
                break;
            case "Week":
                DateTime weekStart = localNow.Date.AddDays(-7).ToUniversalTime();
                query = query.Where(s => s.CreatedAt >= weekStart || (s.UpdatedAt != null && s.UpdatedAt >= weekStart));
                break;
            case "Month":
                DateTime monthStart = new DateTime(localNow.Year, localNow.Month, 1).ToUniversalTime();
                query = query.Where(s => s.CreatedAt >= monthStart || (s.UpdatedAt != null && s.UpdatedAt >= monthStart));
                break;
            case "Custom":
                if (CustomStartDate.HasValue || CustomEndDate.HasValue)
                {
                    DateTime cStart = (CustomStartDate ?? CustomEndDate ?? DateTime.Today).Date.ToUniversalTime();
                    DateTime cEnd = (CustomEndDate ?? CustomStartDate ?? DateTime.Today).Date.AddDays(1).ToUniversalTime();
                    query = query.Where(s => (s.CreatedAt >= cStart && s.CreatedAt < cEnd) || (s.UpdatedAt != null && s.UpdatedAt >= cStart && s.UpdatedAt < cEnd));
                }
                break;
            case "All":
            default:
                break;
        }

        // 2. Payment Method Filter
        if (SelectedPaymentMethod != "All")
        {
            query = query.Where(s => s.PaymentMethod == SelectedPaymentMethod);
        }

        // 3. Cashier Filter
        if (!string.IsNullOrWhiteSpace(SelectedCashier) && SelectedCashier != "الكل")
        {
            query = query.Where(s => s.User != null && s.User.FullName == SelectedCashier);
        }

        // 4. Text Search (Supports Barcode, Product Name, Invoice Number, Customer, Cashier)
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string clean = SearchQuery.Trim().ToLower();
            query = query.Where(s => s.InvoiceNumber.ToLower().Contains(clean) ||
                                     (s.CustomerName != null && s.CustomerName.ToLower().Contains(clean)) ||
                                     (s.User != null && s.User.FullName.ToLower().Contains(clean)) ||
                                     s.Items.Any(i => i.ProductName.ToLower().Contains(clean) || 
                                                      i.Barcode.ToLower().Contains(clean) ||
                                                      (i.Product != null && (i.Product.Name.ToLower().Contains(clean) || i.Product.Barcode.ToLower().Contains(clean)))));
        }

        var salesList = await query.OrderByDescending(s => s.CreatedAt).ToListAsync();

        FilteredSales.Clear();
        decimal totalSum = 0;
        decimal totalProfit = 0;
        int completedCount = 0;

        foreach (var s in salesList)
        {
            FilteredSales.Add(s);
            if (s.Status != "Returned")
            {
                totalSum += s.TotalAmount;
                totalProfit += s.InvoiceNetProfit;
                completedCount++;
            }
        }

        TotalSalesAmount = totalSum;
        TotalInvoicesCount = salesList.Count;
        TotalProfitAmount = totalProfit;
        AverageInvoiceAmount = completedCount > 0 ? (totalSum / completedCount) : 0;
    }

    private void PrintReceipt(Sale sale)
    {
        try
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                FlowDocument doc = CreateReceiptFlowDocument(sale, printDialog.PrintableAreaWidth);
                IDocumentPaginatorSource idpSource = doc;
                printDialog.PrintDocument(idpSource.DocumentPaginator, $"وصل مبيعات {sale.InvoiceNumber}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء طباعة الوصل: {ex.Message}", "خطأ في الطباعة", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private FlowDocument CreateReceiptFlowDocument(Sale sale, double printableWidth)
    {
        bool isReturned = sale.Status == "Returned";

        FlowDocument doc = new FlowDocument
        {
            PageWidth = printableWidth > 0 ? Math.Min(printableWidth, 320) : 300,
            PagePadding = new Thickness(8),
            FontFamily = new FontFamily("Times New Roman, Arial"),
            FlowDirection = FlowDirection.RightToLeft
        };

        // Header
        Paragraph pHeader = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };
        pHeader.Inlines.Add(new Bold(new Run("⚡ 7amo.pos\n")) { FontSize = 16 });

        if (isReturned)
        {
            pHeader.Inlines.Add(new Bold(new Run("🛑 وصل إرجاع مواد مسترجعة 🛑\n")) { FontSize = 12, Foreground = Brushes.DarkRed });
            pHeader.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Red });
            pHeader.Inlines.Add(new Run($"رقم وصل الإرجاع: {sale.InvoiceNumber}\n") { FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed });
            pHeader.Inlines.Add(new Run($"تاريخ البيع الأصلي: {sale.CreatedAt.ToLocalTime():yyyy-MM-dd hh:mm tt}\n") { FontSize = 9.5 });
            pHeader.Inlines.Add(new Run($"تاريخ ووقت الإرجاع: {(sale.UpdatedAt ?? sale.CreatedAt).ToLocalTime():yyyy-MM-dd hh:mm tt}\n") { FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed });
            pHeader.Inlines.Add(new Run($"الحالة: 🔴 مسترجع (Returned)\n") { FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkRed });
            pHeader.Inlines.Add(new Run($"الكاشير: {sale.User?.FullName ?? "كاشير عام"} | رد المبلغ: نقداً\n") { FontSize = 9.5 });
        }
        else
        {
            pHeader.Inlines.Add(new Run("نظام نقاط البيع والمخازن المتكامل\n") { FontSize = 10, Foreground = Brushes.DimGray });
            pHeader.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });
            pHeader.Inlines.Add(new Run($"رقم الوصل: {sale.InvoiceNumber}\n") { FontSize = 11, FontWeight = FontWeights.Bold });
            pHeader.Inlines.Add(new Run($"التاريخ: {sale.CreatedAt.ToLocalTime():yyyy-MM-dd hh:mm tt}\n") { FontSize = 10 });
            pHeader.Inlines.Add(new Run($"الكاشير: {sale.User?.FullName ?? "كاشير عام"} | الدفع: {(sale.PaymentMethod == "Cash" ? "نقداً" : "بطاقة")}\n") { FontSize = 10 });
        }

        pHeader.Inlines.Add(new Run("-------------------------------------------") { Foreground = isReturned ? Brushes.Red : Brushes.Gray });
        doc.Blocks.Add(pHeader);

        // Items Table
        Table table = new Table { CellSpacing = 2, Margin = new Thickness(0, 0, 0, 6) };
        table.Columns.Add(new TableColumn { Width = new GridLength(2, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1.5, GridUnitType.Star) });

        TableRowGroup rowGroup = new TableRowGroup();
        TableRow headerRow = new TableRow { FontWeight = FontWeights.Bold };
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run(isReturned ? "المادة المسترجعة" : "المادة"))));
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run(isReturned ? "الكمية" : "العدد"))));
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run("السعر"))));
        headerRow.Cells.Add(new TableCell(new Paragraph(new Run(isReturned ? "المسترد" : "الإجمالي"))));
        rowGroup.Rows.Add(headerRow);

        foreach (var item in sale.Items)
        {
            decimal displayQty = Math.Abs(item.Quantity);
            decimal displayTotal = Math.Abs(item.TotalPrice);

            TableRow row = new TableRow { FontSize = 9.5 };
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.ProductName))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(displayQty.ToString("N0")))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(item.UnitPrice.ToString("N0")))));
            row.Cells.Add(new TableCell(new Paragraph(new Run(isReturned ? $"-{displayTotal:N0}" : displayTotal.ToString("N0")))));
            rowGroup.Rows.Add(row);
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        // Totals & Footer
        Paragraph pTotals = new Paragraph
        {
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 0, 6)
        };
        pTotals.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = isReturned ? Brushes.Red : Brushes.Gray });
        
        if (isReturned)
        {
            pTotals.Inlines.Add(new Bold(new Run($"إجمالي المبلغ المسترد للزبون: - {Math.Abs(sale.TotalAmount):N0} د.ع\n")) { FontSize = 13, Foreground = Brushes.DarkRed });
            pTotals.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Red });
            pTotals.Inlines.Add(new Run("✔ تم استرجاع المواد وإعادة المبلغ إلى العميل بنجاح\n") { FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkSlateGray });
        }
        else
        {
            pTotals.Inlines.Add(new Run($"المجموع الفرعي: {sale.SubTotal:N0} د.ع\n") { FontSize = 10 });
            if (sale.DiscountAmount > 0)
            {
                pTotals.Inlines.Add(new Run($"الخصم الممنوح: {sale.DiscountAmount:N0} د.ع\n") { FontSize = 10, Foreground = Brushes.DarkRed });
            }
            pTotals.Inlines.Add(new Bold(new Run($"المبلغ الإجمالي المطلوب: {sale.TotalAmount:N0} د.ع\n")) { FontSize = 14 });
            pTotals.Inlines.Add(new Run("-------------------------------------------\n") { Foreground = Brushes.Gray });
            pTotals.Inlines.Add(new Run("شكراً لزيارتكم ونتشرف بخدمتكم دائماً\n") { FontSize = 10 });
        }

        doc.Blocks.Add(pTotals);

        return doc;
    }
}
