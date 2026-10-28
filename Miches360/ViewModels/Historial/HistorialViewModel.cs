using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Historial;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Mensajes;
using Miches360.Servicios;
using Miches360.Servicios.Impresion;

namespace Miches360.ViewModels;

public sealed partial class VentaHistorialItem : ObservableObject
{
    public VentaHistorialItem(VentaResumenDto venta)
    {
        Venta = venta;
    }

    public VentaResumenDto Venta { get; }

    public string Folio => $"#{Venta.Folio:0000}";

    public string Hora => Venta.Fecha.ToString("HH:mm", Periodos.Cultura);

    public string Detalle
    {
        get
        {
            var partes = new List<string> { Venta.Origen, $"{Venta.Articulos} art." };
            if (Venta.Metodos.Length > 0)
            {
                partes.Add(Venta.Metodos);
            }

            return string.Join(" · ", partes);
        }
    }

    public string TextoTotal => Formato.Dinero(Venta.Total);

    public string Estado => Venta.Estado switch
    {
        EstadoCuenta.Abierta => "ABIERTA",
        EstadoCuenta.Pagada => "COBRADA",
        _ => "CANCELADA"
    };

    public string ColorEstado => Venta.Estado switch
    {
        EstadoCuenta.Abierta => "#E1AF5B",
        EstadoCuenta.Pagada => "#8FC79B",
        _ => "#E8A27A"
    };

    public bool EsCancelada => Venta.Estado == EstadoCuenta.Cancelada;

    [ObservableProperty]
    private bool _estaSeleccionada;
}

public sealed partial class HistorialViewModel : ObservableObject
{
    private const string FiltroTodas = "todas";
    private const string FiltroCobradas = "cobradas";
    private const string FiltroAbiertas = "abiertas";
    private const string FiltroCanceladas = "canceladas";

    private readonly IHistorialServicio _historial;
    private readonly ServicioImpresion _impresion;
    private readonly string _usuario;
    private List<VentaResumenDto> _ventas = new();
    private DateTime _dia = DateTime.Today;
    private string _filtro = FiltroTodas;

    private readonly IndicadorCarga _carga;

    public HistorialViewModel(IHistorialServicio historial, ServicioImpresion impresion, IOptions<NegocioOpciones> negocio, IndicadorCarga carga)
    {
        _carga = carga;
        _historial = historial;
        _impresion = impresion;
        _usuario = negocio.Value.NombreCaja;

        Filtros = new List<OpcionSeleccionable<string>>
        {
            new(FiltroTodas, "Todas"),
            new(FiltroCobradas, "Cobradas"),
            new(FiltroAbiertas, "Abiertas"),
            new(FiltroCanceladas, "Canceladas")
        };
        Opciones.Marcar(Filtros, FiltroTodas);

        MotivosRapidos = new[]
        {
            "Error al capturar",
            "Cobro duplicado",
            "El cliente se retractó",
            "Producto no disponible"
        };
    }

    public event Action? LimpiarPin;

    public IReadOnlyList<OpcionSeleccionable<string>> Filtros { get; }

    public IReadOnlyList<string> MotivosRapidos { get; }

    public ObservableCollection<VentaHistorialItem> Ventas { get; } = new();

    [ObservableProperty]
    private string _textoDia = string.Empty;

