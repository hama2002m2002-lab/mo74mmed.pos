using System;
using System.Windows;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.Services;

namespace HamoPos.Views;

public partial class AddSupplierWindow : Window
{
    public event Action? SupplierAdded;

    public AddSupplierWindow()
    {
        InitializeComponent();
        TxtName.Focus();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("يرجى إدخال اسم المندوب.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        decimal.TryParse(TxtOpeningBalance.Text.Trim(), out decimal openBal);

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = name,
            Company = TxtCompany.Text.Trim(),
            Phone = TxtPhone.Text.Trim(),
            Address = TxtAddress.Text.Trim(),
            OpeningBalance = openBal,
            Balance = openBal,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            using (var db = new AppDbContext())
            {
                var service = new SupplierService(db);
                await service.SaveSupplierAsync(supplier);
            }

            SupplierAdded?.Invoke();
            MessageBox.Show($"✔ تم حفظ المندوب '{supplier.Name}' بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء حفظ المندوب: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
