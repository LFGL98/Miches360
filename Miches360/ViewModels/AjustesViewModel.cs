using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Aplicacion.Ajustes;
using Miches360.Aplicacion.Dispositivos;
using Miches360.Dominio.Comun;
using Miches360.Servicios;
using Miches360.Servicios.Impresion;
using Miches360.Servidor;

namespace Miches360.ViewModels;

public sealed partial class AjustesViewModel : ObservableObject
{
    private const string ImpresoraPredeterminada = "";

    private readonly IAjustesServicio _ajustes;
    private readonly ServicioImpresion _impresion;
    private readonly ServidorLocal _servidor;
    private readonly IDispositivosServicio _dispositivos;

    public AjustesViewModel(
        IAjustesServicio ajustes,
        ServicioImpresion impresion,
        ServidorLocal servidor,
        IDispositivosServicio dispositivos,
        SesionAdministrador sesion,
        AccesoAdminViewModel acceso)
    {
        _ajustes = ajustes;
        _impresion = impresion;
        _servidor = servidor;
        _dispositivos = dispositivos;
        Sesion = sesion;
        Acceso = acceso;
        Anchos = new List<OpcionSeleccionable<int>> { new(58, "58 mm"), new(80, "80 mm") };
        OpcionesLogo = new List<OpcionSeleccionable<bool>> { new(true, "Con logo"), new(false, "Sin logo") };
        PropertyChangedEventManager.AddHandler(sesion, AlCambiarSesion, nameof(SesionAdministrador.Activa));
    }

    public ObservableCollection<CelularItem> Celulares { get; } = new();

    [ObservableProperty]
    private bool _servidorActivo;

    [ObservableProperty]
    private string _estadoServidor = string.Empty;

    [ObservableProperty]
    private string _direcciones = string.Empty;

    [ObservableProperty]
    private string _codigoVinculacion = string.Empty;

    [ObservableProperty]
    private string? _mensajeCelulares;

    [ObservableProperty]
    private bool _mensajeCelularesEsError;

    [RelayCommand]
    private void NuevoCodigo()
    {
        CodigoVinculacion = FormatearCodigo(_dispositivos.GenerarCodigo());
        MostrarMensajeCelulares("Código nuevo. El anterior ya no sirve para vincular.", esError: false);
    }

    [RelayCommand]
    private Task ActualizarCelularesAsync() => CargarCelularesAsync();

    [RelayCommand]
    private async Task QuitarCelularAsync(CelularItem? celular)
    {
        if (celular is null)
        {
            return;
        }

        if (!celular.Confirmando)
        {
            foreach (var otro in Celulares)
            {
                otro.Confirmando = false;
            }

            celular.Confirmando = true;
            return;
        }

        try
        {
            await _dispositivos.RevocarAsync(celular.Dispositivo.Id);
            await CargarCelularesAsync();
            MostrarMensajeCelulares($"Se quitó el celular de {celular.Dispositivo.Mesero}.", esError: false);
        }
        catch (Exception ex)
        {
            MostrarMensajeCelulares(ex.Message, esError: true);
        }
    }

    [RelayCommand]
    private async Task PermitirFirewallAsync()
    {
        var error = await FirewallWindows.PermitirPuertoAsync(_servidor.Puerto);
        MostrarMensajeCelulares(
            error ?? $"Listo: Windows ya deja entrar a los celulares por el puerto {_servidor.Puerto}.",
            esError: error is not null);
    }

    private async Task CargarCelularesAsync()
    {
        ServidorActivo = _servidor.Activo;
        EstadoServidor = _servidor.Estado;
        var direcciones = _servidor.Direcciones;
        Direcciones = direcciones.Count == 0
            ? "Sin red: conecta la laptop al router o enciende la zona con cobertura."
            : string.Join(Environment.NewLine, direcciones);
        CodigoVinculacion = FormatearCodigo(_dispositivos.CodigoVinculacion);

        try
        {
            var lista = await _dispositivos.ListarAsync();
            Celulares.Clear();
            foreach (var dispositivo in lista)
            {
                Celulares.Add(new CelularItem(dispositivo));
            }
        }
        catch (Exception ex)
        {
            MostrarMensajeCelulares($"No se pudieron leer los celulares: {ex.Message}", esError: true);
        }
    }

    private static string FormatearCodigo(string codigo) =>
        codigo.Length == 6 ? $"{codigo[..3]} {codigo[3..]}" : codigo;

