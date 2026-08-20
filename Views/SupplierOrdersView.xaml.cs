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

    private void CloseModal_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SupplierOrdersViewModel vm)
        {
            vm.IsRepsModalOpen = false;
        }
    }
}
