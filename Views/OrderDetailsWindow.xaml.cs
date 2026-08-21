using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.Views;

public partial class OrderDetailsWindow : Window
{
    private SupplierOrder _order;
    public event Action? OrderUpdated;

    public OrderDetailsWindow(SupplierOrder order)
    {
        InitializeComponent();
        _order = order;
        LoadOrderData();
    }

    private void LoadOrderData()
    {
        TxtMarketName.Text = _order.MarketName;
        TxtOrderNumber.Text = _order.OrderNumber;
        TxtPhone.Text = !string.IsNullOrWhiteSpace(_order.MarketPhone) ? _order.MarketPhone : "--";
        TxtRep.Text = !string.IsNullOrWhiteSpace(_order.RepresentativeName) ? _order.RepresentativeName : "مندوب المبيعات";
        TxtAddress.Text = !string.IsNullOrWhiteSpace(_order.MarketAddress) ? _order.MarketAddress : "--";
        TxtNotes.Text = !string.IsNullOrWhiteSpace(_order.Notes) ? _order.Notes : "لا توجد ملاحظات إضافية.";
        TxtGrandTotal.Text = $"{_order.TotalAmount:N0} د.ع";

        ItemsDataGrid.ItemsSource = _order.Items;

        UpdateStatusBadge();
    }

    private void UpdateStatusBadge()
    {
        switch (_order.Status)
        {
            case OrderStatus.Pending:
                BadgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#854D0E"));
                TxtStatus.Text = "⏳ قيد الانتظار";
                break;
            case OrderStatus.InPreparation:
                BadgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF"));
                TxtStatus.Text = "📦 جاري التجهيز";
                break;
            case OrderStatus.Delivered:
                BadgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#065F46"));
                TxtStatus.Text = "✔ تم التوصيل والتسليم";
                break;
            case OrderStatus.Cancelled:
                BadgeStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#991B1B"));
                TxtStatus.Text = "❌ ملغية";
                break;
        }
    }

    private async void DeliverOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var db = new AppDbContext();
            var dbOrder = await db.SupplierOrders.FirstOrDefaultAsync(o => o.Id == _order.Id);
            if (dbOrder != null)
            {
                dbOrder.Status = OrderStatus.Delivered;
                await db.SaveChangesAsync();
                _order.Status = OrderStatus.Delivered;
                UpdateStatusBadge();
                OrderUpdated?.Invoke();
            }

            // Also auto-export PDF backup
            string pdf = PdfExportService.ExportA4InvoiceToPdf(_order, openAfterSave: false);
            MessageBox.Show($"✔ تم اعتماد وتسليم الطلبية بنجاح!\nتم حفظ نسخة PDF تلقائياً في:\n{pdf}", "تم التسليم بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء اعتماد الطلبية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportPdf_Click(object sender, RoutedEventArgs e)
    {
        string path = PdfExportService.ExportA4InvoiceToPdf(_order, openAfterSave: true);
        if (!string.IsNullOrEmpty(path))
        {
            MessageBox.Show($"✔ تم تصدير فاتورة A4 وحفظ نسخة PDF بنجاح في جهازك:\n\n{path}", "تم حفظ PDF", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void PrintA4_Click(object sender, RoutedEventArgs e)
    {
        // 1. Auto-save PDF backup
        PdfExportService.ExportA4InvoiceToPdf(_order, openAfterSave: false);

        // 2. Open A4 Print Dialog
        A4InvoicePrintService.PrintA4Invoice(_order);
    }

    private async void ConvertToInvoice_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show($"هل أنت متأكد من تحويل طلبية ({_order.MarketName}) إلى فاتورة شراء وتوريد رسمية بالمخزن؟", "تحويل الطلبية", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var db = new AppDbContext();
            var dbOrder = await db.SupplierOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == _order.Id);
            if (dbOrder == null) return;

            var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Name == dbOrder.RepresentativeName || s.Name == "مورد عام");
            if (supplier == null)
            {
                supplier = new Supplier
                {
                    Name = string.IsNullOrWhiteSpace(dbOrder.RepresentativeName) ? "مورد عام" : dbOrder.RepresentativeName,
                    Phone = dbOrder.MarketPhone,
                    Notes = "تم إنشاؤه تلقائياً من الطلبيات السحابية"
                };
                db.Suppliers.Add(supplier);
                await db.SaveChangesAsync();
            }

            var purchase = new PurchaseInvoice
            {
                InvoiceNumber = $"PUR-ORD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}",
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                TotalAmount = dbOrder.TotalAmount,
                PaidAmount = dbOrder.TotalAmount,
                Notes = $"تحويل من طلبية الماركت: {dbOrder.MarketName} (رقم {dbOrder.OrderNumber})",
                Items = dbOrder.Items.Select(i => new PurchaseInvoiceItem
                {
                    ProductId = i.ProductId ?? Guid.Empty,
                    ProductName = i.ProductName,
                    Barcode = i.Barcode,
                    Quantity = i.Quantity,
                    UnitCost = i.UnitPrice,
                    SellingPrice = i.UnitPrice,
                    IsCarton = i.UnitType == "Carton"
                }).ToList()
            };

            db.PurchaseInvoices.Add(purchase);
            dbOrder.IsConvertedToInvoice = true;
            dbOrder.Status = OrderStatus.Delivered;
            await db.SaveChangesAsync();

            _order.Status = OrderStatus.Delivered;
            UpdateStatusBadge();
            OrderUpdated?.Invoke();

            MessageBox.Show($"✔ تم تحويل الطلبية بنجاح إلى فاتورة شراء وتوريد برقم:\n{purchase.InvoiceNumber}", "تم التحويل بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        PdfExportService.OpenInvoicesFolder();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
