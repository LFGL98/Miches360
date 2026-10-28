using System.Windows;
using Miches360.ViewModels;

namespace Miches360;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