    private void MostrarMensajeCelulares(string mensaje, bool esError)
    {
        MensajeCelularesEsError = esError;
        MensajeCelulares = mensaje;
    }

    public ObservableCollection<OpcionSeleccionable<string>> Impresoras { get; } = new();

    public IReadOnlyList<OpcionSeleccionable<int>> Anchos { get; }

    public IReadOnlyList<OpcionSeleccionable<bool>> OpcionesLogo { get; }

    [ObservableProperty]
    private bool _logo = true;

    partial void OnLogoChanged(bool value) => Opciones.Marcar(OpcionesLogo, value);

    [ObservableProperty]
    private string _nombreTicket = string.Empty;

    [ObservableProperty]
    private string _direccion = string.Empty;

    [ObservableProperty]
    private string _telefono = string.Empty;

    [ObservableProperty]
    private string _pieTicket = string.Empty;

    [ObservableProperty]
    private string _impresora = ImpresoraPredeterminada;

    [ObservableProperty]
    private int _anchoMm = 80;

    [ObservableProperty]
    private string? _mensajeTicket;

    [ObservableProperty]
    private bool _mensajeTicketEsError;

    partial void OnImpresoraChanged(string value) => Opciones.Marcar(Impresoras, value);

    partial void OnAnchoMmChanged(int value) => Opciones.Marcar(Anchos, value);

    public event Action? LimpiarCamposPin;

    public SesionAdministrador Sesion { get; }

    public AccesoAdminViewModel Acceso { get; }

    [ObservableProperty]
    private bool _pinEsInicial;

    [ObservableProperty]
    private string _banco = string.Empty;

    [ObservableProperty]
    private string _clabe = string.Empty;

    [ObservableProperty]
    private string _titular = string.Empty;

    [ObservableProperty]
    private string? _mensajeDatos;

    [ObservableProperty]
    private bool _mensajeDatosEsError;

    [ObservableProperty]
    private string? _mensajePin;

