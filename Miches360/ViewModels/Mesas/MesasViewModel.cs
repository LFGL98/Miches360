using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Historial;
using Miches360.Aplicacion.Mesas;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Servicios;
using Miches360.Servicios.Impresion;

namespace Miches360.ViewModels;

public sealed partial class MesaEstadoItem : ObservableObject
{
    public MesaEstadoItem(MesaEstadoDto mesa, DateTime ahora)
    {
        Mesa = mesa;
        if (mesa.Ocupada && mesa.AbiertaEn is DateTime abierta)
        {
            var minutos = (int)Math.Max(0, (ahora - abierta).TotalMinutes);
            var tiempo = minutos < 60 ? $"{minutos} min" : $"{minutos / 60} h {minutos % 60} min";
            var personas = mesa.Tipo == TipoCuenta.Separadas ? $"{mesa.Personas} pers" : "1 cuenta";
            Detalle = $"{personas} · {tiempo}";
        }
        else
        {
            Detalle = "Libre · toca para abrir";
        }
    }

    public MesaEstadoDto Mesa { get; }

    public string Detalle { get; }

    public string Estado => Mesa.Ocupada ? "ABIERTA" : "LIBRE";

    public string TextoPendiente => Mesa.Ocupada ? Formato.Dinero(Mesa.Pendiente) : string.Empty;

    [ObservableProperty]
    private bool _estaSeleccionada;
}

public sealed partial class MesasViewModel : ObservableObject
{
    private readonly IMesasServicio _mesas;
    private readonly IHistorialServicio _historial;
    private readonly ServicioImpresion _impresion;
    private readonly Navegador _navegador;
    private readonly string _usuario;

    public MesasViewModel(
        IMesasServicio mesas,
        IHistorialServicio historial,
        ServicioImpresion impresion,
        Navegador navegador,
        IOptions<NegocioOpciones> negocio)
    {
        _mesas = mesas;
        _historial = historial;
        _impresion = impresion;
        _navegador = navegador;
        _usuario = negocio.Value.NombreCaja;
    }

    public event Action? LimpiarPin;

    public IReadOnlyList<string> MotivosRapidos { get; } = new[]
    {
        "Se fueron sin consumir",
        "Mesa abierta por error",
        "Error al capturar",
        "El cliente se retractó"
    };

