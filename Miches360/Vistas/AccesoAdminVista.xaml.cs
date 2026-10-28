using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Miches360.ViewModels;

namespace Miches360.Vistas;

public partial class AccesoAdminVista : UserControl
{
    public AccesoAdminVista()
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
        IsVisibleChanged += AlCambiarVisibilidad;
    }

    private void AlCambiarVisibilidad(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _ = Dispatcher.InvokeAsync(() => Focus());
        }
    }

    private void AlPresionarTecla(object sender, KeyEventArgs e)
    {
        if (DataContext is not AccesoAdminViewModel vm)
        {
            return;
        }

        var digito = e.Key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(e.Key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((int)(e.Key - Key.NumPad0)).ToString(),
            _ => null
        };

        if (digito is not null)
        {
            vm.TeclaCommand.Execute(digito);
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            vm.BorrarPinCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            vm.EntrarCommand.Execute(null);
            e.Handled = true;
        }
    }
}