    [ObservableProperty]
    private bool _mensajePinEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarDatosCommand), nameof(CambiarPinCommand), nameof(GuardarTicketCommand), nameof(ImprimirPruebaCommand))]
    private bool _procesando;

    public string NuevoPin { get; set; } = string.Empty;

    public string ConfirmarPin { get; set; } = string.Empty;

    public async Task InicializarAsync()
    {
        if (Sesion.Activa)
        {
            await CargarAsync();
        }
        else
        {
            await Acceso.InicializarAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task GuardarDatosAsync()
    {
        Procesando = true;
        MensajeDatos = null;
        try
        {
            await _ajustes.GuardarDatosTransferenciaAsync(Sesion.Pin, Banco, Clabe, Titular);
            await CargarAsync();
            MostrarMensajeDatos("Datos guardados. Se muestran desde el siguiente cobro.", esError: false);
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensajeDatos(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensajeDatos($"No se pudo guardar: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task CambiarPinAsync()
    {
        Procesando = true;
        MensajePin = null;
        try
        {
            await _ajustes.CambiarPinAsync(Sesion.Pin, NuevoPin, ConfirmarPin);
            Sesion.ActualizarPin(NuevoPin);
            PinEsInicial = false;
            MostrarMensajePin("PIN actualizado. Úsalo la próxima vez que entres.", esError: false);
            NuevoPin = string.Empty;
            ConfirmarPin = string.Empty;
            LimpiarCamposPin?.Invoke();
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensajePin(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensajePin($"No se pudo cambiar el PIN: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void ElegirImpresora(OpcionSeleccionable<string>? opcion)
    {
        if (opcion is not null)
        {
            Impresora = opcion.Valor;
        }
    }

    [RelayCommand]
    private void ElegirAncho(OpcionSeleccionable<int>? opcion)
    {
        if (opcion is not null)
        {
            AnchoMm = opcion.Valor;
        }
    }

    [RelayCommand]
    private void ElegirLogo(OpcionSeleccionable<bool>? opcion)
    {
        if (opcion is not null)
        {
            Logo = opcion.Valor;
        }
    }

    [RelayCommand]
    private void RecargarImpresoras() => CargarImpresoras();

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task GuardarTicketAsync()
    {
        Procesando = true;
        MensajeTicket = null;
        try
        {
            await _ajustes.GuardarConfiguracionTicketAsync(Sesion.Pin, ArmarTicket());
            MostrarMensajeTicket("Datos del ticket guardados.", esError: false);
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensajeTicket(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensajeTicket($"No se pudo guardar: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private void ImprimirPrueba()
    {
        Procesando = true;
        try
        {
            var error = _impresion.ImprimirPrueba(ArmarTicket());
            MostrarMensajeTicket(
                error ?? "Prueba enviada. Si salió bien, guarda los datos.",
                esError: error is not null);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void Bloquear() => Sesion.Cerrar();

    private ConfiguracionTicketDto ArmarTicket() =>
        new(NombreTicket, Direccion, Telefono, PieTicket, Impresora, AnchoMm, ImprimirAlCobrar: false, Logo);

    private void CargarImpresoras()
    {
        var actual = Impresora;
        Impresoras.Clear();
        Impresoras.Add(new OpcionSeleccionable<string>(ImpresoraPredeterminada, "Predeterminada de Windows"));
        foreach (var nombre in ServicioImpresion.ImpresorasInstaladas())
        {
            Impresoras.Add(new OpcionSeleccionable<string>(nombre, nombre));
        }

        if (!string.IsNullOrEmpty(actual) && Impresoras.All(i => !string.Equals(i.Valor, actual, StringComparison.OrdinalIgnoreCase)))
        {
            Impresoras.Add(new OpcionSeleccionable<string>(actual, $"{actual} (no encontrada)"));
        }

        Opciones.Marcar(Impresoras, actual);
    }

    private void MostrarMensajeTicket(string mensaje, bool esError)
    {
        MensajeTicketEsError = esError;
        MensajeTicket = mensaje;
    }

    private async Task CargarAsync()
    {
        try
        {
            var ajustes = await _ajustes.AbrirAjustesAsync(Sesion.Pin);
            if (ajustes is null)
            {
                Sesion.Cerrar();
                return;
            }

            Banco = ajustes.Banco;
            Clabe = ajustes.Clabe;
            Titular = ajustes.Titular;
            PinEsInicial = ajustes.PinEsInicial;

            NombreTicket = ajustes.Ticket.NombreTicket;
            Direccion = ajustes.Ticket.Direccion;
            Telefono = ajustes.Ticket.Telefono;
            PieTicket = ajustes.Ticket.PieTicket;
            Impresora = ajustes.Ticket.Impresora;
            AnchoMm = ajustes.Ticket.AnchoMm;
            Logo = ajustes.Ticket.Logo;
            CargarImpresoras();
            await CargarCelularesAsync();
            Opciones.Marcar(Anchos, AnchoMm);
            Opciones.Marcar(OpcionesLogo, Logo);
        }
        catch (Exception ex)
        {
            MostrarMensajeDatos($"No se pudieron leer los ajustes: {ex.Message}", esError: true);
        }
    }

    private void AlCambiarSesion(object? sender, PropertyChangedEventArgs e)
    {
        if (Sesion.Activa)
        {
            _ = CargarAsync();
            return;
        }

        Banco = string.Empty;
        Clabe = string.Empty;
        Titular = string.Empty;
        MensajeDatos = null;
        MensajePin = null;
        MensajeTicket = null;
        NuevoPin = string.Empty;
        ConfirmarPin = string.Empty;
        LimpiarCamposPin?.Invoke();
        _ = Acceso.InicializarAsync();
    }

    private bool NoProcesando() => !Procesando;

    private void MostrarMensajeDatos(string mensaje, bool esError)
    {
        MensajeDatosEsError = esError;
        MensajeDatos = mensaje;
    }

    private void MostrarMensajePin(string mensaje, bool esError)
    {
        MensajePinEsError = esError;
        MensajePin = mensaje;
    }
}

public sealed partial class CelularItem : ObservableObject
{
    public CelularItem(DispositivoDto dispositivo)
    {
        Dispositivo = dispositivo;
    }

    public DispositivoDto Dispositivo { get; }

    public string Detalle
    {
        get
        {
            var modelo = string.IsNullOrWhiteSpace(Dispositivo.Modelo) ? "Celular" : Dispositivo.Modelo;
            return Dispositivo.UltimaConexion is DateTime ultima
                ? $"{modelo} · visto {ultima:dd/MM HH:mm}"
                : modelo;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoQuitar))]
    private bool _confirmando;

    public string TextoQuitar => Confirmando ? "¿Seguro?" : "Quitar";
}
