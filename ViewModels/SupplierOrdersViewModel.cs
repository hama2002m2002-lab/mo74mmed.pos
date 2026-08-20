using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.ViewModels;

public class SupplierOrdersViewModel : BaseViewModel
{
    private readonly AppDbContext _context;

    public ObservableCollection<SupplierOrder> AllOrders { get; } = new();
    public ObservableCollection<SupplierOrder> FilteredOrders { get; } = new();

    private SupplierOrder? _selectedOrder;
    public SupplierOrder? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetProperty(ref _selectedOrder, value))
            {
                OnPropertyChanged(nameof(HasSelectedOrder));
                OnPropertyChanged(nameof(SelectedOrderItems));
            }
        }
    }

    public bool HasSelectedOrder => SelectedOrder != null;
    public ObservableCollection<SupplierOrderItem> SelectedOrderItems => 
        SelectedOrder != null ? new ObservableCollection<SupplierOrderItem>(SelectedOrder.Items) : new ObservableCollection<SupplierOrderItem>();

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    private string _statusFilter = "All";
    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value))
            {
                ApplyFilter();
                OnPropertyChanged(nameof(IsFilterAll));
                OnPropertyChanged(nameof(IsFilterPending));
                OnPropertyChanged(nameof(IsFilterInPrep));
                OnPropertyChanged(nameof(IsFilterDelivered));
            }
        }
    }

    public bool IsFilterAll => StatusFilter == "All";
    public bool IsFilterPending => StatusFilter == "Pending";
    public bool IsFilterInPrep => StatusFilter == "InPreparation";
    public bool IsFilterDelivered => StatusFilter == "Delivered";

    // KPI Counters
    private int _totalOrdersCount;
    public int TotalOrdersCount { get => _totalOrdersCount; set => SetProperty(ref _totalOrdersCount, value); }

    private int _pendingOrdersCount;
    public int PendingOrdersCount { get => _pendingOrdersCount; set => SetProperty(ref _pendingOrdersCount, value); }

    private int _inPrepOrdersCount;
    public int InPrepOrdersCount { get => _inPrepOrdersCount; set => SetProperty(ref _inPrepOrdersCount, value); }

    private int _deliveredOrdersCount;
    public int DeliveredOrdersCount { get => _deliveredOrdersCount; set => SetProperty(ref _deliveredOrdersCount, value); }

    private decimal _totalOrdersValue;
    public decimal TotalOrdersValue { get => _totalOrdersValue; set => SetProperty(ref _totalOrdersValue, value); }

    // Commands
    public ICommand LoadOrdersCommand { get; }
    public ICommand AddNewOrderCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand SetPendingStatusCommand { get; }
    public ICommand SetInPrepStatusCommand { get; }
    public ICommand SetDeliveredStatusCommand { get; }
    public ICommand SetCancelledStatusCommand { get; }
    public ICommand ConvertToInvoiceCommand { get; }
    public ICommand DeleteOrderCommand { get; }
    public ICommand PrintOrderCommand { get; }
    public ICommand OpenMobilePortalCommand { get; }
    public ICommand CopyMobilePortalUrlCommand { get; }
    public ICommand OpenCloudPortalCommand { get; }
    public ICommand CopyCloudPortalUrlCommand { get; }
    public ICommand SyncCloudNowCommand { get; }

    public string PortalUrl => HamoPos.Services.RepWebPortalService.Instance.PortalUrl;
    public string CloudPortalUrl => HamoPos.Services.CloudSyncService.Instance.PublicCloudPortalUrl;
    public string CloudSyncStatus => HamoPos.Services.CloudSyncService.Instance.SyncStatusMessage;

    public event Action? OrderConvertedToPurchase;

    public SupplierOrdersViewModel()
    {
        _context = new AppDbContext();

        LoadOrdersCommand = new AsyncRelayCommand(LoadOrdersAsync);
        AddNewOrderCommand = new RelayCommand(ExecuteAddNewOrder);
        SetFilterCommand = new RelayCommand((param) =>
        {
            if (param is string status)
            {
                StatusFilter = status;
            }
        });

        SyncCloudNowCommand = new AsyncRelayCommand(async () =>
        {
            await HamoPos.Services.CloudSyncService.Instance.SyncAllAsync();
            OnPropertyChanged(nameof(CloudSyncStatus));
            await LoadOrdersAsync();
            MessageBox.Show("تمت المزامنة السحابية بنجاح 24/7!\nتم تحديث الكتالوج وفحص كافة الطلبيات الواردة من الموبايل.", "السحابة متصلة", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        HamoPos.Services.CloudSyncService.Instance.CloudOrdersImported += () =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                _ = LoadOrdersAsync();
                OnPropertyChanged(nameof(CloudSyncStatus));
            });
        };

        OpenMobilePortalCommand = new RelayCommand(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = PortalUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        });

        CopyMobilePortalUrlCommand = new RelayCommand(() =>
        {
            try
            {
                Clipboard.SetText(PortalUrl);
                MessageBox.Show($"تم نسخ رابط شبكة الواي فاي المحلية للموبايل:\n{PortalUrl}", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        });

        OpenCloudPortalCommand = new RelayCommand(() =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = CloudPortalUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        });

        CopyCloudPortalUrlCommand = new RelayCommand(() =>
        {
            try
            {
                Clipboard.SetText(CloudPortalUrl);
                MessageBox.Show($"تم نسخ رابط الموبايل السحابي العالمي 24/7 بنجاح:\n\n{CloudPortalUrl}\n\n(يمكنك إرساله عبر واتساب لأي مندوب أو فتحه من أي موبايل حول العالم عبر 4G/5G حتى واللابتوب مطفأ!)", "تم نسخ الرابط السحابي", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        });

        SetPendingStatusCommand = new AsyncRelayCommand(async () => await UpdateSelectedOrderStatusAsync(OrderStatus.Pending));
        SetInPrepStatusCommand = new AsyncRelayCommand(async () => await UpdateSelectedOrderStatusAsync(OrderStatus.InPreparation));
        SetDeliveredStatusCommand = new AsyncRelayCommand(async () => await UpdateSelectedOrderStatusAsync(OrderStatus.Delivered));
        SetCancelledStatusCommand = new AsyncRelayCommand(async () => await UpdateSelectedOrderStatusAsync(OrderStatus.Cancelled));
        
        ConvertToInvoiceCommand = new AsyncRelayCommand(ConvertToPurchaseInvoiceAsync);
        DeleteOrderCommand = new AsyncRelayCommand(DeleteSelectedOrderAsync);
        PrintOrderCommand = new RelayCommand(ExecutePrintOrder);
    }

    public async Task InitializeAsync()
    {
        await LoadOrdersAsync();
    }

    public async Task LoadOrdersAsync()
    {
        try
        {
            var orders = await _context.SupplierOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            AllOrders.Clear();
            foreach (var ord in orders)
            {
                AllOrders.Add(ord);
            }

            // Calculate KPIs
            TotalOrdersCount = AllOrders.Count;
            PendingOrdersCount = AllOrders.Count(o => o.Status == OrderStatus.Pending);
            InPrepOrdersCount = AllOrders.Count(o => o.Status == OrderStatus.InPreparation);
            DeliveredOrdersCount = AllOrders.Count(o => o.Status == OrderStatus.Delivered);
            TotalOrdersValue = AllOrders.Sum(o => o.TotalAmount);

            ApplyFilter();

            if (SelectedOrder != null)
            {
                SelectedOrder = AllOrders.FirstOrDefault(o => o.Id == SelectedOrder.Id);
            }
            else if (FilteredOrders.Any())
            {
                SelectedOrder = FilteredOrders.First();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading orders: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        var query = AllOrders.AsEnumerable();

        if (StatusFilter != "All")
        {
            if (Enum.TryParse<OrderStatus>(StatusFilter, out var targetStatus))
            {
                query = query.Where(o => o.Status == targetStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string s = SearchQuery.Trim().ToLower();
            query = query.Where(o =>
                (!string.IsNullOrEmpty(o.MarketName) && o.MarketName.ToLower().Contains(s)) ||
                (!string.IsNullOrEmpty(o.OrderNumber) && o.OrderNumber.ToLower().Contains(s)) ||
                (!string.IsNullOrEmpty(o.RepresentativeName) && o.RepresentativeName.ToLower().Contains(s)) ||
                (!string.IsNullOrEmpty(o.SupplierName) && o.SupplierName.ToLower().Contains(s)) ||
                (!string.IsNullOrEmpty(o.MarketPhone) && o.MarketPhone.Contains(s))
            );
        }

        FilteredOrders.Clear();
        foreach (var item in query)
        {
            FilteredOrders.Add(item);
        }
    }

    private async Task UpdateSelectedOrderStatusAsync(OrderStatus newStatus)
    {
        if (SelectedOrder == null) return;

        try
        {
            var dbOrder = await _context.SupplierOrders.FindAsync(SelectedOrder.Id);
            if (dbOrder != null)
            {
                dbOrder.Status = newStatus;
                await _context.SaveChangesAsync();
                SelectedOrder.Status = newStatus;
                OnPropertyChanged(nameof(SelectedOrder));
                await LoadOrdersAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذر تحديث حالة الطلبية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteAddNewOrder()
    {
        var dialog = new Views.AddSupplierOrderDialog();
        if (dialog.ShowDialog() == true)
        {
            _ = LoadOrdersAsync();
        }
    }

    private async Task ConvertToPurchaseInvoiceAsync()
    {
        if (SelectedOrder == null) return;

        if (SelectedOrder.IsConvertedToInvoice)
        {
            MessageBox.Show("تم تحويل هذه الطلبية إلى فاتورة شراء وتوريد مسبقاً!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"هل تريد تأكيد استلام بضاعة الطلبية ({SelectedOrder.OrderNumber}) وإدراجها كفاتورة شراء وتوريد في المخزن؟\n\n- سيتم زيادة كميات المخزون فوراً.\n- سيتم تسجيل الفاتورة بحساب المندوب ({SelectedOrder.SupplierName}).",
            "تحويل الطلبية إلى فاتورة شراء",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            // 1. Create Purchase Invoice
            var purchaseInvoice = new PurchaseInvoice
            {
                InvoiceNumber = $"PUR-ORD-{DateTime.Now:yyyyMMddHHmm}",
                SupplierId = SelectedOrder.SupplierId ?? Guid.Empty,
                SupplierName = SelectedOrder.SupplierName,
                TotalAmount = SelectedOrder.TotalAmount,
                PaidAmount = SelectedOrder.TotalAmount, // Default fully paid or as needed
                PaymentMethod = "Cash",
                Notes = $"تم التحويل تلقائياً من طلبية الماركت: {SelectedOrder.MarketName} (رقم: {SelectedOrder.OrderNumber})"
            };

            _context.PurchaseInvoices.Add(purchaseInvoice);

            // 2. Add Invoice Items and increase Stock in Product
            foreach (var orderItem in SelectedOrder.Items)
            {
                var invItem = new PurchaseInvoiceItem
                {
                    PurchaseInvoiceId = purchaseInvoice.Id,
                    ProductId = orderItem.ProductId ?? Guid.Empty,
                    ProductName = orderItem.ProductName,
                    Barcode = orderItem.Barcode,
                    Quantity = orderItem.Quantity,
                    UnitCost = orderItem.UnitPrice,
                    SellingPrice = orderItem.UnitPrice * 1.25m,
                    IsCarton = orderItem.UnitType == "Carton"
                };
                _context.PurchaseInvoiceItems.Add(invItem);

                // Update product stock if exists
                if (orderItem.ProductId.HasValue)
                {
                    var product = await _context.Products.FindAsync(orderItem.ProductId.Value);
                    if (product != null)
                    {
                        if (orderItem.UnitType == "Carton" && product.ItemsPerCarton > 0)
                        {
                            product.StockQuantity += orderItem.Quantity * product.ItemsPerCarton;
                        }
                        else
                        {
                            product.StockQuantity += orderItem.Quantity;
                        }
                    }
                }
            }

            // 3. Mark order as delivered and converted
            var dbOrder = await _context.SupplierOrders.FindAsync(SelectedOrder.Id);
            if (dbOrder != null)
            {
                dbOrder.IsConvertedToInvoice = true;
                dbOrder.Status = OrderStatus.Delivered;
            }

            await _context.SaveChangesAsync();

            MessageBox.Show(
                $"تم بنجاح تحويل الطلبية إلى فاتورة شراء وتوريد رقم ({purchaseInvoice.InvoiceNumber}) وتحديث كميات المخزن!",
                "تمت العملية بنجاح ✔",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            OrderConvertedToPurchase?.Invoke();
            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"فشل تحويل الطلبية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteSelectedOrderAsync()
    {
        if (SelectedOrder == null) return;

        var confirm = MessageBox.Show(
            $"هل أنت متأكد من حذف الطلبية رقم ({SelectedOrder.OrderNumber}) لماركت ({SelectedOrder.MarketName})؟",
            "تأكيد الحذف",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            var dbOrder = await _context.SupplierOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == SelectedOrder.Id);
            if (dbOrder != null)
            {
                _context.SupplierOrders.Remove(dbOrder);
                await _context.SaveChangesAsync();
                SelectedOrder = null;
                await LoadOrdersAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذر حذف الطلبية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecutePrintOrder()
    {
        if (SelectedOrder == null) return;

        try
        {
            var doc = new System.Windows.Documents.FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Tahoma"),
                PageWidth = 300,
                PagePadding = new Thickness(10),
                FlowDirection = FlowDirection.RightToLeft
            };

            var titleBlock = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("📦 كشف تجهيز طلبية بضاعة"))
            {
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            doc.Blocks.Add(titleBlock);

            var infoPara = new System.Windows.Documents.Paragraph();
            infoPara.Inlines.Add(new System.Windows.Documents.Run($"رقم الطلبية: {SelectedOrder.OrderNumber}\n"));
            infoPara.Inlines.Add(new System.Windows.Documents.Run($"الماركت: {SelectedOrder.MarketName}\n"));
            if (!string.IsNullOrEmpty(SelectedOrder.MarketPhone))
                infoPara.Inlines.Add(new System.Windows.Documents.Run($"هاتف الماركت: {SelectedOrder.MarketPhone}\n"));
            infoPara.Inlines.Add(new System.Windows.Documents.Run($"المندوب: {SelectedOrder.RepresentativeName} ({SelectedOrder.SupplierName})\n"));
            infoPara.Inlines.Add(new System.Windows.Documents.Run($"التاريخ: {SelectedOrder.OrderDate:yyyy/MM/dd - hh:mm tt}\n"));
            infoPara.Inlines.Add(new System.Windows.Documents.Run("--------------------------------"));
            doc.Blocks.Add(infoPara);

            // Table of items
            foreach (var item in SelectedOrder.Items)
            {
                var itemPara = new System.Windows.Documents.Paragraph
                {
                    Margin = new Thickness(0, 2, 0, 2)
                };
                string unitStr = item.UnitType == "Carton" ? "كرتون" : (item.UnitType == "Wholesale" ? "جملة" : "مفرد");
                itemPara.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run($"• {item.ProductName}\n")));
                itemPara.Inlines.Add(new System.Windows.Documents.Run($"  الكمية: {item.Quantity} ({unitStr}) × {item.UnitPrice:N0} = {item.TotalPrice:N0} د.ع"));
                doc.Blocks.Add(itemPara);
            }

            var totalPara = new System.Windows.Documents.Paragraph
            {
                Margin = new Thickness(0, 8, 0, 0),
                TextAlignment = TextAlignment.Center
            };
            totalPara.Inlines.Add(new System.Windows.Documents.Run("--------------------------------\n"));
            totalPara.Inlines.Add(new System.Windows.Documents.Bold(new System.Windows.Documents.Run($"إجمالي الطلبية: {SelectedOrder.TotalAmount:N0} د.ع\n")));
            totalPara.Inlines.Add(new System.Windows.Documents.Run($"الحالة: {SelectedOrder.Status}"));
            doc.Blocks.Add(totalPara);

            var printDlg = new System.Windows.Controls.PrintDialog();
            var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
            printDlg.PrintDocument(paginator, $"Order_{SelectedOrder.OrderNumber}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذر الطباعة: {ex.Message}", "تنبيه الطباعة", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
