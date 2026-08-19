using System.Windows.Controls;
using System.Windows.Input;
using HamoPos.Models;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class InventoryView : UserControl
{
    public InventoryView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is Product prod && DataContext is InventoryViewModel vm)
        {
            vm.EditProductCommand.Execute(prod);
        }
    }
}
