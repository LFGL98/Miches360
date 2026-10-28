using System.Globalization;
using System.Windows;
using Miches360.Aplicacion.Corte;
using Miches360.Aplicacion.Historial;
using Miches360.Dominio.Enums;

namespace Miches360.Servicios.Impresion;

internal static class DocumentosTicket
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    public static FrameworkElement Venta(TicketDto ticket, DatosTicket datos, double ancho)
    {
        var t = new ConstructorTicket(ancho);
        t.Encabezado(datos)
         .Recuadro($"FOLIO #{ticket.Folio:0000}", ticket.Fecha.ToString("dd/MM/yyyy HH:mm", Cultura));

        var atendio = string.IsNullOrWhiteSpace(ticket.Usuario) ? datos.Caja : ticket.Usuario;
        t.Centrado($"{ticket.Origen} · Atendió {atendio}", 0.9);
        if (!string.IsNullOrWhiteSpace(ticket.Personas))
        {
            t.Centrado($"Cuenta de {ticket.Personas}", 0.9, negrita: true);
        }

        if (ticket.Cancelada)
        {
            t.Banda("VENTA CANCELADA")
             .Centrado(ticket.CanceladaEn?.ToString("dd/MM/yyyy HH:mm", Cultura), 0.9)
             .Centrado(ticket.MotivoCancelacion, 0.9, negrita: true);
        }
        else if (ticket.SinCobrar)
        {
            t.Banda("CUENTA · POR PAGAR");
        }

        t.Banda("PRODUCTOS");

        var grupos = ticket.Renglones.GroupBy(r => r.Persona ?? string.Empty).ToList();
        var primero = true;
        foreach (var persona in grupos)
        {
            if (persona.Key.Length > 0)
            {
                if (!primero)
                {
                    t.Espacio(4);
                }

                t.Parrafo(persona.Key.ToUpper(Cultura), 0.95, negrita: true);
            }

            primero = false;
            var renglones = persona
                .GroupBy(r => (r.Producto, r.Tamano, r.PrecioUnitario, Nota: r.Nota ?? string.Empty))
                .Select(g => (g.Key.Producto, g.Key.Tamano, g.Key.PrecioUnitario, g.Key.Nota, Cantidad: g.Sum(r => r.Cantidad), Importe: g.Sum(r => r.Importe)));

            foreach (var r in renglones)
            {
                var partes = new List<string>();
                if (r.Tamano.Length > 0)
                {
                    partes.Add(r.Tamano);
                }

                if (r.Cantidad > 1)
                {
                    partes.Add($"{ConstructorTicket.Dinero(r.PrecioUnitario)} c/u");
                }

                if (r.Nota.Length > 0)
                {
                    partes.Add($"Nota: {r.Nota}");
                }

                t.Producto(r.Cantidad, r.Producto, partes.Count > 0 ? string.Join(" · ", partes) : null, r.Importe);
            }
        }

        var articulos = ticket.Renglones.Sum(r => r.Cantidad);
        t.TotalDestacado("TOTAL", ConstructorTicket.Dinero(ticket.Total))
         .Fila($"{articulos} artículo{(articulos == 1 ? string.Empty : "s")}", string.Empty, 0.9);

        foreach (var pago in ticket.Pagos)
        {
            var metodo = pago.Metodo == MetodoPago.Efectivo ? "Efectivo" : "Transferencia";
            t.Fila(metodo, ConstructorTicket.Dinero(pago.Monto));
        }

        if (ticket.EfectivoRecibido > 0)
        {
            t.Fila("Recibido", ConstructorTicket.Dinero(ticket.EfectivoRecibido))
             .Fila("Cambio", ConstructorTicket.Dinero(ticket.Cambio), 1.15, negrita: true);
        }

        if (ticket.Pendiente > 0)
        {
            t.Fila("Por pagar", ConstructorTicket.Dinero(ticket.Pendiente), 1.2, negrita: true);
        }

        t.Pie(datos.Pie)
         .Centrado("Este ticket no es comprobante fiscal", 0.8);

        return t.Construir();
    }

    public static FrameworkElement Corte(CorteDto corte, string tipo, string periodo, DatosTicket datos, double ancho, DateTime ahora)
    {
        var t = new ConstructorTicket(ancho);
        t.Encabezado(datos)
         .Banda($"CORTE DE CAJA · {tipo.ToUpper(Cultura)}")
         .TituloCentrado(periodo, 1.2)
         .Centrado($"Impreso {ahora.ToString("dd/MM/yyyy HH:mm", Cultura)} · {datos.Caja}", 0.85)
         .TotalDestacado("VENTA TOTAL", ConstructorTicket.Dinero(corte.Total))
         .Fila("Efectivo", ConstructorTicket.Dinero(corte.Efectivo), 1.05, negrita: true)
         .Fila("Transferencia", ConstructorTicket.Dinero(corte.Transferencia), 1.05, negrita: true)
         .Fila("Cobros", corte.Cobros.ToString(Cultura))
         .Fila("Ticket promedio", ConstructorTicket.Dinero(corte.TicketPromedio))
         .Fila("Cambio entregado", ConstructorTicket.Dinero(corte.CambioEntregado));

        if (corte.Cancelaciones.Count > 0)
        {
            t.Banda($"CANCELADAS ({corte.Cancelaciones.Count}) · {ConstructorTicket.Dinero(corte.ImporteCancelado)}");
            foreach (var c in corte.Cancelaciones)
            {
                t.Parrafo($"#{c.Folio:0000} {c.CanceladaEn.ToString("dd/MM HH:mm", Cultura)} · {ConstructorTicket.Dinero(c.Total)} · {c.Motivo}", 0.85);
            }
        }

        if (corte.PorCategoria.Count > 0)
        {
            t.Banda("POR CATEGORÍA");
            foreach (var c in corte.PorCategoria)
            {
                t.Fila($"{c.Categoria} ({c.Cantidad}) · {c.Porcentaje.ToString("P0", Cultura)}", ConstructorTicket.Dinero(c.Importe));
            }
        }

        if (corte.PorProducto.Count > 0)
        {
            t.Banda("PRODUCTOS");
            foreach (var p in corte.PorProducto)
            {
                t.Producto(p.Cantidad, p.Producto, p.Tamano, p.Importe);
            }
        }

        if (corte.PorDia.Count > 1)
        {
            t.Banda("POR DÍA");
            foreach (var d in corte.PorDia)
            {
                t.Fila($"{d.Dia.ToString("ddd dd/MM", Cultura)} ({d.Cobros})", ConstructorTicket.Dinero(d.Total));
            }
        }

        if (corte.Arqueos.Count > 0)
        {
            var a = corte.Arqueos[0];
            t.Banda($"ARQUEO · {a.RealizadoEn.ToString("dd/MM HH:mm", Cultura)}")
             .Fila("Fondo inicial", ConstructorTicket.Dinero(a.Fondo))
             .Fila("Ventas en efectivo", ConstructorTicket.Dinero(a.VentasEfectivo))
             .Fila("Debe haber", ConstructorTicket.Dinero(a.Esperado))
             .Fila("Contado", ConstructorTicket.Dinero(a.Contado))
             .Fila("Diferencia", TextoDiferencia(a.Diferencia), 1.1, negrita: true);
        }

        if (corte.CuentasAbiertas > 0)
        {
            t.Separador().Parrafo(
                $"Mesas abiertas: {corte.CuentasAbiertas} con {ConstructorTicket.Dinero(corte.PendienteAbierto)} sin cobrar (no incluidas).",
                0.9);
        }

        t.Espacio(36)
         .Centrado("______________________________")
         .Centrado("Entregó", 0.9)
         .Espacio(30)
         .Centrado("______________________________")
         .Centrado("Recibió", 0.9);

        return t.Construir();
    }

    public static FrameworkElement Prueba(DatosTicket datos, double ancho, int anchoMm, DateTime ahora)
    {
        var t = new ConstructorTicket(ancho);
        t.Encabezado(datos)
         .Recuadro("FOLIO #0000", ahora.ToString("dd/MM/yyyy HH:mm", Cultura))
         .Centrado($"Prueba de impresión · Rollo de {anchoMm} mm", 0.9)
         .Banda("PRODUCTOS")
         .Producto(2, "Michelada clásica", "Litro · $95.00 c/u", 190m)
         .Producto(1, "Papas preparadas", null, 60m)
         .TotalDestacado("TOTAL", ConstructorTicket.Dinero(250m))
         .Fila("Efectivo", ConstructorTicket.Dinero(250m))
         .Pie(datos.Pie)
         .Centrado(datos.Caja, 0.8);
        return t.Construir();
    }

    public static string TextoDiferencia(decimal diferencia) => diferencia switch
    {
        0 => "Cuadra",
        < 0 => $"Faltan {ConstructorTicket.Dinero(-diferencia)}",
        _ => $"Sobran {ConstructorTicket.Dinero(diferencia)}"
    };
}
