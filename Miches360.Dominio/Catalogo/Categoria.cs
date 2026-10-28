using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Catalogo;

public sealed class Categoria : Entidad
{
    private readonly List<Producto> _productos = new();

    private Categoria()
    {
    }

    public Categoria(string nombre, string colorHex, EstacionPreparacion estacion, bool manejaTamanos, int orden)
    {
        Actualizar(nombre, colorHex, estacion, manejaTamanos, orden);
    }

    public string Nombre { get; private set; } = string.Empty;

    public string ColorHex { get; private set; } = "#33211A";

    public EstacionPreparacion Estacion { get; private set; }

    public bool ManejaTamanos { get; private set; }

    public int Orden { get; private set; }

    public bool Activa { get; private set; } = true;

    public bool Eliminada { get; private set; }

    public DateTime? EliminadaEn { get; private set; }

    public IReadOnlyCollection<Producto> Productos => _productos;

    public void Actualizar(string nombre, string colorHex, EstacionPreparacion estacion, bool manejaTamanos, int orden)
    {
        Nombre = Validar.Texto(nombre, "El nombre de la categoría", 60);
        ColorHex = Validar.Color(colorHex);
        Estacion = estacion;
        ManejaTamanos = manejaTamanos;
        Orden = orden;
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    public void Eliminar(DateTime ahora)
    {
        Activa = false;
        Eliminada = true;
        EliminadaEn = ahora;
    }
}
