using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Ajustes;
using Miches360.Aplicacion.Ventas;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public sealed partial class CobroViewModel : ObservableObject
{
    private const int MaximoDigitos = 7;

    private readonly CobroContexto _contexto;
    private readonly IAjustesServicio _ajustes;
    private string _buffer = string.Empty;

    public CobroViewModel(
        CobroContexto contexto,
        IAjustesServicio ajustes,
        IOptions<NegocioOpciones> negocio)
    {
        _contexto = contexto;
        _ajustes = ajustes;
        Negocio = negocio.Value;
        Concepto = $"M360-{DateTime.Now:ddHHmm}";
        Metodo = contexto.MetodoInicial;
        _ = CargarDatosTransferenciaAsync();
    }

    public NegocioOpciones Negocio { get; }

    [ObservableProperty]
    private string _banco = string.Empty;

    [ObservableProperty]
    private string _clabe = string.Empty;

    [ObservableProperty]
    private string _titular = string.Empty;

    [ObservableProperty]
    private bool _hayDatosTransferencia = true;

    public string Titulo => _contexto.Titulo;

    public IReadOnlyList<LineaCobro> Lineas => _contexto.Lineas;

    public decimal Total => _contexto.Total;

    public int Articulos => _contexto.Lineas.Sum(l => l.Cantidad);

    public string Concepto { get; }

    [ObservableProperty]
    private MetodoCobro _metodo;

    [ObservableProperty]
    private decimal _montoTeclado;

    [ObservableProperty]
    private string? _referencia;

    [ObservableProperty]
    private bool _transferenciaConfirmada;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CobrarCommand))]
    private bool _procesando;

    [ObservableProperty]
    private string? _mensajeError;

    public bool EsEfectivo => Metodo == MetodoCobro.Efectivo;

    public bool EsTransferencia => Metodo == MetodoCobro.Transferencia;

    public bool EsMixto => Metodo == MetodoCobro.Mixto;

    public bool PanelEfectivoActivo => EsEfectivo || EsMixto;

    public bool PanelTransferenciaActivo => EsTransferencia || EsMixto;

    public string TituloTeclado => EsMixto ? "Parte en efectivo" : "Recibido";

    public decimal MontoTransferencia => Metodo switch
    {
        MetodoCobro.Transferencia => Total,
        MetodoCobro.Mixto => Math.Max(Total - MontoTeclado, 0),
        _ => 0
    };

    public decimal Cambio => EsEfectivo ? Math.Max(MontoTeclado - Total, 0) : 0;

    public decimal Falta => EsEfectivo ? Math.Max(Total - MontoTeclado, 0) : 0;

    public bool ResultadoEfectivoOk => EsEfectivo ? MontoTeclado >= Total : MontoTeclado > 0 && MontoTeclado < Total;

    public string ResultadoEfectivo => Metodo switch
    {
        MetodoCobro.Efectivo when MontoTeclado == 0 => "Captura lo que entrega el cliente",
        MetodoCobro.Efectivo when Falta > 0 => $"Faltan {Formato.Dinero(Falta)}",
        MetodoCobro.Efectivo => $"Cambio {Formato.Dinero(Cambio)}",
        MetodoCobro.Mixto when MontoTeclado == 0 => "Captura la parte en efectivo",
        MetodoCobro.Mixto when MontoTeclado >= Total => "El efectivo cubre todo: usa Efectivo",
        MetodoCobro.Mixto => $"Resto por transferencia {Formato.Dinero(MontoTransferencia)}",
        _ => string.Empty
    };

    public string TextoBotonCobrar => Metodo switch
    {
        MetodoCobro.Efectivo => $"Cobrar {Formato.Dinero(Total)} en efectivo",
        MetodoCobro.Transferencia => $"Cobrar {Formato.Dinero(Total)} por transferencia",
        _ => $"Cobrar {Formato.Dinero(Total)} mixto"
    };

    public string TextoConfirmarTransferencia => TransferenciaConfirmada
        ? "Transferencia confirmada · tocar para deshacer"
        : "Ya llegó · confirmar transferencia";

    public bool PuedeCobrar => !Procesando && (Metodo switch
    {
        MetodoCobro.Efectivo => MontoTeclado >= Total,
        MetodoCobro.Transferencia => TransferenciaConfirmada,
        MetodoCobro.Mixto => MontoTeclado > 0 && MontoTeclado < Total && TransferenciaConfirmada,
        _ => false
    });

    partial void OnMetodoChanged(MetodoCobro value) => Refrescar();

    partial void OnMontoTecladoChanged(decimal value) => Refrescar();

    partial void OnTransferenciaConfirmadaChanged(bool value) => Refrescar();

    [RelayCommand]
    private void SeleccionarMetodo(string? metodo)
    {
        if (!Enum.TryParse<MetodoCobro>(metodo, out var nuevo) || nuevo == Metodo)
        {
            return;
        }

        _buffer = string.Empty;
        MontoTeclado = 0;
        TransferenciaConfirmada = false;
        MensajeError = null;
        Metodo = nuevo;
    }

    [RelayCommand]
    private void Tecla(string? digito)
    {
        if (!PanelEfectivoActivo || string.IsNullOrEmpty(digito))
        {
            return;
        }

        if (_buffer.Length == 0 && digito.All(c => c == '0'))
        {
            return;
        }

        if (_buffer.Length + digito.Length > MaximoDigitos)
        {
            return;
        }

        _buffer += digito;
        MontoTeclado = decimal.Parse(_buffer, CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private void Borrar()
    {
        if (_buffer.Length > 0)
        {
            _buffer = _buffer[..^1];
        }

        MontoTeclado = _buffer.Length == 0 ? 0 : decimal.Parse(_buffer, CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private void Billete(string? valor)
    {
        if (!PanelEfectivoActivo || string.IsNullOrEmpty(valor))
        {
            return;
        }

        if (valor == "exacto")
        {
            _buffer = string.Empty;
            MontoTeclado = Total;
            return;
        }

        if (decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var billete))
        {
            _buffer = billete.ToString("0", CultureInfo.InvariantCulture);
            MontoTeclado = billete;
        }
    }

    [RelayCommand]
    private void ConfirmarTransferencia()
    {
        if (PanelTransferenciaActivo)
        {
            TransferenciaConfirmada = !TransferenciaConfirmada;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeCobrar))]
    private async Task CobrarAsync()
    {
        Procesando = true;
        MensajeError = null;
        try
        {
            var resultado = await _contexto.Cobrar(new CobroSolicitud(ArmarPagos(), EfectivoRecibido(), Negocio.NombreCaja));
            _contexto.AlCobrar(resultado);
        }
        catch (ReglaNegocioException ex)
        {
            MensajeError = ex.Message;
        }
        catch (Exception ex)
        {
            MensajeError = $"No se pudo registrar el cobro: {ex.Message}";
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void Volver() => _contexto.AlVolver();


    private async Task CargarDatosTransferenciaAsync()
    {
        try
        {
            var datos = await _ajustes.ObtenerDatosTransferenciaAsync();
            Banco = datos.Banco;
            Clabe = datos.ClabeFormateada;
            Titular = datos.Titular;
            HayDatosTransferencia = datos.Configurado;
        }
        catch (Exception ex)
        {
            HayDatosTransferencia = false;
            MensajeError = $"No se pudieron leer los datos de transferencia: {ex.Message}";
        }
    }

    private List<PagoSolicitud> ArmarPagos()
    {
        var referencia = string.IsNullOrWhiteSpace(Referencia) ? Concepto : Referencia.Trim();
        return Metodo switch
        {
            MetodoCobro.Efectivo => new List<PagoSolicitud>
            {
                new(MetodoPago.Efectivo, Total, null)
            },
            MetodoCobro.Transferencia => new List<PagoSolicitud>
            {
                new(MetodoPago.Transferencia, Total, referencia)
            },
            _ => new List<PagoSolicitud>
            {
                new(MetodoPago.Efectivo, MontoTeclado, null),
                new(MetodoPago.Transferencia, Total - MontoTeclado, referencia)
            }
        };
    }

    private decimal EfectivoRecibido() => Metodo == MetodoCobro.Transferencia ? 0 : MontoTeclado;

    private void Refrescar()
    {
        OnPropertyChanged(string.Empty);
        CobrarCommand.NotifyCanExecuteChanged();
    }
}
