using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using HamoPos.Data;
using HamoPos.Models;

namespace HamoPos.Views;

public partial class AddSupplierOrderDialog : Window
{
    private readonly AppDbContext _context;
    private readonly ObservableCollection<SupplierOrderItem> _orderItems = new();
    private string _generatedOrderNumber = string.Empty;

    public AddSupplierOrderDialog()
    {
        InitializeComponent();
        _context = new AppDbContext();
        DgOrderItems.ItemsSource = _orderItems;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _generatedOrderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
        TxtOrderNumber.Text = _generatedOrderNumber;

        try
        {
            var suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
            CmbSuppliers.ItemsSource = suppliers;

            var products = await _context.Products.OrderBy(p => p.Name).ToListAsync();
            CmbProducts.ItemsSource = products;
        }
        catch { }
    }

    private void CmbProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CmbProducts.SelectedItem is Product product)
        {
            TxtItemPrice.Text = product.Cost.ToString("0");
        }
    }

    private void BtnAddItem_Click(object sender, RoutedEventArgs e)
    {
        string productName = CmbProducts.Text.Trim();
        if (CmbProducts.SelectedItem is Product p)
        {
            productName = p.Name;
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            MessageBox.Show("يرجى كتابة أو اختيار اسم المادة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(TxtItemQty.Text, out decimal qty) || qty <= 0)
        {
            MessageBox.Show("يرجى إدخال كمية صحيحة أكبر من الصفر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal.TryParse(TxtItemPrice.Text, out decimal price);

        string unitType = "Retail";
        if (CmbUnitType.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
        {
            unitType = tag;
        }

        var item = new SupplierOrderItem
        {
            ProductId = (CmbProducts.SelectedItem as Product)?.Id,
            ProductName = productName,
            Barcode = (CmbProducts.SelectedItem as Product)?.Barcode ?? "",
            Quantity = qty,
            UnitType = unitType,
            UnitPrice = price
        };

        _orderItems.Add(item);
        RecalculateGrandTotal();

        // Reset input fields
        CmbProducts.Text = "";
        CmbProducts.SelectedItem = null;
        TxtItemQty.Text = "1";
        TxtItemPrice.Text = "0";
    }

    private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is SupplierOrderItem item)
        {
            _orderItems.Remove(item);
            RecalculateGrandTotal();
        }
    }

    private void RecalculateGrandTotal()
    {
        decimal total = _orderItems.Sum(i => i.TotalPrice);
        TxtGrandTotal.Text = $"{total:N0} د.ع";
    }

    private async void BtnSaveOrder_Click(object sender, RoutedEventArgs e)
    {
        string marketName = TxtMarketName.Text.Trim();
        if (string.IsNullOrWhiteSpace(marketName))
        {
            MessageBox.Show("يرجى إدخال اسم الماركت أو العميل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!_orderItems.Any())
        {
            MessageBox.Show("يرجى إضافة مادة واحدة على الأقل في الطلبية.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var supplier = CmbSuppliers.SelectedItem as Supplier;

        var order = new SupplierOrder
        {
            OrderNumber = _generatedOrderNumber,
            OrderDate = DateTime.Now,
            MarketName = marketName,
            MarketPhone = TxtMarketPhone.Text.Trim(),
            MarketAddress = TxtMarketAddress.Text.Trim(),
            RepresentativeName = TxtRepName.Text.Trim(),
            SupplierId = supplier?.Id,
            SupplierName = supplier?.Name ?? "مندوب عام",
            Status = OrderStatus.Pending,
            TotalAmount = _orderItems.Sum(i => i.TotalPrice),
            Notes = TxtNotes.Text.Trim(),
            Items = _orderItems.ToList()
        };

        try
        {
            _context.SupplierOrders.Add(order);
            await _context.SaveChangesAsync();

            MessageBox.Show($"تم حفظ الطلبية رقم ({order.OrderNumber}) بنجاح!", "تم الحفظ ✔", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"فشل حفظ الطلبية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
