using System.Windows;
using System.Windows.Controls;
using Miches360.ViewModels;

namespace Miches360.Vistas;

public partial class AjustesVista : UserControl
{
    public AjustesVista()
    {
        InitializeComponent();
        DataContextChanged += AlCambiarDataContext;
    }

    private AjustesViewModel? ViewModel => DataContext as AjustesViewModel;

    private void AlCambiarDataContext(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AjustesViewModel anterior)
        {
            anterior.LimpiarCamposPin -= LimpiarCamposPin;
        }

        if (e.NewValue is AjustesViewModel nuevo)
        {
            nuevo.LimpiarCamposPin += LimpiarCamposPin;
        }
    }

    private void AlCambiarNuevoPin(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.NuevoPin = CampoNuevoPin.Password;
        }
    }

    private void AlCambiarConfirmarPin(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ConfirmarPin = CampoConfirmarPin.Password;
        }
    }

    private void LimpiarCamposPin()
    {
        CampoNuevoPin.Clear();
        CampoConfirmarPin.Clear();
    }
}
