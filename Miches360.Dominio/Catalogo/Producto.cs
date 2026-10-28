using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Catalogo;

public sealed class Producto : Entidad
{
    private readonly List<PrecioProducto> _precios = new();

    private Producto()
    {
    }

    public Producto(Guid categoriaId, string nombre, string colorHex, int orden, EstacionPreparacion? estacionPropia = null)
    {
        CategoriaId = categoriaId;
        Actualizar(nombre, colorHex, orden, estacionPropia);
    }

    public Guid CategoriaId { get; private set; }

    public Categoria? Categoria { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string ColorHex { get; private set; } = "#33211A";

    public int Orden { get; private set; }

    public EstacionPreparacion? EstacionPropia { get; private set; }

    public bool Disponible { get; private set; } = true;

    public bool Activo { get; private set; } = true;

    public bool Eliminado { get; private set; }

    public DateTime? EliminadoEn { get; private set; }

    public IReadOnlyCollection<PrecioProducto> Precios => _precios;

    public EstacionPreparacion EstacionEfectiva =>
        EstacionPropia
        ?? Categoria?.Estacion
        ?? throw new InvalidOperationException($"Carga la categoría de '{Nombre}' para conocer su estación.");

    public void Actualizar(string nombre, string colorHex, int orden, EstacionPreparacion? estacionPropia)
    {
        Nombre = Validar.Texto(nombre, "El nombre del producto", 80);
        ColorHex = Validar.Color(colorHex);
        Orden = orden;
        EstacionPropia = estacionPropia;
    }

    public void CambiarCategoria(Guid categoriaId)
    {
        CategoriaId = categoriaId;
        Categoria = null;
    }

    public void FijarPrecio(Tamano tamano, decimal precio)
    {
        if (precio <= 0)
        {
            throw new ReglaNegocioException("El precio debe ser mayor a cero.");
        }

        var existente = _precios.FirstOrDefault(p => p.Tamano == tamano);
        if (existente is null)
        {
            _precios.Add(new PrecioProducto(Id, tamano, precio));
        }
        else
        {
            existente.CambiarPrecio(precio);
        }
    }

    public void QuitarPrecio(Tamano tamano)
    {
        _precios.RemoveAll(p => p.Tamano == tamano);
    }

    public decimal? ObtenerPrecio(Tamano tamano) =>
        _precios.FirstOrDefault(p => p.Tamano == tamano)?.Precio;

    public void MarcarDisponible(bool disponible) => Disponible = disponible;

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    public void Eliminar(DateTime ahora)
    {
        Activo = false;
        Eliminado = true;
        EliminadoEn = ahora;
    }
}
