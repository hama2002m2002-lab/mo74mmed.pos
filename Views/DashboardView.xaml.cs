using System.Windows.Controls;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        Loaded += async (s, e) =>
        {
            if (DataContext is DashboardViewModel vm)
            {
                await vm.LoadDashboardDataAsync();
            }
        };
    }
}
