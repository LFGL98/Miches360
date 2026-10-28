using System.Windows;
using System.Windows.Controls;
using Miches360.ViewModels;

namespace Miches360.Vistas;

public partial class ProductosAdminVista : UserControl
{
    public ProductosAdminVista()
    {
        InitializeComponent();
    }

    private void AlternarInactivos(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProductosAdminViewModel vm)
        {
            vm.MostrarInactivos = !vm.MostrarInactivos;
        }
    }
}
