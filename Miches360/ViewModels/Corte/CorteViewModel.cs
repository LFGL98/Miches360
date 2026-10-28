using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Corte;
using Miches360.Mensajes;
using Miches360.Servicios;
using Miches360.Servicios.Impresion;

namespace Miches360.ViewModels;

public sealed class CategoriaCorteItem
{
    public const double AnchoMaximo = 240;

    public CategoriaCorteItem(CorteCategoriaDto categoria, decimal porcentajeMayor)
    {
        Nombre = categoria.Categoria;
        ColorHex = categoria.ColorHex;
        TextoImporte = Formato.Dinero(categoria.Importe);
        TextoDetalle = $"{categoria.Cantidad} vendido{(categoria.Cantidad == 1 ? string.Empty : "s")} · {categoria.Porcentaje:P0}";
        AnchoBarra = porcentajeMayor <= 0 ? 0 : Math.Max(6, (double)(categoria.Porcentaje / porcentajeMayor) * AnchoMaximo);
    }

    public string Nombre { get; }

    public string ColorHex { get; }

    public string TextoImporte { get; }

    public string TextoDetalle { get; }

    public double AnchoBarra { get; }
}

public sealed class ProductoCorteItem
{
    public ProductoCorteItem(CorteProductoDto producto)
    {
        Cantidad = producto.Cantidad;
        Nombre = producto.Producto;
        Tamano = producto.Tamano;
        TextoImporte = Formato.Dinero(producto.Importe);
    }

    public int Cantidad { get; }

    public string Nombre { get; }

    public string Tamano { get; }

    public string TextoImporte { get; }
}

public sealed class DiaCorteItem
{
    public DiaCorteItem(CorteDiaDto dia)
    {
        var texto = dia.Dia.ToString("dddd d", CorteViewModel.Cultura);
        Dia = char.ToUpper(texto[0], CorteViewModel.Cultura) + texto[1..];
        TextoCobros = $"{dia.Cobros} cobro{(dia.Cobros == 1 ? string.Empty : "s")}";
        TextoTotal = Formato.Dinero(dia.Total);
        TextoDesglose = $"Efectivo {Formato.Dinero(dia.Efectivo)} · Transf. {Formato.Dinero(dia.Transferencia)}";
    }

    public string Dia { get; }

    public string TextoCobros { get; }

    public string TextoTotal { get; }

    public string TextoDesglose { get; }
}

public sealed class ArqueoItem
{
    public ArqueoItem(ArqueoDto arqueo)
    {
        Encabezado = $"{arqueo.RealizadoEn:HH:mm}{(string.IsNullOrWhiteSpace(arqueo.Usuario) ? string.Empty : " · " + arqueo.Usuario)}";
        TextoDetalle = $"Esperado {Formato.Dinero(arqueo.Esperado)} · Contado {Formato.Dinero(arqueo.Contado)}";
        TextoDiferencia = CorteViewModel.TextoDiferencia(arqueo.Diferencia);
        ColorDiferencia = CorteViewModel.ColorDiferencia(arqueo.Diferencia);
        Notas = arqueo.Notas;
    }

    public string Encabezado { get; }

    public string TextoDetalle { get; }

    public string TextoDiferencia { get; }

    public string ColorDiferencia { get; }

    public string? Notas { get; }
}

public sealed partial class CorteViewModel : ObservableObject
{
    public static readonly CultureInfo Cultura = Periodos.Cultura;

    private const string ColorCuadra = "#8FD19E";
    private const string ColorFalta = "#F08A6A";
    private const string ColorSobra = "#E1AF5B";

    private readonly ICorteServicio _corte;
    private readonly ServicioImpresion _impresion;
    private readonly string _usuario;

    private DateTime _inicio = DateTime.Today;

    private readonly IndicadorCarga _carga;

