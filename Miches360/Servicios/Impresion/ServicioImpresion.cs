using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Aplicacion.Ajustes;
using Miches360.Aplicacion.Corte;
using Miches360.Aplicacion.Historial;

namespace Miches360.Servicios.Impresion;

public sealed class ServicioImpresion
{
    private const double MmPorPulgada = 25.4;
    private const double DipPorPulgada = 96;

    private readonly IHistorialServicio _historial;
    private readonly IAjustesServicio _ajustes;
    private readonly IReloj _reloj;
    private readonly NegocioOpciones _negocio;

    public ServicioImpresion(IHistorialServicio historial, IAjustesServicio ajustes, IReloj reloj, IOptions<NegocioOpciones> negocio)
    {
        _historial = historial;
        _ajustes = ajustes;
        _reloj = reloj;
        _negocio = negocio.Value;
    }

    public static IReadOnlyList<string> ImpresorasInstaladas()
    {
        try
        {
            using var servidor = new LocalPrintServer();
            using var colas = servidor.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
            return colas.Select(c => c.FullName).Distinct().OrderBy(n => n).ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task<DatosTicket> ObtenerDatosTicketAsync() =>
        Datos(await _ajustes.ObtenerConfiguracionTicketAsync());

    public async Task<string?> ImprimirTicketAsync(Guid cuentaId, Guid? cobroId = null)
    {
        try
        {
            var ticket = await _historial.ObtenerTicketAsync(cuentaId, cobroId);
            var config = await _ajustes.ObtenerConfiguracionTicketAsync();
            var datos = Datos(config);
            ImprimirEnRollo(ancho => DocumentosTicket.Venta(ticket, datos, ancho), config, $"Ticket {ticket.Folio:0000}");
            return null;
        }
        catch (Exception ex)
        {
            return $"No se pudo imprimir el ticket: {ex.Message}";
        }
    }

    public async Task<string?> ImprimirCorteEnTicketAsync(CorteDto corte, string tipo, string periodo)
    {
        try
        {
            var config = await _ajustes.ObtenerConfiguracionTicketAsync();
            var datos = Datos(config);
            var ahora = _reloj.Ahora;
            ImprimirEnRollo(ancho => DocumentosTicket.Corte(corte, tipo, periodo, datos, ancho, ahora), config, $"Corte {tipo}");
            return null;
        }
        catch (Exception ex)
        {
            return $"No se pudo imprimir el corte: {ex.Message}";
        }
    }

    public async Task<string?> ImprimirReporteCorteAsync(CorteDto corte, string tipo, string periodo)
    {
        try
        {
            var config = await _ajustes.ObtenerConfiguracionTicketAsync();
            var datos = Datos(config);

            var dialogo = new PrintDialog { UserPageRangeEnabled = false };
            if (dialogo.ShowDialog() != true)
            {
                return null;
            }

            var documento = ReporteCorte.Crear(corte, tipo, periodo, datos, _reloj.Ahora);
            documento.PageWidth = dialogo.PrintableAreaWidth;
            documento.PageHeight = dialogo.PrintableAreaHeight;
            documento.ColumnWidth = dialogo.PrintableAreaWidth;

            var paginador = ((IDocumentPaginatorSource)documento).DocumentPaginator;
            dialogo.PrintDocument(paginador, $"Corte de caja {tipo} · {periodo}");
            return null;
        }
        catch (Exception ex)
        {
            return $"No se pudo imprimir el reporte: {ex.Message}";
        }
    }

    public string? ImprimirPrueba(ConfiguracionTicketDto config)
    {
        try
        {
            var datos = Datos(config);
            var ahora = _reloj.Ahora;
            ImprimirEnRollo(ancho => DocumentosTicket.Prueba(datos, ancho, config.AnchoMm, ahora), config, "Prueba de ticket");
            return null;
        }
        catch (Exception ex)
        {
            return $"No se pudo imprimir: {ex.Message}";
        }
    }

    private DatosTicket Datos(ConfiguracionTicketDto config) =>
        new(
            string.IsNullOrWhiteSpace(config.NombreTicket) ? _negocio.Nombre : config.NombreTicket,
            config.Direccion,
            config.Telefono,
            config.PieTicket,
            _negocio.NombreCaja,
            config.Logo);

    private static void ImprimirEnRollo(Func<double, FrameworkElement> construir, ConfiguracionTicketDto config, string descripcion)
    {
        var dialogo = new PrintDialog();
        if (!string.IsNullOrWhiteSpace(config.Impresora))
        {
            dialogo.PrintQueue = BuscarCola(config.Impresora);
        }

        var anchoPapel = MmADip(config.AnchoMm);
        var ticketImpresion = dialogo.PrintTicket;
        ticketImpresion.PageMediaSize = new PageMediaSize(anchoPapel, MmADip(297));

        var area = dialogo.PrintQueue.GetPrintCapabilities(ticketImpresion).PageImageableArea;
        var origen = new Point(area?.OriginWidth ?? MmADip(2), area?.OriginHeight ?? 0);

        var anchoUtil = area is { ExtentWidth: > 0 }
            ? Math.Min(area.ExtentWidth, anchoPapel - origen.X)
            : anchoPapel - MmADip(4);
        if (anchoUtil < MmADip(30))
        {
            anchoUtil = anchoPapel - MmADip(4);
        }

        var contenido = construir(anchoUtil);
        contenido.Measure(new Size(anchoUtil, double.PositiveInfinity));
        contenido.Arrange(new Rect(new Size(anchoUtil, contenido.DesiredSize.Height)));
        contenido.UpdateLayout();

        var alto = contenido.ActualHeight;
        ticketImpresion.PageMediaSize = new PageMediaSize(anchoPapel, origen.Y + alto + MmADip(4));

        var areaFinal = dialogo.PrintQueue.GetPrintCapabilities(ticketImpresion).PageImageableArea;
        var altoUtil = areaFinal is { ExtentHeight: > 0 } ? Math.Min(areaFinal.ExtentHeight, alto) : alto;
        if (alto - altoUtil < 2)
        {
            altoUtil = alto;
        }

        dialogo.PrintDocument(new PaginadorTicket(contenido, anchoPapel, origen, altoUtil), descripcion);
    }

    private static PrintQueue BuscarCola(string nombre)
    {
        var servidor = new LocalPrintServer();
        var colas = servidor.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
        return colas.FirstOrDefault(c =>
                   string.Equals(c.FullName, nombre, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(c.Name, nombre, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException($"no se encontró la impresora \"{nombre}\". Revisa que esté encendida y elígela otra vez en Ajustes.");
    }

    private static double MmADip(double mm) => mm / MmPorPulgada * DipPorPulgada;
}
