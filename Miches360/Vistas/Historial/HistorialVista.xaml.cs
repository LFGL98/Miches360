using System.Windows;
using System.Windows.Controls;
using Miches360.ViewModels;

namespace Miches360.Vistas;

public partial class HistorialVista : UserControl
{
    public HistorialVista()
    {
        InitializeComponent();
        DataContextChanged += AlCambiarDataContext;
    }

    private HistorialViewModel? ViewModel => DataContext as HistorialViewModel;

    private void AlCambiarDataContext(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is HistorialViewModel anterior)
        {
            anterior.LimpiarPin -= LimpiarPin;
        }

        if (e.NewValue is HistorialViewModel nuevo)
        {
            nuevo.LimpiarPin += LimpiarPin;
        }
    }

    private void AlCambiarPin(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.Pin = CampoPin.Password;
        }
    }

    private void LimpiarPin() => CampoPin.Clear();
}
