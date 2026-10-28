using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Miches360.Aplicacion.Corte;

namespace Miches360.Servicios.Impresion;

internal static class ReporteCorte
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    private enum Alinear
    {
        Izquierda,
        Derecha
    }

    public static FlowDocument Crear(CorteDto corte, string tipo, string periodo, DatosTicket datos, DateTime ahora)
    {
        var doc = new FlowDocument
        {
            FontFamily = Marca.FuenteTexto,
            FontSize = 11.5,
            Foreground = Marca.TextoTicket,
            Background = Brushes.White,
            PagePadding = new Thickness(48, 40, 48, 40),
            TextAlignment = TextAlignment.Left
        };

        doc.Blocks.Add(Encabezado(datos, tipo, periodo, ahora));
        doc.Blocks.Add(Indicadores(corte));

        var resumen = new List<string[]>
        {
            new[] { "Ticket promedio", Dinero(corte.TicketPromedio) },
            new[] { "Cambio entregado", Dinero(corte.CambioEntregado) },
            new[] { "Ventas canceladas", $"{corte.Cancelaciones.Count} · {Dinero(corte.ImporteCancelado)}" }
        };
        if (corte.CuentasAbiertas > 0)
        {
            resumen.Add(new[] { "Mesas abiertas (no incluidas)", $"{corte.CuentasAbiertas} · {Dinero(corte.PendienteAbierto)}" });
        }

        doc.Blocks.Add(Tabla(
            new[] { ("Concepto", 3.0, Alinear.Izquierda), ("Importe", 2.0, Alinear.Derecha) },
            resumen,
            encabezado: false,
            primeraEnNegrita: true));

        if (corte.PorDia.Count > 1)
        {
            Titulo(doc, "Por día");
            doc.Blocks.Add(Tabla(
                new[]
                {
                    ("Día", 2.4, Alinear.Izquierda),
                    ("Cobros", 1.0, Alinear.Derecha),
                    ("Efectivo", 1.6, Alinear.Derecha),
                    ("Transferencia", 1.6, Alinear.Derecha),
                    ("Total", 1.6, Alinear.Derecha)
                },
                corte.PorDia.Select(d => new[]
                {
                    Capital(d.Dia.ToString("dddd dd/MM", Cultura)),
                    d.Cobros.ToString(Cultura),
                    Dinero(d.Efectivo),
                    Dinero(d.Transferencia),
                    Dinero(d.Total)
                }),
                total: new[]
                {
                    "Total",
                    corte.Cobros.ToString(Cultura),
                    Dinero(corte.Efectivo),
                    Dinero(corte.Transferencia),
                    Dinero(corte.Total)
                }));
        }

        Titulo(doc, "Por categoría");
        if (corte.PorCategoria.Count == 0)
        {
            doc.Blocks.Add(new Paragraph(new Run("Sin ventas cobradas en este periodo.")) { Foreground = Marca.TextoTicketSuave });
        }
        else
        {
            doc.Blocks.Add(Tabla(
                new[]
                {
                    ("Categoría", 3.0, Alinear.Izquierda),
                    ("Cantidad", 1.2, Alinear.Derecha),
                    ("%", 1.0, Alinear.Derecha),
                    ("Importe", 1.6, Alinear.Derecha)
                },
                corte.PorCategoria.Select(c => new[]
                {
                    c.Categoria,
                    c.Cantidad.ToString(Cultura),
                    c.Porcentaje.ToString("P0", Cultura),
                    Dinero(c.Importe)
                }),
                colores: corte.PorCategoria.Select(c => c.ColorHex).ToList()));
        }

        if (corte.PorProducto.Count > 0)
        {
            Titulo(doc, "Por producto");
            doc.Blocks.Add(Tabla(
                new[]
                {
                    ("Producto", 3.4, Alinear.Izquierda),
                    ("Tamaño", 1.2, Alinear.Izquierda),
                    ("Cantidad", 1.0, Alinear.Derecha),
                    ("Importe", 1.6, Alinear.Derecha)
                },
                corte.PorProducto.Select(p => new[]
                {
                    p.Producto,
                    p.Tamano,
                    p.Cantidad.ToString(Cultura),
                    Dinero(p.Importe)
                }),
                total: new[]
                {
                    "Total",
                    string.Empty,
                    corte.PorProducto.Sum(p => p.Cantidad).ToString(Cultura),
                    Dinero(corte.PorProducto.Sum(p => p.Importe))
                }));
        }

        if (corte.Cancelaciones.Count > 0)
        {
            Titulo(doc, "Ventas canceladas");
            doc.Blocks.Add(Tabla(
                new[]
                {
                    ("Folio", 0.9, Alinear.Izquierda),
                    ("Cancelada", 1.5, Alinear.Izquierda),
                    ("Origen", 1.4, Alinear.Izquierda),
                    ("Motivo", 3.0, Alinear.Izquierda),
                    ("Importe", 1.3, Alinear.Derecha)
                },
                corte.Cancelaciones.Select(c => new[]
                {
                    $"#{c.Folio:0000}",
                    c.CanceladaEn.ToString("dd/MM HH:mm", Cultura),
                    c.Origen,
                    string.IsNullOrWhiteSpace(c.Usuario) ? c.Motivo : $"{c.Motivo} ({c.Usuario})",
                    Dinero(c.Total)
                })));
        }

        if (corte.Arqueos.Count > 0)
        {
            Titulo(doc, "Arqueos de caja");
            doc.Blocks.Add(Tabla(
                new[]
                {
                    ("Fecha", 1.6, Alinear.Izquierda),
                    ("Fondo", 1.2, Alinear.Derecha),
                    ("Ventas efectivo", 1.5, Alinear.Derecha),
                    ("Debe haber", 1.4, Alinear.Derecha),
                    ("Contado", 1.3, Alinear.Derecha),
                    ("Diferencia", 1.6, Alinear.Derecha)
                },
                corte.Arqueos.Select(a => new[]
                {
                    a.RealizadoEn.ToString("dd/MM HH:mm", Cultura),
                    Dinero(a.Fondo),
                    Dinero(a.VentasEfectivo),
                    Dinero(a.Esperado),
                    Dinero(a.Contado),
                    DocumentosTicket.TextoDiferencia(a.Diferencia)
                })));
        }

        doc.Blocks.Add(Firmas());
        doc.Blocks.Add(new Paragraph(new Run($"{datos.Nombre} · Punto de venta Miches 360"))
        {
            FontSize = 9,
            Foreground = Marca.TextoTicketSuave,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 18, 0, 0)
        });

        return doc;
    }

    private static Block Encabezado(DatosTicket datos, string tipo, string periodo, DateTime ahora)
    {
        var rejilla = new Grid();
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rejilla.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        if (datos.Logo && Marca.Logo is { } logo)
        {
            rejilla.Children.Add(new Border
            {
                Width = 66,
                Height = 66,
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(0, 0, 14, 0),
                Background = new ImageBrush(logo) { Stretch = Stretch.UniformToFill }
            });
        }

        var negocio = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        negocio.Children.Add(Texto(datos.Nombre.ToUpper(Cultura), 28, Marca.Encabezado, titulo: true));
        var contacto = string.Join(" · ", new[] { datos.Direccion, string.IsNullOrWhiteSpace(datos.Telefono) ? string.Empty : $"Tel. {datos.Telefono}" }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (contacto.Length > 0)
        {
            negocio.Children.Add(Texto(contacto, 11, Marca.TextoTicketSuave));
        }

        Grid.SetColumn(negocio, 1);
        rejilla.Children.Add(negocio);

        var corte = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        var etiqueta = new Border
        {
            Background = Marca.Encabezado,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 3, 12, 3),
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = Texto($"CORTE {tipo.ToUpper(Cultura)}", 15, Marca.Acento, titulo: true)
        };
        corte.Children.Add(etiqueta);
        var textoPeriodo = Texto(periodo, 14, Marca.TextoTicket, titulo: true);
        textoPeriodo.TextAlignment = TextAlignment.Right;
        textoPeriodo.Margin = new Thickness(0, 4, 0, 0);
        corte.Children.Add(textoPeriodo);
        var impreso = Texto($"Impreso {ahora.ToString("dd/MM/yyyy HH:mm", Cultura)} · {datos.Caja}", 10, Marca.TextoTicketSuave);
        impreso.TextAlignment = TextAlignment.Right;
        corte.Children.Add(impreso);
        Grid.SetColumn(corte, 2);
        rejilla.Children.Add(corte);

        var contenedor = new StackPanel();
        contenedor.Children.Add(rejilla);
        contenedor.Children.Add(new Border { Height = 5, Background = Marca.Acento, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 12, 0, 0) });

        return new BlockUIContainer(contenedor) { Margin = new Thickness(0, 0, 0, 14) };
    }

    private static Block Indicadores(CorteDto corte)
    {
        var tarjetas = new UniformGrid { Columns = 4, Rows = 1 };
        tarjetas.Children.Add(Tarjeta("VENTA TOTAL", Dinero(corte.Total), null, destacada: true));
        tarjetas.Children.Add(Tarjeta("EFECTIVO", Dinero(corte.Efectivo), Porcentaje(corte.Efectivo, corte.Total), destacada: false));
        tarjetas.Children.Add(Tarjeta("TRANSFERENCIA", Dinero(corte.Transferencia), Porcentaje(corte.Transferencia, corte.Total), destacada: false));
        tarjetas.Children.Add(Tarjeta("COBROS", corte.Cobros.ToString(Cultura), null, destacada: false));
        return new BlockUIContainer(tarjetas) { Margin = new Thickness(0, 0, 0, 12) };
    }

    private static Border Tarjeta(string etiqueta, string valor, string? nota, bool destacada)
    {
        var panel = new StackPanel();
        panel.Children.Add(Texto(etiqueta, 10, destacada ? Marca.TextoSuaveOscuro : Marca.TextoTicketSuave, negrita: true));
        panel.Children.Add(Texto(valor, 22, destacada ? Marca.Acento : Marca.TextoTicket, titulo: true));
        if (!string.IsNullOrEmpty(nota))
        {
            panel.Children.Add(Texto(nota, 10, Marca.TextoTicketSuave));
        }

        return new Border
        {
            Background = destacada ? Marca.Encabezado : Marca.Crema,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 8, 0),
            Child = panel
        };
    }

    private static void Titulo(FlowDocument doc, string texto) =>
        doc.Blocks.Add(new Paragraph(new Run(texto.ToUpper(Cultura)))
        {
            FontFamily = Marca.FuenteTitulo,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = Marca.Encabezado,
            BorderBrush = Marca.Acento,
            BorderThickness = new Thickness(5, 0, 0, 0),
            Padding = new Thickness(8, 0, 0, 0),
            Margin = new Thickness(0, 18, 0, 6)
        });

    private static Table Tabla(
        IReadOnlyList<(string Titulo, double Ancho, Alinear Alineacion)> columnas,
        IEnumerable<string[]> filas,
        string[]? total = null,
        bool encabezado = true,
        bool primeraEnNegrita = false,
        IReadOnlyList<string>? colores = null)
    {
        var tabla = new Table { CellSpacing = 0, BorderBrush = Marca.LineaCrema, BorderThickness = new Thickness(0, 1, 0, 1) };
        foreach (var columna in columnas)
        {
            tabla.Columns.Add(new TableColumn { Width = new GridLength(columna.Ancho, GridUnitType.Star) });
        }

        var grupo = new TableRowGroup();
        tabla.RowGroups.Add(grupo);

        if (encabezado)
        {
            var filaEncabezado = new TableRow { Background = Marca.Encabezado, Foreground = Marca.Crema, FontWeight = FontWeights.Bold };
            for (var i = 0; i < columnas.Count; i++)
            {
                filaEncabezado.Cells.Add(Celda(columnas[i].Titulo, columnas[i].Alineacion, null));
            }

            grupo.Rows.Add(filaEncabezado);
        }

        var numero = 0;
        foreach (var valores in filas)
        {
            var fila = new TableRow { Background = numero % 2 == 1 ? Marca.CremaClara : Brushes.White };
            for (var i = 0; i < columnas.Count; i++)
            {
                var color = i == 0 && colores is not null && numero < colores.Count ? colores[numero] : null;
                var celda = Celda(i < valores.Length ? valores[i] : string.Empty, columnas[i].Alineacion, color);
                if (primeraEnNegrita && i == 0)
                {
                    celda.FontWeight = FontWeights.SemiBold;
                }

                fila.Cells.Add(celda);
            }

            grupo.Rows.Add(fila);
            numero++;
        }

        if (total is not null)
        {
            var filaTotal = new TableRow { FontWeight = FontWeights.Bold, Background = Marca.Crema };
            for (var i = 0; i < columnas.Count; i++)
            {
                var celda = Celda(i < total.Length ? total[i] : string.Empty, columnas[i].Alineacion, null);
                celda.BorderBrush = Marca.TextoTicket;
                celda.BorderThickness = new Thickness(0, 1.5, 0, 0);
                filaTotal.Cells.Add(celda);
            }

            grupo.Rows.Add(filaTotal);
        }

        return tabla;
    }

    private static TableCell Celda(string texto, Alinear alineacion, string? colorHex)
    {
        var parrafo = new Paragraph
        {
            Margin = new Thickness(0),
            TextAlignment = alineacion == Alinear.Derecha ? TextAlignment.Right : TextAlignment.Left
        };

        if (!string.IsNullOrWhiteSpace(colorHex))
        {
            parrafo.Inlines.Add(new InlineUIContainer(new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = Marca.Pincel(colorHex),
                Margin = new Thickness(0, 0, 6, -1)
            })
            {
                BaselineAlignment = BaselineAlignment.Center
            });
        }

        parrafo.Inlines.Add(new Run(texto));
        return new TableCell(parrafo) { Padding = new Thickness(7, 4, 7, 4) };
    }

    private static Block Firmas()
    {
        var firmas = new Table { CellSpacing = 28, Margin = new Thickness(0, 46, 0, 0) };
        firmas.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        firmas.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var grupo = new TableRowGroup();
        var fila = new TableRow();
        fila.Cells.Add(Firma("Entregó"));
        fila.Cells.Add(Firma("Recibió"));
        grupo.Rows.Add(fila);
        firmas.RowGroups.Add(grupo);
        return firmas;
    }

    private static TableCell Firma(string texto)
    {
        var parrafo = new Paragraph
        {
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0),
            BorderBrush = Marca.TextoTicket,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 4, 0, 0),
            Foreground = Marca.TextoTicketSuave
        };
        parrafo.Inlines.Add(new Run(texto));
        return new TableCell(parrafo);
    }

    private static TextBlock Texto(string texto, double tamano, Brush color, bool titulo = false, bool negrita = false) =>
        new()
        {
            Text = texto,
            FontFamily = titulo ? Marca.FuenteTitulo : Marca.FuenteTexto,
            FontSize = tamano,
            FontWeight = titulo || negrita ? FontWeights.Bold : FontWeights.Normal,
            Foreground = color,
            TextWrapping = TextWrapping.Wrap
        };

    private static string Dinero(decimal valor) => valor.ToString("$#,0.00", Cultura);

    private static string? Porcentaje(decimal parte, decimal total) =>
        total == 0 ? null : $"{(parte / total).ToString("P0", Cultura)} del total";

    private static string Capital(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], Cultura) + texto[1..];
}
