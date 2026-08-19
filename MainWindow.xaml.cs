using System.Windows;
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
}