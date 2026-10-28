using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Ajustes;
using Miches360.Aplicacion.Mesas;
using Miches360.Aplicacion.Ventas;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public sealed record PartesIgualesContexto(Guid CuentaId);

public sealed record BilleteParte(ParteItem Parte, decimal Valor, string Texto);

public sealed partial class ParteItem : ObservableObject
{
    private static readonly decimal[] Denominaciones = { 50, 100, 200, 500, 1000 };

    public ParteItem(int numero, decimal monto)
    {
        Numero = numero;
        Monto = monto;
        Billetes = new[] { new BilleteParte(this, monto, "Exacto") }
            .Concat(Denominaciones.Where(d => d > monto).Take(3).Select(d => new BilleteParte(this, d, Formato.Dinero(d))))
            .ToList();
    }

    public int Numero { get; }

    public decimal Monto { get; }

    public string Titulo => $"PARTE {Numero}";

    public IReadOnlyList<BilleteParte> Billetes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Pagada), nameof(Pendiente), nameof(EsEfectivo), nameof(EsTransferencia), nameof(TextoEstado), nameof(Cambio), nameof(TextoCambio))]
    private MetodoPago? _metodo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cambio), nameof(TextoCambio))]
    private decimal _recibido;

    public bool Pagada => Metodo is not null;

    public bool Pendiente => Metodo is null;

    public bool EsEfectivo => Metodo == MetodoPago.Efectivo;

    public bool EsTransferencia => Metodo == MetodoPago.Transferencia;

    public decimal Cambio => EsEfectivo ? Math.Max(Recibido - Monto, 0) : 0;

    public string TextoEstado => Metodo switch
    {
        MetodoPago.Efectivo => "PAGÓ EN EFECTIVO",
        MetodoPago.Transferencia => "PAGÓ POR TRANSFERENCIA",
        _ => "POR PAGAR"
    };

    public string TextoCambio => !EsEfectivo
        ? string.Empty
        : Cambio > 0 ? $"Recibido {Formato.Dinero(Recibido)} · Cambio {Formato.Dinero(Cambio)}" : "Pago exacto";
}

public sealed partial class PartesIgualesViewModel : ObservableObject
{
    private const int MinimoPartes = 2;
    private const int MaximoPartes = 20;

    private readonly PartesIgualesContexto _contexto;
    private readonly IMesasServicio _mesas;
    private readonly IAjustesServicio _ajustes;
    private readonly Navegador _navegador;
    private readonly string _usuario;
    private CuentaDetalleDto? _cuenta;
    private List<Guid> _subcuentas = new();

    public PartesIgualesViewModel(
        PartesIgualesContexto contexto,
        IMesasServicio mesas,
        IAjustesServicio ajustes,
        Navegador navegador,
        IOptions<NegocioOpciones> negocio)
    {
        _contexto = contexto;
        _mesas = mesas;
        _ajustes = ajustes;
        _navegador = navegador;
        _usuario = negocio.Value.NombreCaja;
    }

    public ObservableCollection<ParteItem> Partes { get; } = new();

    [ObservableProperty]
    private string _titulo = "PARTES IGUALES";

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private int _numeroPartes = 2;

    [ObservableProperty]
    private string _textoPorParte = string.Empty;

    [ObservableProperty]
    private string _textoAvance = string.Empty;

