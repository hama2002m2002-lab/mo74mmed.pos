using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class AddProductFullView : UserControl
{
    public AddProductFullView()
    {
        InitializeComponent();
    }

    private void TxtBarcode_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AddProductFullViewModel vm)
        {
            if (vm.GenerateBarcodeCommand.CanExecute(null))
            {
                vm.GenerateBarcodeCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void TxtBarcode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        TxtBarcode_PreviewMouseDoubleClick(sender, e);
    }

    private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TxtName.Focus();
            TxtName.SelectAll();
            e.Handled = true;
        }
    }

    private void NumberTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.SelectAll();
        }
    }
}
