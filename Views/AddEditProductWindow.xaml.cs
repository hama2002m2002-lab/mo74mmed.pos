using System.Windows;
using HamoPos.ViewModels;

namespace HamoPos.Views;

public partial class AddEditProductWindow : Window
{
    public AddEditProductWindow(AddEditProductViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += (success) =>
        {
            DialogResult = success;
            Close();
        };

        Loaded += async (s, e) =>
        {
            await viewModel.InitializeAsync();
        };
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
