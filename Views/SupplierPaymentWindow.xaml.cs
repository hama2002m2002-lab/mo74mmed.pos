using System;
using System.Windows;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.Views;

public partial class SupplierPaymentWindow : Window
{
    private readonly Supplier _supplier;
    public event Action? PaymentSaved;

    public SupplierPaymentWindow(Supplier supplier)
    {
        InitializeComponent();
        _supplier = supplier;
        TxtSupplierName.Text = $"المندوب: {supplier.Name} {(!string.IsNullOrEmpty(supplier.Company) ? $"({supplier.Company})" : "")}";
        TxtReceiptNumber.Text = $"PAY-{DateTime.Now:yyyyMMdd}-{new Random().Next(100, 999)}";
        TxtNotes.Text = $"سداد دفعة نقدية للمندوب {supplier.Name}";
        TxtAmount.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(TxtAmount.Text.Trim(), out decimal amount) || amount <= 0)
        {
            MessageBox.Show("يرجى إدخال مبلغ صحيح للدفعة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using (var db = new AppDbContext())
            {
                var service = new SupplierService(db);
                await service.AddTransactionAsync(_supplier.Id, "Payment", amount, TxtNotes.Text.Trim(), TxtReceiptNumber.Text.Trim());
            }

            PaymentSaved?.Invoke();
            MessageBox.Show($"✔ تم تسجيل دفعة بقيمة {amount:N0} د.ع بنجاح للمندوب '{_supplier.Name}'.", "نجاح السداد", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء حفظ الدفعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
