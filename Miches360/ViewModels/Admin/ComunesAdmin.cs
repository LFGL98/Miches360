using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Miches360.Aplicacion.Catalogo;
using Miches360.Dominio.Enums;

namespace Miches360.ViewModels;

public sealed partial class OpcionSeleccionable<T> : ObservableObject
{
    public OpcionSeleccionable(T valor, string texto, string? colorHex = null)
    {
        Valor = valor;
        Texto = texto;
        ColorHex = colorHex ?? "#3A2620";
    }

    public T Valor { get; }

    public string Texto { get; }

    public string ColorHex { get; }

    [ObservableProperty]
    private bool _estaSeleccionada;
}

public static class Opciones
{
    public static readonly string[] Paleta =
    {
        "#C66A43", "#8F311A", "#B5452A", "#7A4A2A", "#9C6B3A", "#E07B39",
        "#E8B13A", "#E8D27A", "#F2D9AE", "#D9485F", "#3E8EDE", "#8FB9A8"
    };

    public static List<OpcionSeleccionable<string>> CrearColores() =>
        Paleta.Select(c => new OpcionSeleccionable<string>(c, c, c)).ToList();

    public static string TextoEstacion(EstacionPreparacion estacion) => estacion switch
    {
        EstacionPreparacion.Barra => "Barra",
        EstacionPreparacion.Cocina => "Cocina",
        _ => "Ya listo"
    };

    public static void Marcar<T>(IEnumerable<OpcionSeleccionable<T>> opciones, T valor)
    {
        foreach (var opcion in opciones)
        {
            opcion.EstaSeleccionada = EqualityComparer<T>.Default.Equals(opcion.Valor, valor);
        }
    }

    public static bool IntentarLeerPrecio(string? texto, out decimal? precio)
    {
        precio = null;
        var limpio = (texto ?? string.Empty).Replace("$", string.Empty).Replace(",", string.Empty).Trim();
        if (limpio.Length == 0)
        {
            return true;
        }

        if (decimal.TryParse(limpio, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) && valor > 0)
        {
            precio = valor;
            return true;
        }

        return false;
    }

    public static string TextoPrecio(decimal? precio) =>
        precio is decimal valor ? valor.ToString("0.##", CultureInfo.InvariantCulture) : string.Empty;
}

public sealed partial class CategoriaAdminItem : ObservableObject
{
    public CategoriaAdminItem(CategoriaAdminDto categoria)
    {
        Categoria = categoria;
    }

    public CategoriaAdminDto Categoria { get; }

    public string Detalle =>
        $"{Opciones.TextoEstacion(Categoria.Estacion)} · {(Categoria.ManejaTamanos ? "Con tamaños" : "Precio único")} · {Categoria.Productos} productos";

    [ObservableProperty]
    private bool _estaSeleccionada;
}

public sealed partial class ProductoAdminItem : ObservableObject
{
    public ProductoAdminItem(ProductoAdminDto producto)
    {
        Producto = producto;
    }

    public ProductoAdminDto Producto { get; }

    public string TextoPrecios
    {
        get
        {
            if (Producto.PrecioUnico is decimal unico)
            {
                return Formato.Dinero(unico);
            }

            static string P(decimal? p) => p is decimal v ? Formato.Dinero(v) : "—";
            return $"Ch {P(Producto.PrecioChico)} · Med {P(Producto.PrecioMediano)} · L {P(Producto.PrecioLitro)}";
        }
    }

    public string Estado => !Producto.Activo ? "FUERA DEL CATÁLOGO" : !Producto.Disponible ? "AGOTADO" : string.Empty;

    [ObservableProperty]
    private bool _estaSeleccionado;
}

public sealed partial class MesaAdminItem : ObservableObject
{
    public MesaAdminItem(MesaAdminDto mesa)
    {
        Mesa = mesa;
    }

    public MesaAdminDto Mesa { get; }

    [ObservableProperty]
    private bool _estaSeleccionada;
}
