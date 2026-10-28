using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Catalogo;

public sealed class PrecioProducto : Entidad
{
    private PrecioProducto()
    {
    }

    internal PrecioProducto(Guid productoId, Tamano tamano, decimal precio)
    {
        ProductoId = productoId;
        Tamano = tamano;
        Precio = Validar.Dinero(precio);
    }

    public Guid ProductoId { get; private set; }

    public Tamano Tamano { get; private set; }

    public decimal Precio { get; private set; }

    internal void CambiarPrecio(decimal precio) => Precio = Validar.Dinero(precio);
}
