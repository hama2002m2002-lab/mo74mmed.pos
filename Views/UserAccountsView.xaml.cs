using System.Windows;
using System.Windows.Controls;
using HamoPos.Data;
using HamoPos.Models;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class UserAccountsView : UserControl
{
    public UserAccountsView()
    {
        InitializeComponent();
    }

    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        var win = new CashierAccountWindow();
        win.Owner = Window.GetWindow(this);
        win.UserSaved += () =>
        {
            if (DataContext is UserAccountsViewModel vm)
            {
                _ = vm.RefreshDataAsync();
            }
        };
        win.ShowDialog();
    }

    private void EditUser_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserAccountsViewModel vm && vm.SelectedCashier != null)
        {
            using var db = new AppDbContext();
            var user = db.Users.Find(vm.SelectedCashier.Id);
            if (user != null)
            {
                var win = new CashierAccountWindow(user);
                win.Owner = Window.GetWindow(this);
                win.UserSaved += () =>
                {
                    _ = vm.RefreshDataAsync();
                };
                win.ShowDialog();
            }
        }
    }

    private void ViewSaleItems_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Sale sale)
        {
            var win = new SaleItemsWindow(sale);
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }
    }
}
