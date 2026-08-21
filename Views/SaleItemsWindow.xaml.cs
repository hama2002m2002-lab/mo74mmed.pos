using System.Windows;
using HamoPos.Models;

namespace HamoPos.Views;

public partial class SaleItemsWindow : Window
{
    public SaleItemsWindow(Sale sale)
    {
        InitializeComponent();
        TxtInvoiceNumber.Text = $"محتويات الفاتورة: {sale.InvoiceNumber}";
        GridItems.ItemsSource = sale.Items;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
