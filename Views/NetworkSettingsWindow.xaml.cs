using System.Windows;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class NetworkSettingsWindow : Window
{
    public NetworkSettingsWindow()
    {
        InitializeComponent();
        if (DataContext is NetworkSettingsViewModel vm)
        {
            vm.RequestClose += () => this.Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
