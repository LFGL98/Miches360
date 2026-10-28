using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Aplicacion.Mesas;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Servicios;
using Miches360.Servicios.Impresion;

namespace Miches360.ViewModels;

public sealed record CuentaMesaContexto(Guid CuentaId, string? Mensaje, TicketCobrado? Ticket = null);

public sealed partial class RenglonItem : ObservableObject
{
    public RenglonItem(RenglonCuentaDto renglon, Guid subcuentaId, bool pagado)
    {
        Renglon = renglon;
        SubcuentaId = subcuentaId;
        Pagado = pagado;
    }

    public RenglonCuentaDto Renglon { get; }

    public Guid SubcuentaId { get; }

    public bool Pagado { get; }

    public string Texto => $"{Renglon.Cantidad} × {Renglon.Producto}";

    public string Detalle
    {
        get
        {
            var tamano = Renglon.Tamano == Tamano.Unico ? string.Empty : Renglon.Tamano.ToString();
            if (string.IsNullOrWhiteSpace(Renglon.Nota))
            {
                return tamano;
            }

            return tamano.Length == 0 ? $"Nota: {Renglon.Nota}" : $"{tamano} · Nota: {Renglon.Nota}";
        }
    }

    [ObservableProperty]
    private bool _estaSeleccionado;
}

public sealed partial class PersonaColumna : ObservableObject
{
    public PersonaColumna(PersonaCuentaDto persona)
    {
        Persona = persona;
        Renglones = persona.Renglones.Select(r => new RenglonItem(r, persona.SubcuentaId, persona.Pagada)).ToList();
    }

    public PersonaCuentaDto Persona { get; }

    public IReadOnlyList<RenglonItem> Renglones { get; }

    public bool PuedeCobrar => !Persona.Pagada && Persona.Total > 0;

    public string TextoCobrar => $"Cobrar {Formato.Dinero(Persona.Total)}";

    [ObservableProperty]
    private bool _puedeRecibir;

    [ObservableProperty]
    private bool _editandoNombre;

    [ObservableProperty]
    private string _nombreNuevo = string.Empty;
}

public sealed partial class CuentaMesaViewModel : ObservableObject
{
    private readonly CuentaMesaContexto _contexto;
    private readonly IMesasServicio _mesas;
    private readonly Navegador _navegador;
    private readonly ServicioImpresion _impresion;

    public CuentaMesaViewModel(CuentaMesaContexto contexto, IMesasServicio mesas, Navegador navegador, ServicioImpresion impresion)
    {
        _contexto = contexto;
        _mesas = mesas;
        _navegador = navegador;
        _impresion = impresion;
    }

    [ObservableProperty]
    private TicketCobrado? _ticketPorImprimir;

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
    private void EditarNombre(PersonaColumna? columna)
    {
        if (columna is null)
        {
            return;
        }

        foreach (var otra in Columnas)
        {
            otra.EditandoNombre = false;
        }

        columna.NombreNuevo = columna.Persona.Nombre;
        columna.EditandoNombre = true;
    }

