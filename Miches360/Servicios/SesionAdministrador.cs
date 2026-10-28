using CommunityToolkit.Mvvm.ComponentModel;

namespace Miches360.Servicios;

public sealed partial class SesionAdministrador : ObservableObject
{
    [ObservableProperty]
    private bool _activa;

    public string Pin { get; private set; } = string.Empty;

    public void Abrir(string pin)
    {
        Pin = pin;
        Activa = true;
    }

    public void ActualizarPin(string pin) => Pin = pin;

    public void Cerrar()
    {
        Pin = string.Empty;
        Activa = false;
    }
}
