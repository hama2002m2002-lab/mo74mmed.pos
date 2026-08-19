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
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AddProductFullViewModel vm)
        {
            vm.RequestFocusNameField += FocusNameField;
            vm.RequestFocusBarcodeField += FocusBarcodeField;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AddProductFullViewModel vm)
        {
            vm.RequestFocusNameField -= FocusNameField;
            vm.RequestFocusBarcodeField -= FocusBarcodeField;
        }
    }

    private void FocusNameField()
    {
        Dispatcher.InvokeAsync(() =>
        {
            TxtName?.Focus();
            TxtName?.SelectAll();
        });
    }

    private void FocusBarcodeField()
    {
        Dispatcher.InvokeAsync(() =>
        {
            TxtBarcode?.Focus();
            TxtBarcode?.SelectAll();
        });
    }

    private void TxtBarcode_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            TriggerGenerateBarcode();
            e.Handled = true;
        }
    }

    private void TxtBarcode_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        TriggerGenerateBarcode();
        e.Handled = true;
    }

    private void TxtBarcode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        TriggerGenerateBarcode();
        e.Handled = true;
    }

    private void TriggerGenerateBarcode()
    {
        if (DataContext is AddProductFullViewModel vm)
        {
            if (vm.GenerateBarcodeCommand.CanExecute(null))
            {
                vm.GenerateBarcodeCommand.Execute(null);
            }
        }
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
