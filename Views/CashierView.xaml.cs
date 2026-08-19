using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class CashierView : UserControl
{
    public CashierView()
    {
        InitializeComponent();
        DataContextChanged += CashierView_DataContextChanged;
    }

    private void CashierView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainCashierViewModel vm)
        {
            vm.RequestFocusBarcodeField += FocusBarcode;
            vm.RequestFocusWarehouseSearch += FocusWarehouseSearch;
        }
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        FocusBarcode();
    }

    public void FocusBarcode()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (DataContext is MainCashierViewModel vm)
            {
                if (vm.IsWarehouseModalOpen || vm.IsSalesHistoryModalOpen || vm.IsInvoiceDetailsModalOpen)
                    return;
            }

            if (TxtDiscount.IsFocused)
                return;

            TxtBarcodeScan.Focus();
            TxtBarcodeScan.SelectAll();
        }));
    }

    public void FocusWarehouseSearch()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            TxtWarehouseSearch.Focus();
            TxtWarehouseSearch.SelectAll();
        }));
    }

    private void TxtBarcodeScan_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is MainCashierViewModel vm)
            {
                if (vm.BarcodeScannedCommand.CanExecute(null))
                {
                    vm.BarcodeScannedCommand.Execute(null);
                }
            }
            e.Handled = true;
            FocusBarcode();
        }
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainCashierViewModel vm)
        {
            if (vm.IsWarehouseModalOpen || vm.IsSalesHistoryModalOpen || vm.IsInvoiceDetailsModalOpen)
                return;
        }

        // إذا كان التركيز في حقل الخصم، عند ضغط Enter يعود لحقل الباركود
        if (TxtDiscount.IsFocused)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                Keyboard.ClearFocus();
                FocusBarcode();
                e.Handled = true;
            }
            return;
        }

        // إذا كان حقل الباركود غير محدد وقام السكانر أو الكاشير بكتابة باركود
        if (!TxtBarcodeScan.IsFocused)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MainCashierViewModel vm2 && vm2.BarcodeScannedCommand.CanExecute(null))
                {
                    vm2.BarcodeScannedCommand.Execute(null);
                }
                FocusBarcode();
                e.Handled = true;
            }
            else if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                // توجيه تلقائي فوري للسكانر إلى حقل الباركود
                if ((e.Key >= Key.D0 && e.Key <= Key.D9) ||
                    (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
                    (e.Key >= Key.A && e.Key <= Key.Z))
                {
                    TxtBarcodeScan.Focus();
                }
            }
        }
    }
}