    [RelayCommand]
    private void CancelarNombre(PersonaColumna? columna)
    {
        if (columna is not null)
        {
            columna.EditandoNombre = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task GuardarNombreAsync(PersonaColumna? columna)
    {
        if (columna is null || Cuenta is null)
        {
            return;
        }

        var nombre = columna.NombreNuevo.Trim();
        if (nombre.Length == 0 || nombre == columna.Persona.Nombre)
        {
            columna.EditandoNombre = false;
            return;
        }

        Procesando = true;
        try
        {
            await _mesas.RenombrarPersonaAsync(Cuenta.CuentaId, columna.Persona.SubcuentaId, nombre);
            MostrarMensaje($"{columna.Persona.Nombre} ahora es {nombre}.", esError: false);
            await CargarAsync();
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
        if (Cuenta is null)
        {
            return;
        }

        if (!Cuenta.Personas.Any(p => !p.Pagada && p.Total > 0))
        {
            MostrarMensaje("No hay nada pendiente de cobrar.", esError: true);
            return;
        }

        _navegador.IrAPartesIguales(Cuenta.CuentaId);
    }

    [RelayCommand]
    private async Task ImprimirCuenta()
    {
        if (Cuenta is null)
        {
            return;
        }

        var error = await _impresion.ImprimirTicketAsync(Cuenta.CuentaId);
        MostrarMensaje(error ?? "Cuenta enviada a la impresora.", esError: error is not null);
    }

    public ObservableCollection<PersonaColumna> Columnas { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulo), nameof(TextoTotal), nameof(TextoPagado), nameof(TextoPendiente))]
    private CuentaDetalleDto? _cuenta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    private RenglonItem? _seleccionado;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoverAquiCommand), nameof(QuitarSeleccionadoCommand), nameof(AgregarPersonaCommand), nameof(GuardarNombreCommand))]
    private bool _procesando;

    public string Titulo => Cuenta is null ? "CUENTA" : $"{Cuenta.MesaNombre.ToUpperInvariant()} · CUENTA #{Cuenta.Folio:0000}";

    public string TextoTotal => Formato.Dinero(Cuenta?.Total ?? 0);

    public string TextoPagado => Formato.Dinero(Cuenta?.Pagado ?? 0);

    public string TextoPendiente => Formato.Dinero(Cuenta?.Pendiente ?? 0);

    public bool HaySeleccion => Seleccionado is not null;

    public Task RefrescarAsync() =>
        Procesando || Seleccionado is not null || Columnas.Any(c => c.EditandoNombre)
            ? Task.CompletedTask
            : CargarAsync();

    public async Task InicializarAsync()
    {
        if (!string.IsNullOrEmpty(_contexto.Mensaje))
        {
            MostrarMensaje(_contexto.Mensaje, esError: false);
        }

        TicketPorImprimir = _contexto.Ticket;

        await CargarAsync();
    }

    [RelayCommand]
    private void Seleccionar(RenglonItem? renglon)
    {
        if (renglon is null || renglon.Pagado)
        {
            return;
        }

        var nuevo = ReferenceEquals(Seleccionado, renglon) ? null : renglon;
        foreach (var item in Columnas.SelectMany(c => c.Renglones))
        {
            item.EstaSeleccionado = ReferenceEquals(item, nuevo);
        }

        Seleccionado = nuevo;
        foreach (var columna in Columnas)
        {
            columna.PuedeRecibir = nuevo is not null
                && !columna.Persona.Pagada
                && columna.Persona.SubcuentaId != nuevo.SubcuentaId;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task MoverAquiAsync(PersonaColumna? destino)
    {
        if (destino is null || Seleccionado is not { } renglon || Cuenta is null)
        {
            return;
        }

        Procesando = true;
        try
        {
            await _mesas.MoverRenglonAsync(Cuenta.CuentaId, renglon.Renglon.Id, destino.Persona.SubcuentaId);
            MostrarMensaje($"{renglon.Renglon.Producto} pasó a {destino.Persona.Nombre}.", esError: false);
            await CargarAsync();
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

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task QuitarSeleccionadoAsync()
    {
        if (Seleccionado is not { } renglon || Cuenta is null)
        {
            return;
        }

        Procesando = true;
        try
        {
            await _mesas.QuitarRenglonAsync(Cuenta.CuentaId, renglon.Renglon.Id);
            MostrarMensaje($"Se quitó {renglon.Texto}.", esError: false);
            await CargarAsync();
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

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task AgregarPersonaAsync()
    {
        if (Cuenta is null)
        {
            return;
        }

        Procesando = true;
        try
        {
            await _mesas.AgregarPersonaAsync(Cuenta.CuentaId, null);
            await CargarAsync();
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
    private void CobrarPersona(PersonaColumna? columna)
    {
        if (columna is not { PuedeCobrar: true } || Cuenta is null)
        {
            return;
        }

        var cuentaId = Cuenta.CuentaId;
        _navegador.IrACobro(CobroMesa.Crear(Cuenta, new[] { columna.Persona }, _mesas, _navegador,
            () => _navegador.IrACuentaMesa(cuentaId, null)));
    }

    [RelayCommand]
    private void CobrarTodo()
    {
        if (Cuenta is null)
        {
            return;
        }

        var pendientes = Cuenta.Personas.Where(p => !p.Pagada && p.Total > 0).ToList();
        if (pendientes.Count == 0)
        {
            MostrarMensaje("No hay nada pendiente de cobrar.", esError: true);
            return;
        }

        var cuentaId = Cuenta.CuentaId;
        _navegador.IrACobro(CobroMesa.Crear(Cuenta, pendientes, _mesas, _navegador,
            () => _navegador.IrACuentaMesa(cuentaId, null)));
    }

    [RelayCommand]
    private void AgregarProductos()
    {
        if (Cuenta is not null)
        {
            _navegador.IrAOrdenMesa(Cuenta.CuentaId);
        }
    }

    [RelayCommand]
    private void Volver() => _navegador.IrAMesas(Cuenta?.MesaId, null);

    private bool NoProcesando() => !Procesando;

    private async Task CargarAsync()
    {
        try
        {
            Cuenta = await _mesas.ObtenerCuentaAsync(_contexto.CuentaId);
            Seleccionado = null;
            Columnas.Clear();
            foreach (var persona in Cuenta.Personas)
            {
                Columnas.Add(new PersonaColumna(persona));
            }

            if (Cuenta.Tipo == TipoCuenta.Unica && Columnas.Count == 1 && Mensaje is null)
            {
                MostrarMensaje("Cuenta única. Si quieren pagar por separado, agrega personas y pasa los productos tocándolos.", esError: false);
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo cargar la cuenta: {ex.Message}", esError: true);
        }
    }

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }
}
