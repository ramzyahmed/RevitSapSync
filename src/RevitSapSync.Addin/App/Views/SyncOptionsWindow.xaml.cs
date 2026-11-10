using System.Windows;
using RevitSapSync.App.ViewModels;

namespace RevitSapSync.App.Views;

public partial class SyncOptionsWindow : Window
{
    public SyncOptionsWindow(SyncOptionsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Start_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
