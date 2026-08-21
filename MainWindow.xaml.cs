using System.Windows;
using System.Windows.Controls;
using HamoPos.Models;
using HamoPos.ViewModels;

namespace HamoPos;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            if (DataContext is MainShellViewModel vm)
            {
                await vm.InitializeAsync();
            }
        };
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button btn && btn.DataContext is ShellTabItem tab && DataContext is MainShellViewModel vm)
        {
            vm.CloseTab(tab);
        }
    }
}