    [ObservableProperty]
    private string _resumen = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    private bool _puedeAvanzar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion), nameof(PuedeCancelar))]
    [NotifyCanExecuteChangedFor(nameof(ReimprimirCommand), nameof(IniciarCancelacionCommand))]
    private VentaHistorialItem? _seleccionada;

    [ObservableProperty]
    private VistaPreviaTicket? _vistaPrevia;

    public bool HaySeleccion => Seleccionada is not null;

    public bool PuedeCancelar => Seleccionada is { EsCancelada: false };

    [ObservableProperty]
    private bool _mostrarCancelacion;

    [ObservableProperty]
    private string _motivo = string.Empty;

    public string Pin { get; set; } = string.Empty;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReimprimirCommand), nameof(ConfirmarCancelacionCommand))]
    private bool _procesando;

    public async Task InicializarAsync(DateTime? dia = null, Guid? seleccionar = null)
    {
        _dia = (dia ?? DateTime.Today).Date;
        await CargarAsync(seleccionar);
    }

    [RelayCommand]
    private async Task Anterior()
    {
        _dia = _dia.AddDays(-1);
        await CargarAsync(seleccionar: null);
    }

    [RelayCommand(CanExecute = nameof(PuedeAvanzar))]
    private async Task Siguiente()
    {
        _dia = _dia.AddDays(1);
        await CargarAsync(seleccionar: null);
    }

    [RelayCommand]
    private async Task Hoy()
    {
        _dia = DateTime.Today;
        await CargarAsync(seleccionar: null);
    }

    [RelayCommand]
    private Task Actualizar() => CargarAsync(Seleccionada?.Venta.CuentaId);

    [RelayCommand]
    private void Filtrar(OpcionSeleccionable<string>? filtro)
    {
        if (filtro is null)
        {
            return;
        }

        _filtro = filtro.Valor;
        Opciones.Marcar(Filtros, _filtro);
        MostrarVentas(Seleccionada?.Venta.CuentaId);
    }

    [RelayCommand]
    private async Task Seleccionar(VentaHistorialItem? item)
    {
        if (item is null)
        {
            return;
        }

        foreach (var venta in Ventas)
        {
            venta.EstaSeleccionada = ReferenceEquals(venta, item);
        }

        Seleccionada = item;
        CerrarCancelacion();
        Mensaje = null;
        await CargarTicketAsync(item.Venta.CuentaId);
    }

    [RelayCommand(CanExecute = nameof(PuedeReimprimir))]
    private async Task Reimprimir()
    {
        if (Seleccionada is null)
        {
            return;
        }

        Procesando = true;
        try
        {
            var error = await _impresion.ImprimirTicketAsync(Seleccionada.Venta.CuentaId);
            MostrarMensaje(error ?? $"Ticket {Seleccionada.Folio} enviado a la impresora.", esError: error is not null);
        }
        finally
        {
            Procesando = false;
        }
    }

    private bool PuedeReimprimir() => Seleccionada is not null && !Procesando;

    [RelayCommand(CanExecute = nameof(PuedeIniciarCancelacion))]
    private void IniciarCancelacion()
    {
        Motivo = string.Empty;
        Pin = string.Empty;
        LimpiarPin?.Invoke();
        Mensaje = null;
        MostrarCancelacion = true;
    }

    private bool PuedeIniciarCancelacion() => PuedeCancelar;

    [RelayCommand]
    private void ElegirMotivo(string? motivo)
    {
        if (!string.IsNullOrWhiteSpace(motivo))
        {
            Motivo = motivo;
        }
    }

    [RelayCommand]
    private void AbandonarCancelacion() => CerrarCancelacion();

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task ConfirmarCancelacion()
    {
        if (Seleccionada is null)
        {
            return;
        }

        Procesando = true;
        try
        {
            var cuentaId = Seleccionada.Venta.CuentaId;
            var folio = Seleccionada.Folio;
            await _historial.CancelarVentaAsync(cuentaId, Pin, Motivo, $"{_usuario} (admin)");

            CerrarCancelacion();
            await CargarAsync(cuentaId);
            MostrarMensaje($"Venta {folio} cancelada. Ya no cuenta en el corte de caja.", esError: false);
            WeakReferenceMessenger.Default.Send(new AvisoMensaje($"Venta {folio} cancelada.", EsError: false));
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo cancelar: {ex.Message}", esError: true);
        }
        finally
        {
            Pin = string.Empty;
            LimpiarPin?.Invoke();
            Procesando = false;
        }
    }

    private bool NoProcesando() => !Procesando;

    private async Task CargarAsync(Guid? seleccionar)
    {
        var hasta = _dia.AddDays(1);
        TextoDia = Periodos.TextoDia(_dia);
        PuedeAvanzar = hasta <= DateTime.Today;

        try
        {
            _ventas = (await _carga.EjecutarAsync("Buscando las ventas", () => _historial.ListarAsync(_dia, hasta))).ToList();
        }
        catch (Exception ex)
        {
            _ventas = new List<VentaResumenDto>();
            MostrarMensaje($"No se pudo cargar el historial: {ex.Message}", esError: true);
        }

        var cobradas = _ventas.Where(v => v.Estado == EstadoCuenta.Pagada).ToList();
        var canceladas = _ventas.Count(v => v.Estado == EstadoCuenta.Cancelada);
        var abiertas = _ventas.Count(v => v.Estado == EstadoCuenta.Abierta);
        Resumen = $"{cobradas.Count} cobrada{(cobradas.Count == 1 ? string.Empty : "s")} · {Formato.Dinero(cobradas.Sum(v => v.Total))}" +
                  (abiertas > 0 ? $" · {abiertas} abierta{(abiertas == 1 ? string.Empty : "s")}" : string.Empty) +
                  (canceladas > 0 ? $" · {canceladas} cancelada{(canceladas == 1 ? string.Empty : "s")}" : string.Empty);

        MostrarVentas(seleccionar);
    }

    private void MostrarVentas(Guid? seleccionar)
    {
        Ventas.Clear();
        foreach (var venta in _ventas.Where(CumpleFiltro))
        {
            Ventas.Add(new VentaHistorialItem(venta));
        }

        var item = seleccionar is Guid id ? Ventas.FirstOrDefault(v => v.Venta.CuentaId == id) : null;
        if (item is null)
        {
            Seleccionada = null;
            VistaPrevia = null;
            CerrarCancelacion();
            return;
        }

        item.EstaSeleccionada = true;
        Seleccionada = item;
        _ = CargarTicketAsync(item.Venta.CuentaId);
    }

    private bool CumpleFiltro(VentaResumenDto venta) => _filtro switch
    {
        FiltroCobradas => venta.Estado == EstadoCuenta.Pagada,
        FiltroAbiertas => venta.Estado == EstadoCuenta.Abierta,
        FiltroCanceladas => venta.Estado == EstadoCuenta.Cancelada,
        _ => true
    };

    private async Task CargarTicketAsync(Guid cuentaId)
    {
        try
        {
            var ticket = await _historial.ObtenerTicketAsync(cuentaId);
            var datos = await _impresion.ObtenerDatosTicketAsync();
            if (Seleccionada?.Venta.CuentaId == cuentaId)
            {
                VistaPrevia = new VistaPreviaTicket(ticket, datos);
            }
        }
        catch (Exception ex)
        {
            VistaPrevia = null;
            MostrarMensaje($"No se pudo leer el ticket: {ex.Message}", esError: true);
        }
    }

    private void CerrarCancelacion()
    {
        MostrarCancelacion = false;
        Motivo = string.Empty;
        Pin = string.Empty;
        LimpiarPin?.Invoke();
    }

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }
}
