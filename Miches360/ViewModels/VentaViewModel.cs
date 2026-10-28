using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Miches360.Aplicacion.Ventas;
using Miches360.Dominio.Enums;
using Miches360.Mensajes;
using Miches360.Servicios;
using Miches360.Servicios.Impresion;

namespace Miches360.ViewModels;

public sealed partial class VentaViewModel : ObservableObject
{
    private readonly IVentaServicio _ventas;
    private readonly Navegador _navegador;
    private readonly ServicioImpresion _impresion;

    public VentaViewModel(SelectorProductosViewModel selector, IVentaServicio ventas, Navegador navegador, ServicioImpresion impresion)
    {
        Selector = selector;
        _ventas = ventas;
        _navegador = navegador;
        _impresion = impresion;

        Selector.ProductoElegido += AgregarProducto;
        Lineas.CollectionChanged += (_, _) => RecalcularTotales();
    }

    public SelectorProductosViewModel Selector { get; }

    public ObservableCollection<LineaTicket> Lineas { get; } = new();

    public EditorNotaViewModel EditorNota { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImprimirTicketCommand))]
    private TicketCobrado? _ultimoTicket;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private int _articulos;

    [ObservableProperty]
    private string? _mensajeExito;

    public Task InicializarAsync() => Selector.InicializarAsync();

    [RelayCommand]
    private void Incrementar(LineaTicket? linea)
    {
        if (linea is null)
        {
            return;
        }

        linea.Cantidad++;
        RecalcularTotales();
    }

    [RelayCommand]
    private void Decrementar(LineaTicket? linea)
    {
        if (linea is null)
        {
            return;
        }

        if (linea.Cantidad <= 1)
        {
            Lineas.Remove(linea);
        }
        else
        {
            linea.Cantidad--;
            RecalcularTotales();
        }
    }

    [RelayCommand]
    private void EditarNota(LineaTicket? linea)
    {
        if (linea is null)
        {
            return;
        }

        var titulo = string.IsNullOrEmpty(linea.Detalle) ? linea.Nombre : $"{linea.Nombre} · {linea.Detalle}";
        EditorNota.Abrir(titulo, linea.Nota, nota => linea.Nota = nota);
    }

    [RelayCommand(CanExecute = nameof(PuedeImprimirTicket))]
    private async Task ImprimirTicketAsync()
    {
        if (UltimoTicket is not { } ticket)
        {
            return;
        }

        UltimoTicket = null;
        var error = await _impresion.ImprimirTicketAsync(ticket.CuentaId, ticket.CobroId);
        if (error is not null)
        {
            WeakReferenceMessenger.Default.Send(new AvisoMensaje($"{error} Puedes reimprimirlo desde Historial.", EsError: true));
            return;
        }

        var texto = $"Ticket #{ticket.Folio:0000} enviado a la impresora";
        MensajeExito = texto;
        _ = OcultarMensajeAsync(texto);
    }

    private bool PuedeImprimirTicket() => UltimoTicket is not null;

    [RelayCommand(CanExecute = nameof(HayLineas))]
    private void CancelarTicket()
    {
        EditorNota.CerrarCommand.Execute(null);
        Lineas.Clear();
    }

    [RelayCommand(CanExecute = nameof(HayLineas))]
    private void CobrarEfectivo() => IrACobro(MetodoCobro.Efectivo);

    [RelayCommand(CanExecute = nameof(HayLineas))]
    private void CobrarTransferencia() => IrACobro(MetodoCobro.Transferencia);

    private bool HayLineas() => Lineas.Count > 0;

    private void AgregarProducto(ProductoItem producto, Tamano tamano, decimal precio)
    {
        var existente = Lineas.FirstOrDefault(l => l.ProductoId == producto.Id && l.Tamano == tamano && !l.TieneNota);
        if (existente is null)
        {
            Lineas.Add(new LineaTicket(producto.Id, producto.Nombre, tamano, precio));
        }
        else
        {
            existente.Cantidad++;
            RecalcularTotales();
        }

        MensajeExito = null;
        UltimoTicket = null;
    }

    private void IrACobro(MetodoCobro metodo)
    {
        var renglones = Lineas
            .Select(l => new RenglonSolicitud(l.ProductoId, l.Tamano, l.Cantidad, l.Nota))
            .ToList();

        var lineas = Lineas
            .Select(l => new LineaCobro(l.Cantidad, l.Nombre, UnirDetalle(l.Detalle, l.Nota), l.Importe))
            .ToList();

        var contexto = new CobroContexto(
            "COBRAR · PARA LLEVAR",
            lineas,
            Total,
            metodo,
            async solicitud =>
            {
                var venta = await _ventas.RegistrarVentaDirectaAsync(
                    new VentaDirectaSolicitud(renglones, solicitud.Pagos, solicitud.EfectivoRecibido, solicitud.Usuario));
                return new CobroResultado(venta.CuentaId, venta.CobroId, venta.Folio, venta.Total, venta.Cambio, CuentaCerrada: true);
            },
            AlCobrar,
            _navegador.IrAVenta);

        _navegador.IrACobro(contexto);
    }

    private void AlCobrar(CobroResultado venta)
    {
        EditorNota.CerrarCommand.Execute(null);
        Lineas.Clear();
        UltimoTicket = venta.Ticket;
        var mensaje = venta.Cambio > 0
            ? $"Venta #{venta.Folio:0000} cobrada · Cambio {Formato.Dinero(venta.Cambio)}"
            : $"Venta #{venta.Folio:0000} cobrada";
        MensajeExito = mensaje;
        _ = OcultarMensajeAsync(mensaje);
        _navegador.IrAVenta();
    }

    private async Task OcultarMensajeAsync(string mensaje)
    {
        await Task.Delay(TimeSpan.FromSeconds(12));
        if (MensajeExito == mensaje)
        {
            MensajeExito = null;
            UltimoTicket = null;
        }
    }

    private static string UnirDetalle(string detalle, string? nota) =>
        string.IsNullOrWhiteSpace(nota) ? detalle
        : string.IsNullOrEmpty(detalle) ? $"Nota: {nota}"
        : $"{detalle} · Nota: {nota}";

    private void RecalcularTotales()
    {
        Total = Lineas.Sum(l => l.Importe);
        Articulos = Lineas.Sum(l => l.Cantidad);
        CancelarTicketCommand.NotifyCanExecuteChanged();
        CobrarEfectivoCommand.NotifyCanExecuteChanged();
        CobrarTransferenciaCommand.NotifyCanExecuteChanged();
    }
}
