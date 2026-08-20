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
            _ = vm.InitializeAsync();
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

    private void TxtBarcode_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            TriggerGenerateBarcode();
            e.Handled = true;
        }
    }

    private void TxtBarcode_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            TriggerGenerateBarcode();
            e.Handled = true;
        }
    }

    private void TxtBarcode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        TriggerGenerateBarcode();
        e.Handled = true;
    }

    private void BtnGenerateBarcode_Click(object sender, RoutedEventArgs e)
    {
        TriggerGenerateBarcode();
    }

    private void TriggerGenerateBarcode()
    {
        var random = new Random();
        int suffix = random.Next(1000000, 9999999);
        string newCode = $"200245{suffix}";

        if (DataContext is AddProductFullViewModel vm)
        {
            vm.Barcode = newCode;
        }

        TxtBarcode.Text = newCode;

        Dispatcher.InvokeAsync(() =>
        {
            TxtName?.Focus();
            TxtName?.SelectAll();
        });
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
