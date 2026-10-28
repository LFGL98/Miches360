using CommunityToolkit.Mvvm.ComponentModel;
using Miches360.Aplicacion.Catalogo;
using Miches360.Dominio.Enums;

namespace Miches360.ViewModels;

public sealed partial class SeccionItem : ObservableObject
{
    public SeccionItem(string clave, string texto)
    {
        Clave = clave;
        Texto = texto;
    }

    public string Clave { get; }

    public string Texto { get; }

    [ObservableProperty]
    private bool _estaSeleccionada;
}

public sealed partial class CategoriaItem : ObservableObject
{
    public CategoriaItem(CategoriaDto categoria)
    {
        Id = categoria.Id;
        Nombre = categoria.Nombre;
        ColorHex = categoria.ColorHex;
        ManejaTamanos = categoria.ManejaTamanos;
    }

    public Guid Id { get; }

    public string Nombre { get; }

    public string ColorHex { get; }

    public bool ManejaTamanos { get; }

    [ObservableProperty]
    private bool _estaSeleccionada;
}

public sealed partial class OpcionTamano : ObservableObject
{
    public OpcionTamano(Tamano tamano, string texto)
    {
        Tamano = tamano;
        Texto = texto;
    }

    public Tamano Tamano { get; }

    public string Texto { get; }

    [ObservableProperty]
    private bool _estaSeleccionado;
}

public sealed partial class ProductoItem : ObservableObject
{
    private readonly IReadOnlyDictionary<Tamano, decimal> _precios;

    public ProductoItem(ProductoDto producto)
    {
        Id = producto.Id;
        Nombre = producto.Nombre;
        ColorHex = producto.ColorHex;
        _precios = producto.Precios;
    }

    public Guid Id { get; }

    public string Nombre { get; }

    public string ColorHex { get; }

    [ObservableProperty]
    private decimal? _precioActual;

    [ObservableProperty]
    private bool _habilitado;

    [ObservableProperty]
    private string _textoPrecio = string.Empty;

    public void AplicarTamano(Tamano tamano)
    {
        if (_precios.TryGetValue(tamano, out var precio))
        {
            PrecioActual = precio;
            Habilitado = true;
            TextoPrecio = Formato.Dinero(precio);
        }
        else
        {
            PrecioActual = null;
            Habilitado = false;
            TextoPrecio = "No hay en este tamaño";
        }
    }
}

public sealed partial class LineaTicket : ObservableObject
{
    public LineaTicket(Guid productoId, string nombre, Tamano tamano, decimal precioUnitario)
    {
        ProductoId = productoId;
        Nombre = nombre;
        Tamano = tamano;
        PrecioUnitario = precioUnitario;
    }

    public Guid ProductoId { get; }

    public string Nombre { get; }

    public Tamano Tamano { get; }

    public decimal PrecioUnitario { get; }

    public string Detalle => Tamano == Tamano.Unico ? string.Empty : Tamano.ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Importe))]
    private int _cantidad = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoNota), nameof(TieneNota))]
    private string? _nota;

    public bool TieneNota => !string.IsNullOrWhiteSpace(Nota);

    public string TextoNota => TieneNota ? $"Nota: {Nota}" : "+ Agregar nota";

    public decimal Importe => PrecioUnitario * Cantidad;
}

public static class Formato
{
    private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.GetCultureInfo("es-MX");

    public static string Dinero(decimal valor) =>
        valor.ToString(valor % 1 == 0 ? "$#,0" : "$#,0.00", Cultura);
}
