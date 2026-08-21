using System.Windows;
using System.Windows.Controls;
using HamoPos.Models;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class SuppliersView : UserControl
{
    public SuppliersView()
    {
        InitializeComponent();
    }

    private void AddSupplier_Click(object sender, RoutedEventArgs e)
    {
        var win = new AddSupplierWindow();
        win.Owner = Window.GetWindow(this);
        win.SupplierAdded += () =>
        {
            if (DataContext is SuppliersViewModel vm)
            {
                _ = vm.LoadSuppliersAsync();
            }
        };
        win.ShowDialog();
    }

    private void RecordPayment_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SuppliersViewModel vm && vm.SelectedSupplier != null)
        {
            var win = new SupplierPaymentWindow(vm.SelectedSupplier);
            win.Owner = Window.GetWindow(this);
            win.PaymentSaved += () =>
            {
                _ = vm.LoadSupplierDetailsAsync(vm.SelectedSupplier.Id);
            };
            win.ShowDialog();
        }
        else
        {
            MessageBox.Show("يرجى اختيار مندوب أولاً لتسجيل الدفعة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ViewReceiptImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PurchaseInvoice invoice)
        {
            var win = new ReceiptImageViewerWindow(invoice);
            win.Owner = Window.GetWindow(this);
            win.ImageChanged += () =>
            {
                if (DataContext is SuppliersViewModel vm && vm.SelectedSupplier != null)
                {
                    _ = vm.LoadSupplierDetailsAsync(vm.SelectedSupplier.Id);
                }
            };
            win.ShowDialog();
        }
    }

    private void AttachReceiptImage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PurchaseInvoice invoice)
        {
            var win = new ReceiptImageViewerWindow(invoice);
            win.Owner = Window.GetWindow(this);
            win.ImageChanged += () =>
            {
                if (DataContext is SuppliersViewModel vm && vm.SelectedSupplier != null)
                {
                    _ = vm.LoadSupplierDetailsAsync(vm.SelectedSupplier.Id);
                }
            };
            win.ShowDialog();
        }
    }
}
