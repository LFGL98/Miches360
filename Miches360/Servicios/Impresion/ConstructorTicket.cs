using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Miches360.Servicios.Impresion;

public sealed record DatosTicket(string Nombre, string Direccion, string Telefono, string Pie, string Caja, bool Logo);

public sealed record VistaPreviaTicket(Miches360.Aplicacion.Historial.TicketDto Ticket, DatosTicket Datos);

internal sealed class ConstructorTicket
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    private readonly StackPanel _panel;
    private readonly double _base;

    public ConstructorTicket(double ancho)
    {
        Ancho = ancho;
        _base = ancho < 220 ? 10.5 : 12;
        _panel = new StackPanel { Width = ancho };
    }

    public double Ancho { get; }

    public static string Dinero(decimal valor) => valor.ToString("$#,0.00", Cultura);

    public ConstructorTicket Encabezado(DatosTicket datos)
    {
        if (datos.Logo && Marca.LogoTermico is { } logo)
        {
            var lado = Math.Round(Ancho * 0.38);
            _panel.Children.Add(new Ellipse
            {
                Width = lado,
                Height = lado,
                Fill = new ImageBrush(logo) { Stretch = Stretch.UniformToFill },
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        _panel.Children.Add(Titulo(datos.Nombre.ToUpper(Cultura), _base * 2.3, TextAlignment.Center));
        Centrado(datos.Direccion, 0.9);
        Centrado(string.IsNullOrWhiteSpace(datos.Telefono) ? null : $"Tel. {datos.Telefono}", 0.9);
        return Espacio(4);
    }

    public ConstructorTicket Centrado(string? texto, double escala = 1, bool negrita = false)
    {
        if (!string.IsNullOrWhiteSpace(texto))
        {
            _panel.Children.Add(Texto(texto, escala, negrita, TextAlignment.Center));
        }

        return this;
    }

    public ConstructorTicket Parrafo(string? texto, double escala = 1, bool negrita = false)
    {
        if (!string.IsNullOrWhiteSpace(texto))
        {
            _panel.Children.Add(Texto(texto, escala, negrita, TextAlignment.Left));
        }

        return this;
    }

    public ConstructorTicket TituloCentrado(string texto, double escala = 1.3)
    {
        _panel.Children.Add(Titulo(texto, _base * escala, TextAlignment.Center));
        return this;
    }

    public ConstructorTicket Fila(string izquierda, string derecha, double escala = 1, bool negrita = false)
    {
        var fila = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var izq = Texto(izquierda, escala, negrita, TextAlignment.Left);
        var der = Texto(derecha, escala, negrita, TextAlignment.Right);
        der.Margin = new Thickness(6, 0, 0, 0);
        der.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(der, 1);

        fila.Children.Add(izq);
        fila.Children.Add(der);
        _panel.Children.Add(fila);
        return this;
    }

    public ConstructorTicket Recuadro(string izquierda, string derecha)
    {
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var izq = Titulo(izquierda, _base * 1.7, TextAlignment.Left);
        izq.VerticalAlignment = VerticalAlignment.Center;
        var der = Texto(derecha, 0.9, true, TextAlignment.Right);
        der.VerticalAlignment = VerticalAlignment.Center;
        der.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(der, 1);
        fila.Children.Add(izq);
        fila.Children.Add(der);

        _panel.Children.Add(new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 4, 0, 4),
            Child = fila
        });
        return this;
    }

    public ConstructorTicket Banda(string texto)
    {
        var titulo = Titulo(texto, _base * 1.15, TextAlignment.Left);
        titulo.Foreground = Brushes.White;

        _panel.Children.Add(new Border
        {
            Background = Brushes.Black,
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(0, 8, 0, 5),
            Child = titulo
        });
        return this;
    }

    public ConstructorTicket TotalDestacado(string etiqueta, string monto)
    {
        var fila = new Grid();
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var izq = Titulo(etiqueta, _base * 1.3, TextAlignment.Left);
        izq.Foreground = Brushes.White;
        izq.VerticalAlignment = VerticalAlignment.Center;
        var der = Titulo(monto, _base * 2.2, TextAlignment.Right);
        der.Foreground = Brushes.White;
        der.TextWrapping = TextWrapping.NoWrap;
        Grid.SetColumn(der, 1);
        fila.Children.Add(izq);
        fila.Children.Add(der);

        _panel.Children.Add(new Border
        {
            Background = Brushes.Black,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 8, 0, 6),
            Child = fila
        });
        return this;
    }

    public ConstructorTicket Producto(int cantidad, string nombre, string? detalle, decimal importe)
    {
        var fila = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_base * 2.4) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fila.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var cant = Titulo(cantidad.ToString(Cultura), _base * 1.3, TextAlignment.Left);
        var nom = new StackPanel();
        nom.Children.Add(Texto(nombre, 1, true, TextAlignment.Left));
        if (!string.IsNullOrWhiteSpace(detalle))
        {
            nom.Children.Add(Texto(detalle, 0.85, false, TextAlignment.Left));
        }

        var imp = Texto(Dinero(importe), 1, true, TextAlignment.Right);
        imp.Margin = new Thickness(6, 0, 0, 0);
        imp.TextWrapping = TextWrapping.NoWrap;

        Grid.SetColumn(nom, 1);
        Grid.SetColumn(imp, 2);
        fila.Children.Add(cant);
        fila.Children.Add(nom);
        fila.Children.Add(imp);
        _panel.Children.Add(fila);
        return this;
    }

    public ConstructorTicket Separador()
    {
        _panel.Children.Add(new Line
        {
            X1 = 0,
            X2 = Ancho,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Margin = new Thickness(0, 5, 0, 5),
            SnapsToDevicePixels = true
        });
        return this;
    }

    public ConstructorTicket Espacio(double alto = 6)
    {
        _panel.Children.Add(new Border { Height = alto });
        return this;
    }

    public ConstructorTicket Pie(string? mensaje)
    {
        Separador();
        if (!string.IsNullOrWhiteSpace(mensaje))
        {
            _panel.Children.Add(Titulo(mensaje, _base * 1.4, TextAlignment.Center));
        }

        return this;
    }

    public FrameworkElement Construir() =>
        new Border
        {
            Background = Brushes.White,
            Padding = new Thickness(0, 0, 0, 28),
            Child = _panel,
            Width = Ancho
        };

    private TextBlock Texto(string texto, double escala, bool negrita, TextAlignment alineacion) =>
        new()
        {
            Text = texto,
            FontFamily = Marca.FuenteTexto,
            FontSize = _base * escala,
            FontWeight = negrita ? FontWeights.Bold : FontWeights.Normal,
            Foreground = Brushes.Black,
            TextAlignment = alineacion,
            TextWrapping = TextWrapping.Wrap
        };

    private static TextBlock Titulo(string texto, double tamano, TextAlignment alineacion) =>
        new()
        {
            Text = texto,
            FontFamily = Marca.FuenteTitulo,
            FontSize = tamano,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.Black,
            TextAlignment = alineacion,
            TextWrapping = TextWrapping.Wrap
        };
}
