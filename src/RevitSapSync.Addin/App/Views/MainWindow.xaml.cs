using System.Windows;
using RevitSapSync.App.ViewModels;

namespace RevitSapSync.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