    public CorteViewModel(ICorteServicio corte, ServicioImpresion impresion, IOptions<NegocioOpciones> negocio, IndicadorCarga carga)
    {
        _carga = carga;
        _corte = corte;
        _impresion = impresion;
        _usuario = negocio.Value.NombreCaja;

        Modos = new ObservableCollection<OpcionSeleccionable<TipoPeriodo>>
        {
            new(TipoPeriodo.Dia, "Día"),
            new(TipoPeriodo.Semana, "Semana"),
            new(TipoPeriodo.Mes, "Mes")
        };
        Opciones.Marcar(Modos, TipoPeriodo.Dia);
    }

    public ObservableCollection<OpcionSeleccionable<TipoPeriodo>> Modos { get; }

    public ObservableCollection<CategoriaCorteItem> Categorias { get; } = new();

    public ObservableCollection<ProductoCorteItem> Productos { get; } = new();

    public ObservableCollection<DiaCorteItem> Dias { get; } = new();

    public ObservableCollection<ArqueoItem> Arqueos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsDia), nameof(MuestraPorDia), nameof(PuedeArquear), nameof(TituloPorDia))]
    private TipoPeriodo _tipo = TipoPeriodo.Dia;

    public bool EsDia => Tipo == TipoPeriodo.Dia;

    public bool MuestraPorDia => !EsDia;

    public string TituloPorDia => Tipo == TipoPeriodo.Mes ? "Por día del mes" : "Por día";

    [ObservableProperty]
    private string? _textoCanceladas;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImprimirHojaCommand), nameof(ImprimirTicketCommand))]
    private bool _imprimiendo;

    [ObservableProperty]
    private string _textoPeriodo = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    private bool _puedeAvanzar;

    [ObservableProperty]
    private CorteDto? _corteActual;

    [ObservableProperty]
    private string _textoTotal = Formato.Dinero(0);

    [ObservableProperty]
    private string _textoEfectivo = Formato.Dinero(0);

    [ObservableProperty]
    private string _textoTransferencia = Formato.Dinero(0);

    [ObservableProperty]
    private string _textoCobros = "0";

    [ObservableProperty]
    private string _textoTicketPromedio = Formato.Dinero(0);

    [ObservableProperty]
    private string _textoPorcentajeEfectivo = string.Empty;

    [ObservableProperty]
    private string _textoPorcentajeTransferencia = string.Empty;

    [ObservableProperty]
    private bool _sinVentas = true;

    [ObservableProperty]
    private string? _avisoCuentasAbiertas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeArquear))]
    private bool _diaFuturo;

    public bool PuedeArquear => EsDia && !DiaFuturo;

    [ObservableProperty]
    private string _fondoTexto = string.Empty;

    [ObservableProperty]
    private string _contadoTexto = string.Empty;

    [ObservableProperty]
    private string _notas = string.Empty;

    [ObservableProperty]
    private string _textoVentasEfectivo = Formato.Dinero(0);

    [ObservableProperty]
    private string _textoEsperado = Formato.Dinero(0);

    [ObservableProperty]
    private string _textoDiferenciaArqueo = "—";

    [ObservableProperty]
    private string _colorDiferenciaArqueo = ColorCuadra;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RegistrarArqueoCommand))]
    private bool _procesando;

    public async Task InicializarAsync()
    {
        _inicio = DateTime.Today;
        await CargarAsync();
    }

    public static string TextoDiferencia(decimal diferencia) => diferencia switch
    {
        0 => "Cuadra",
        < 0 => $"Faltan {Formato.Dinero(-diferencia)}",
        _ => $"Sobran {Formato.Dinero(diferencia)}"
    };

    public static string ColorDiferencia(decimal diferencia) => diferencia switch
    {
        0 => ColorCuadra,
        < 0 => ColorFalta,
        _ => ColorSobra
    };

    [RelayCommand]
    private async Task SeleccionarModo(OpcionSeleccionable<TipoPeriodo>? modo)
    {
        if (modo is null || modo.Valor == Tipo)
        {
            return;
        }

        var referencia = DateTime.Today >= _inicio && DateTime.Today < Periodos.Fin(Tipo, _inicio) ? DateTime.Today : _inicio;
        Tipo = modo.Valor;
        Opciones.Marcar(Modos, modo.Valor);
        _inicio = Periodos.Inicio(Tipo, referencia);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task Anterior()
    {
        _inicio = Periodos.Mover(Tipo, _inicio, -1);
        await CargarAsync();
    }

    [RelayCommand(CanExecute = nameof(PuedeAvanzar))]
    private async Task Siguiente()
    {
        _inicio = Periodos.Mover(Tipo, _inicio, 1);
        await CargarAsync();
    }

    [RelayCommand]
    private async Task Hoy()
    {
        _inicio = Periodos.Inicio(Tipo, DateTime.Today);
        await CargarAsync();
    }

    [RelayCommand(CanExecute = nameof(PuedeImprimir))]
    private async Task ImprimirHoja()
    {
        if (CorteActual is not { } corte)
        {
            return;
        }

        Imprimiendo = true;
        try
        {
            var error = await _impresion.ImprimirReporteCorteAsync(corte, Periodos.Nombre(Tipo), TextoPeriodoLimpio());
            if (error is not null)
            {
                MostrarMensaje(error, esError: true);
            }
        }
        finally
        {
            Imprimiendo = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeImprimir))]
    private async Task ImprimirTicket()
    {
        if (CorteActual is not { } corte)
        {
            return;
        }

        Imprimiendo = true;
        try
        {
            var error = await _impresion.ImprimirCorteEnTicketAsync(corte, Periodos.Nombre(Tipo), TextoPeriodoLimpio());
            if (error is null)
            {
                WeakReferenceMessenger.Default.Send(new AvisoMensaje("Corte enviado a la impresora de tickets.", EsError: false));
            }
            else
            {
                MostrarMensaje(error, esError: true);
            }
        }
        finally
        {
            Imprimiendo = false;
        }
    }

    private bool PuedeImprimir() => !Imprimiendo;

    private string TextoPeriodoLimpio()
    {
        var texto = TextoPeriodo;
        var punto = texto.IndexOf(" · ", StringComparison.Ordinal);
        return EsDia && punto >= 0 ? texto[(punto + 3)..] : texto;
    }

    [RelayCommand]
    private Task Actualizar() => CargarAsync();

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task RegistrarArqueo()
    {
        if (!PuedeArquear)
        {
            return;
        }

        if (!IntentarLeerDinero(FondoTexto, out var fondo))
        {
            MostrarMensaje("El fondo no es un número válido.", esError: true);
            return;
        }

        if (string.IsNullOrWhiteSpace(ContadoTexto) || !IntentarLeerDinero(ContadoTexto, out var contado))
        {
            MostrarMensaje("Escribe cuánto efectivo contaste en caja.", esError: true);
            return;
        }

        Procesando = true;
        try
        {
            var arqueo = await _corte.RegistrarArqueoAsync(_inicio, fondo, contado,
                string.IsNullOrWhiteSpace(Notas) ? null : Notas.Trim(), _usuario);

            ContadoTexto = string.Empty;
            Notas = string.Empty;
            await CargarAsync();
            MostrarMensaje($"Arqueo guardado · {TextoDiferencia(arqueo.Diferencia)}", esError: arqueo.Diferencia < 0);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo guardar el arqueo: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    private bool NoProcesando() => !Procesando;

    partial void OnFondoTextoChanged(string value) => RecalcularArqueo();

    partial void OnContadoTextoChanged(string value) => RecalcularArqueo();

    private async Task CargarAsync()
    {
        var hoy = DateTime.Today;
        var desde = _inicio.Date;
        var hasta = Periodos.Fin(Tipo, desde);

        TextoPeriodo = Periodos.Texto(Tipo, desde);
        PuedeAvanzar = hasta <= hoy;
        DiaFuturo = EsDia && desde > hoy;

        try
        {
            var corte = await _carga.EjecutarAsync("Haciendo el corte", () => _corte.ObtenerCorteAsync(desde, hasta));
            Aplicar(corte);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo calcular el corte: {ex.Message}", esError: true);
        }
    }

    private void Aplicar(CorteDto corte)
    {
        CorteActual = corte;
        Mensaje = null;

        TextoTotal = Formato.Dinero(corte.Total);
        TextoEfectivo = Formato.Dinero(corte.Efectivo);
        TextoTransferencia = Formato.Dinero(corte.Transferencia);
        TextoCobros = corte.Cobros.ToString(Cultura);
        TextoTicketPromedio = Formato.Dinero(corte.TicketPromedio);
        TextoPorcentajeEfectivo = corte.Total == 0 ? string.Empty : (corte.Efectivo / corte.Total).ToString("P0", Cultura);
        TextoPorcentajeTransferencia = corte.Total == 0 ? string.Empty : (corte.Transferencia / corte.Total).ToString("P0", Cultura);
        SinVentas = corte.Cobros == 0;
        TextoCanceladas = corte.Cancelaciones.Count == 0
            ? null
            : $"{corte.Cancelaciones.Count} cancelada{(corte.Cancelaciones.Count == 1 ? string.Empty : "s")} · {Formato.Dinero(corte.ImporteCancelado)}";

        AvisoCuentasAbiertas = corte.CuentasAbiertas == 0
            ? null
            : $"Hay {corte.CuentasAbiertas} mesa{(corte.CuentasAbiertas == 1 ? string.Empty : "s")} abierta{(corte.CuentasAbiertas == 1 ? string.Empty : "s")} " +
              $"con {Formato.Dinero(corte.PendienteAbierto)} sin cobrar. No cuentan en este corte hasta que se cobren.";

        var mayor = corte.PorCategoria.Count == 0 ? 0 : corte.PorCategoria.Max(c => c.Porcentaje);
        Categorias.Clear();
        foreach (var categoria in corte.PorCategoria)
        {
            Categorias.Add(new CategoriaCorteItem(categoria, mayor));
        }

        Productos.Clear();
        foreach (var producto in corte.PorProducto)
        {
            Productos.Add(new ProductoCorteItem(producto));
        }

        Dias.Clear();
        foreach (var dia in corte.PorDia)
        {
            Dias.Add(new DiaCorteItem(dia));
        }

        Arqueos.Clear();
        foreach (var arqueo in corte.Arqueos)
        {
            Arqueos.Add(new ArqueoItem(arqueo));
        }

        if (string.IsNullOrWhiteSpace(FondoTexto) && corte.Arqueos.Count > 0)
        {
            FondoTexto = corte.Arqueos[0].Fondo.ToString("0.##", CultureInfo.InvariantCulture);
        }

        RecalcularArqueo();
    }

    private void RecalcularArqueo()
    {
        var ventasEfectivo = CorteActual?.Efectivo ?? 0;
        var fondo = IntentarLeerDinero(FondoTexto, out var f) ? f : 0;
        var esperado = fondo + ventasEfectivo;

        TextoVentasEfectivo = Formato.Dinero(ventasEfectivo);
        TextoEsperado = Formato.Dinero(esperado);

        if (!string.IsNullOrWhiteSpace(ContadoTexto) && IntentarLeerDinero(ContadoTexto, out var contado))
        {
            var diferencia = contado - esperado;
            TextoDiferenciaArqueo = TextoDiferencia(diferencia);
            ColorDiferenciaArqueo = ColorDiferencia(diferencia);
        }
        else
        {
            TextoDiferenciaArqueo = "—";
            ColorDiferenciaArqueo = ColorCuadra;
        }
    }

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }

    private static bool IntentarLeerDinero(string? texto, out decimal valor)
    {
        valor = 0;
        var limpio = (texto ?? string.Empty).Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        if (limpio.Length == 0)
        {
            return true;
        }

        return decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out valor) && valor >= 0;
    }
}
