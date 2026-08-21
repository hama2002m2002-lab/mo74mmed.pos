using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using HamoPos.Services;

namespace HamoPos.Views;

public partial class RepsManagementWindow : Window
{
    private readonly ObservableCollection<StoreRepAccount> _reps = new();

    public RepsManagementWindow()
    {
        InitializeComponent();
        LoadReps();
    }

    private void LoadReps()
    {
        _reps.Clear();
        var store = StoreSettingsService.Instance.Settings;
        foreach (var r in store.RepAccounts)
        {
            _reps.Add(r);
        }
        RepsDataGrid.ItemsSource = _reps;
    }

    private void AddRep_Click(object sender, RoutedEventArgs e)
    {
        string name = TxtRepName.Text.Trim();
        string phone = TxtRepPhone.Text.Trim();
        string pin = TxtRepPin.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("يرجى كتابة اسم المندوب على الأقل!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(pin))
        {
            pin = "1234";
        }

        var rep = new StoreRepAccount
        {
            Name = name,
            Phone = phone,
            PinCode = pin,
            IsActive = true
        };

        var store = StoreSettingsService.Instance.Settings;
        store.RepAccounts.Add(rep);
        StoreSettingsService.Instance.SaveSettings(store);

        _reps.Add(rep);
        TxtRepName.Text = "";
        TxtRepPhone.Text = "";
        TxtRepPin.Text = "1234";

        _ = CloudSyncService.Instance.PushProductsToCloudAsync();

        MessageBox.Show($"تم إنشاء حساب المندوب ({rep.Name}) برمز PIN: ({rep.PinCode}) بنجاح!\nتمت مزامنة حسابه مع بوابة الموبايل السحابية فوراً.", "تم الحفظ بنجاح", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void DeleteRep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is StoreRepAccount rep)
        {
            var confirm = MessageBox.Show($"هل أنت متأكد من حذف حساب المندوب ({rep.Name})؟", "تأكيد الحذف", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm == MessageBoxResult.Yes)
            {
                var store = StoreSettingsService.Instance.Settings;
                store.RepAccounts.RemoveAll(r => r.Id == rep.Id);
                StoreSettingsService.Instance.SaveSettings(store);

                _reps.Remove(rep);
                _ = CloudSyncService.Instance.PushProductsToCloudAsync();
            }
        }
    }

    private void SaveAndClose_Click(object sender, RoutedEventArgs e)
    {
        var store = StoreSettingsService.Instance.Settings;
        store.RepAccounts = _reps.ToList();
        StoreSettingsService.Instance.SaveSettings(store);
        _ = CloudSyncService.Instance.PushProductsToCloudAsync();

        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