    [ObservableProperty]
    private string _datosTransferencia = string.Empty;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegistrarCobroCommand))]
    private bool _todasPagadas;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegistrarCobroCommand))]
    private bool _procesando;

    public async Task InicializarAsync()
    {
        try
        {
            _cuenta = await _mesas.ObtenerCuentaAsync(_contexto.CuentaId);
            var pendientes = _cuenta.Personas.Where(p => !p.Pagada && p.Total > 0).ToList();
            _subcuentas = pendientes.Select(p => p.SubcuentaId).ToList();
            Total = pendientes.Sum(p => p.Total);
            Titulo = $"{_cuenta.MesaNombre.ToUpperInvariant()} · PARTES IGUALES";

            NumeroPartes = Math.Clamp(pendientes.Count > 1 ? pendientes.Count : 2, MinimoPartes, MaximoPartes);
            Repartir();

            if (Total <= 0)
            {
                MostrarMensaje("No hay nada pendiente de cobrar en esta mesa.", esError: true);
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo cargar la cuenta: {ex.Message}", esError: true);
        }

        try
        {
            var datos = await _ajustes.ObtenerDatosTransferenciaAsync();
            DatosTransferencia = datos.Configurado
                ? $"{datos.Banco} · {datos.ClabeFormateada} · {datos.Titular}"
                : "Sin datos de transferencia: captúralos en Ajustes.";
        }
        catch
        {
            DatosTransferencia = string.Empty;
        }
    }

    [RelayCommand]
    private void MasPartes() => CambiarPartes(NumeroPartes + 1);

    [RelayCommand]
    private void MenosPartes() => CambiarPartes(NumeroPartes - 1);

    [RelayCommand]
    private void PagarEfectivo(ParteItem? parte)
    {
        if (parte is null)
        {
            return;
        }

        parte.Recibido = parte.Monto;
        parte.Metodo = MetodoPago.Efectivo;
        ActualizarAvance();
    }

    [RelayCommand]
    private void PagarTransferencia(ParteItem? parte)
    {
        if (parte is null)
        {
            return;
        }

        parte.Recibido = 0;
        parte.Metodo = MetodoPago.Transferencia;
        ActualizarAvance();
    }

    [RelayCommand]
    private void FijarRecibido(BilleteParte? billete)
    {
        if (billete is { Parte.EsEfectivo: true })
        {
            billete.Parte.Recibido = billete.Valor;
        }
    }

    [RelayCommand]
    private void Deshacer(ParteItem? parte)
    {
        if (parte is null)
        {
            return;
        }

        parte.Metodo = null;
        parte.Recibido = 0;
        ActualizarAvance();
    }

    [RelayCommand(CanExecute = nameof(PuedeRegistrar))]
    private async Task RegistrarCobroAsync()
    {
        if (_cuenta is null)
        {
            return;
        }

        Procesando = true;
        try
        {
            var pagos = Partes
                .Select(p => new PagoSolicitud(
                    p.Metodo!.Value,
                    p.Monto,
                    p.Metodo == MetodoPago.Transferencia ? $"M360-{_cuenta.Folio:0000}-P{p.Numero}" : null))
                .ToList();

            var efectivoRecibido = Partes.Where(p => p.EsEfectivo).Sum(p => p.Recibido);

            var resultado = await _mesas.CobrarAsync(new CobroMesaSolicitud(
                _cuenta.CuentaId,
                _subcuentas,
                pagos,
                efectivoRecibido,
                _usuario));

            var ticket = new TicketCobrado(resultado.CuentaId, resultado.CobroId, resultado.Folio);
            var cambio = resultado.Cambio > 0 ? $" · Cambio total {Formato.Dinero(resultado.Cambio)}" : string.Empty;
            if (resultado.CuentaCerrada)
            {
                _navegador.IrAMesas(_cuenta.MesaId, $"{_cuenta.MesaNombre} cobrada en {Partes.Count} partes{cambio}", ticket);
            }
            else
            {
                _navegador.IrACuentaMesa(_cuenta.CuentaId, $"Cobro en {Partes.Count} partes registrado{cambio}.", ticket);
            }
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo registrar el cobro: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void Volver() => _navegador.IrACuentaMesa(_contexto.CuentaId, null);

    private bool PuedeRegistrar() => TodasPagadas && !Procesando && Total > 0;

    private void CambiarPartes(int partes)
    {
        partes = Math.Clamp(partes, MinimoPartes, MaximoPartes);
        if (partes == NumeroPartes)
        {
            return;
        }

        var habiaPagos = Partes.Any(p => p.Pagada);
        NumeroPartes = partes;
        Repartir();
        if (habiaPagos)
        {
            MostrarMensaje("Cambiaste el número de partes: vuelve a marcar quién ya pagó.", esError: true);
        }
    }

    private void Repartir()
    {
        Partes.Clear();
        var centavos = (long)decimal.Round(Total * 100);
        var basePorParte = centavos / NumeroPartes;
        var sobrante = centavos % NumeroPartes;

        for (var i = 0; i < NumeroPartes; i++)
        {
            var monto = (basePorParte + (i < sobrante ? 1 : 0)) / 100m;
            Partes.Add(new ParteItem(i + 1, monto));
        }

        var menor = basePorParte / 100m;
        TextoPorParte = sobrante == 0
            ? $"{Formato.Dinero(menor)} cada parte"
            : $"{Formato.Dinero(menor)} cada parte · {sobrante} paga{(sobrante == 1 ? string.Empty : "n")} 1 centavo más";

        Mensaje = null;
        ActualizarAvance();
    }

    private void ActualizarAvance()
    {
        var pagadas = Partes.Count(p => p.Pagada);
        var falta = Partes.Where(p => !p.Pagada).Sum(p => p.Monto);
        TodasPagadas = Partes.Count > 0 && pagadas == Partes.Count;
        TextoAvance = TodasPagadas
            ? "Todas las partes pagaron"
            : $"Pagaron {pagadas} de {Partes.Count} · faltan {Formato.Dinero(falta)}";
    }

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }
}
