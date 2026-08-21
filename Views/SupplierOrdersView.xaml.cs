using System.Windows;
using System.Windows.Controls;
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
}
