using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Aplicacion.Ajustes;
using Miches360.Infraestructura.Persistencia;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public sealed partial class AccesoAdminViewModel : ObservableObject
{
    private const int LargoMaximoPin = 6;

    private readonly IAjustesServicio _ajustes;
    private readonly SesionAdministrador _sesion;
    private string _pin = string.Empty;

    public AccesoAdminViewModel(IAjustesServicio ajustes, SesionAdministrador sesion)
    {
        _ajustes = ajustes;
        _sesion = sesion;
    }

    public string PinInicial => InicializadorBaseDatos.PinInicial;

    [ObservableProperty]
    private string _pinMascara = string.Empty;

    [ObservableProperty]
    private string? _errorAcceso;

    [ObservableProperty]
    private bool _pinEsInicial;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EntrarCommand))]
    private bool _procesando;

    public async Task InicializarAsync()
    {
        try
        {
            PinEsInicial = await _ajustes.PinEsInicialAsync();
        }
        catch (Exception ex)
        {
            ErrorAcceso = $"No se pudo leer la configuración: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Tecla(string? digito)
    {
        if (string.IsNullOrEmpty(digito) || _pin.Length >= LargoMaximoPin)
        {
            return;
        }

        _pin += digito;
        PinMascara = new string('●', _pin.Length);
        ErrorAcceso = null;
    }

    [RelayCommand]
    private void BorrarPin()
    {
        if (_pin.Length > 0)
        {
            _pin = _pin[..^1];
            PinMascara = new string('●', _pin.Length);
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task EntrarAsync()
    {
        if (_pin.Length < 4)
        {
            ErrorAcceso = "El PIN tiene de 4 a 6 números.";
            return;
        }

        Procesando = true;
        var pin = _pin;
        try
        {
            if (await _ajustes.VerificarPinAsync(pin))
            {
                _sesion.Abrir(pin);
            }
            else
            {
                ErrorAcceso = "PIN incorrecto.";
            }
        }
        catch (Exception ex)
        {
            ErrorAcceso = ex.Message;
        }
        finally
        {
            _pin = string.Empty;
            PinMascara = string.Empty;
            Procesando = false;
        }
    }

    private bool NoProcesando() => !Procesando;
}
