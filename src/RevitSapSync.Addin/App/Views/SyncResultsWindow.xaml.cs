using System.Windows;
using RevitSapSync.App.ViewModels;

namespace RevitSapSync.App.Views;

public partial class SyncResultsWindow : Window
{
    public SyncResultsWindow(SyncResultsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
