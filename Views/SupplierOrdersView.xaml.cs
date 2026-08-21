using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class SupplierOrdersView : UserControl
{
    public SupplierOrdersView()
    {
        InitializeComponent();
    }

    private void OpenRepsWindow_Click(object sender, RoutedEventArgs e)
    {
        var win = new RepsManagementWindow();
        win.Owner = Window.GetWindow(this);
        win.ShowDialog();
    }

    private void OpenPdfFolder_Click(object sender, RoutedEventArgs e)
    {
        PdfExportService.OpenInvoicesFolder();
    }

    private void ViewOrderDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierOrder order)
        {
            OpenDetailsWindow(order);
        }
    }

    private void MainOrdersDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (MainOrdersDataGrid.SelectedItem is SupplierOrder order)
        {
            OpenDetailsWindow(order);
        }
    }

    private void OpenDetailsWindow(SupplierOrder order)
    {
        // Ensure items are loaded
        if (order.Items == null || !order.Items.Any())
        {
            using var db = new AppDbContext();
            var fullOrder = db.SupplierOrders.Include(o => o.Items).FirstOrDefault(o => o.Id == order.Id);
            if (fullOrder != null)
            {
                order = fullOrder;
            }
        }

        var win = new OrderDetailsWindow(order);
        win.Owner = Window.GetWindow(this);
        win.OrderUpdated += () =>
        {
            if (DataContext is SupplierOrdersViewModel vm)
            {
                _ = vm.LoadOrdersAsync();
            }
        };
        win.ShowDialog();
    }

    private async void QuickDeliver_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierOrder order)
        {
            try
            {
                using var db = new AppDbContext();
                var dbOrder = await db.SupplierOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == order.Id);
                if (dbOrder != null)
                {
                    dbOrder.Status = OrderStatus.Delivered;
                    await db.SaveChangesAsync();
                    order.Status = OrderStatus.Delivered;

                    // Auto-export PDF copy
                    string pdf = PdfExportService.ExportA4InvoiceToPdf(dbOrder, openAfterSave: false);

                    if (DataContext is SupplierOrdersViewModel vm)
                    {
                        await vm.LoadOrdersAsync();
                    }

                    MessageBox.Show($"✔ تم تسليم واعتماد طلبية ({order.MarketName}) بنجاح!\nتم حفظ نسخة PDF تلقائياً في المستندات.", "تم التسليم", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ أثناء التسليم: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void QuickPdf_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierOrder order)
        {
            // Ensure full items loaded
            if (order.Items == null || !order.Items.Any())
            {
                using var db = new AppDbContext();
                var fullOrder = db.SupplierOrders.Include(o => o.Items).FirstOrDefault(o => o.Id == order.Id);
                if (fullOrder != null) order = fullOrder;
            }

            string path = PdfExportService.ExportA4InvoiceToPdf(order, openAfterSave: true);
            if (!string.IsNullOrEmpty(path))
            {
                MessageBox.Show($"✔ تم حفظ وتصدير فاتورة A4 بصيغة PDF بنجاح:\n\n{path}", "تم حفظ PDF", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void QuickPrintA4_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierOrder order)
        {
            // Ensure full items loaded
            if (order.Items == null || !order.Items.Any())
            {
                using var db = new AppDbContext();
                var fullOrder = db.SupplierOrders.Include(o => o.Items).FirstOrDefault(o => o.Id == order.Id);
                if (fullOrder != null) order = fullOrder;
            }

            // Auto-save PDF backup
            PdfExportService.ExportA4InvoiceToPdf(order, openAfterSave: false);

            // Print
            A4InvoicePrintService.PrintA4Invoice(order);
        }
    }
}
