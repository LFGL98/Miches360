using System.Windows;
using System.Windows.Controls;
using Miches360.ViewModels;

namespace Miches360.Vistas;

public partial class MesasVista : UserControl
{
    public MesasVista()
    {
        InitializeComponent();
        DataContextChanged += AlCambiarDataContext;
    }

    private MesasViewModel? ViewModel => DataContext as MesasViewModel;

    private void AlCambiarDataContext(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MesasViewModel anterior)
        {
            anterior.LimpiarPin -= LimpiarPin;
        }

        if (e.NewValue is MesasViewModel nuevo)
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
