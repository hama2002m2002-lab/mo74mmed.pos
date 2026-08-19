using System.Windows.Controls;
using System.Windows.Input;
using HamoPos.Models;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class StockView : UserControl
{
    public StockView()
    {
        InitializeComponent();
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is Product prod && DataContext is StockViewModel vm)
        {
            vm.EditProductCommand.Execute(prod);
        }
    }
}