    [ObservableProperty]
    private TicketCobrado? _ticketPorImprimir;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarAcciones))]
    private bool _mostrarCancelacion;

    [ObservableProperty]
    private string _motivo = string.Empty;

    public string Pin { get; set; } = string.Empty;

    public bool MostrarAcciones => !MostrarCancelacion;

    public bool PuedeCancelar => Detalle is { Total: > 0 };

    public string AvisoCancelacion => Detalle is { Pagado: > 0 } cuenta
        ? $"Ya se cobraron {Formato.Dinero(cuenta.Pagado)} de esta mesa: devuélvelos, dejan de contar en el corte."
        : "La mesa queda libre y la cuenta aparece como cancelada en Historial.";

    public ObservableCollection<MesaEstadoItem> Mesas { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarAbrir), nameof(MostrarCuenta), nameof(MostrarAyuda), nameof(TituloPanel))]
    private MesaEstadoItem? _seleccionada;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarCuenta), nameof(PuedeLiberar), nameof(PuedeCancelar), nameof(AvisoCancelacion))]
    private CuentaDetalleDto? _detalle;

    [ObservableProperty]
    private bool _abrirSeparadas;

    [ObservableProperty]
    private int _personas = 2;

    [ObservableProperty]
    private string _resumen = string.Empty;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirMesaCommand), nameof(LiberarMesaCommand), nameof(ConfirmarCancelacionCommand))]
    private bool _procesando;

    public bool MostrarAyuda => Seleccionada is null;

    public bool MostrarAbrir => Seleccionada is { Mesa.Ocupada: false };

    public bool MostrarCuenta => Seleccionada is { Mesa.Ocupada: true } && Detalle is not null;

    public bool PuedeLiberar => Detalle is { Total: 0 };

    public string TituloPanel => Seleccionada?.Mesa.Nombre.ToUpperInvariant() ?? string.Empty;

    public async Task InicializarAsync(Guid? mesaId, string? mensaje, TicketCobrado? ticket = null)
    {
        if (!string.IsNullOrEmpty(mensaje))
        {
            MostrarMensaje(mensaje, esError: false);
        }

        TicketPorImprimir = ticket;

        await CargarAsync(mesaId);
    }

    [RelayCommand]
    private Task ActualizarAsync() => CargarAsync(Seleccionada?.Mesa.MesaId);

    public Task RefrescarAsync() =>
        MostrarCancelacion || Procesando ? Task.CompletedTask : CargarAsync(Seleccionada?.Mesa.MesaId);

    [RelayCommand]
    private async Task SeleccionarAsync(MesaEstadoItem? mesa)
    {
        if (mesa is null)
        {
            return;
        }

        foreach (var item in Mesas)
        {
            item.EstaSeleccionada = ReferenceEquals(item, mesa);
        }

        Seleccionada = mesa;
        Detalle = null;
        AbrirSeparadas = false;
        Personas = 2;
        CerrarCancelacion();

        if (mesa.Mesa.CuentaId is Guid cuentaId)
        {
            try
            {
                Detalle = await _mesas.ObtenerCuentaAsync(cuentaId);
            }
            catch (Exception ex)
            {
                MostrarMensaje(ex.Message, esError: true);
            }
        }
    }

    [RelayCommand]
    private void FijarTipo(string? tipo) => AbrirSeparadas = tipo == "separadas";

    [RelayCommand]
    private void MasPersonas() => Personas = Math.Min(Personas + 1, 30);

    [RelayCommand]
    private void MenosPersonas() => Personas = Math.Max(Personas - 1, 2);

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task AbrirMesaAsync()
    {
        if (Seleccionada is not { Mesa.Ocupada: false } mesa)
        {
            return;
        }

        Procesando = true;
        try
        {
            var tipo = AbrirSeparadas ? TipoCuenta.Separadas : TipoCuenta.Unica;
            var cuentaId = await _mesas.AbrirMesaAsync(mesa.Mesa.MesaId, tipo, AbrirSeparadas ? Personas : 1, _usuario);
            _navegador.IrAOrdenMesa(cuentaId);
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensaje(ex.Message, esError: true);
            await CargarAsync(mesa.Mesa.MesaId);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo abrir la mesa: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void AgregarProductos()
    {
        if (Detalle is not null)
        {
            _navegador.IrAOrdenMesa(Detalle.CuentaId);
        }
    }

    [RelayCommand]
    private void DividirYCobrar()
    {
        if (Detalle is not null)
        {
            _navegador.IrACuentaMesa(Detalle.CuentaId, null);
        }
    }

    [RelayCommand]
    private void CobrarTodo()
    {
        if (Detalle is not { } cuenta)
        {
            return;
        }

        var pendientes = cuenta.Personas.Where(p => !p.Pagada && p.Total > 0).ToList();
        if (pendientes.Count == 0)
        {
            MostrarMensaje("No hay nada pendiente de cobrar en esta mesa.", esError: true);
            return;
        }

        var mesaId = cuenta.MesaId;
        _navegador.IrACobro(CobroMesa.Crear(cuenta, pendientes, _mesas, _navegador, () => _navegador.IrAMesas(mesaId, null)));
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task LiberarMesaAsync()
    {
        if (Detalle is not { } cuenta)
        {
            return;
        }

        Procesando = true;
        try
        {
            await _mesas.CancelarCuentaAsync(cuenta.CuentaId);
            MostrarMensaje($"{cuenta.MesaNombre} quedó libre.", esError: false);
            await CargarAsync(cuenta.MesaId);
        }
        catch (Exception ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void PartesIguales()
    {
        if (Detalle is not { Pendiente: > 0 } cuenta)
        {
            MostrarMensaje("No hay nada pendiente de cobrar en esta mesa.", esError: true);
            return;
        }

        _navegador.IrAPartesIguales(cuenta.CuentaId);
    }

    [RelayCommand]
    private async Task ImprimirTicketCobrado()
    {
        if (TicketPorImprimir is not { } ticket)
        {
            return;
        }

        TicketPorImprimir = null;
        var error = await _impresion.ImprimirTicketAsync(ticket.CuentaId, ticket.CobroId);
        MostrarMensaje(error ?? $"Ticket #{ticket.Folio:0000} enviado a la impresora.", esError: error is not null);
    }

    [RelayCommand]
    private void IniciarCancelacion()
    {
        if (!PuedeCancelar)
        {
            return;
        }

        Motivo = string.Empty;
        Pin = string.Empty;
        LimpiarPin?.Invoke();
        MostrarCancelacion = true;
    }

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
    private async Task ConfirmarCancelacionAsync()
    {
        if (Detalle is not { } cuenta)
        {
            return;
        }

        Procesando = true;
        try
        {
            await _historial.CancelarVentaAsync(cuenta.CuentaId, Pin, Motivo, $"{_usuario} (admin)");
            CerrarCancelacion();
            TicketPorImprimir = null;
            MostrarMensaje($"Cuenta de {cuenta.MesaNombre} cancelada. La mesa quedó libre.", esError: false);
            await CargarAsync(cuenta.MesaId);
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

    private void CerrarCancelacion()
    {
        MostrarCancelacion = false;
        Motivo = string.Empty;
        Pin = string.Empty;
        LimpiarPin?.Invoke();
    }

    private bool NoProcesando() => !Procesando;

    private async Task CargarAsync(Guid? seleccionarMesaId)
    {
        try
        {
            var mapa = await _mesas.ObtenerMapaAsync();
            var ahora = DateTime.Now;

            Mesas.Clear();
            foreach (var mesa in mapa)
            {
                Mesas.Add(new MesaEstadoItem(mesa, ahora));
            }

            var ocupadas = mapa.Count(m => m.Ocupada);
            Resumen = $"{ocupadas} abiertas · {mapa.Count - ocupadas} libres · por cobrar {Formato.Dinero(mapa.Sum(m => m.Pendiente))}";

            var seleccion = Mesas.FirstOrDefault(m => m.Mesa.MesaId == seleccionarMesaId);
            if (seleccion is null)
            {
                Seleccionada = null;
                Detalle = null;
            }
            else
            {
                await SeleccionarAsync(seleccion);
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron cargar las mesas: {ex.Message}", esError: true);
        }
    }

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }
}
