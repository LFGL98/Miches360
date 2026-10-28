using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Ventas;

public sealed class Renglon : Entidad
{
    private Renglon()
    {
    }

    internal Renglon(
        Guid subcuentaId,
        Guid productoId,
        string nombreProducto,
        Tamano tamano,
        decimal precioUnitario,
        int cantidad,
        string? nota,
        EstacionPreparacion estacion,
        DateTime ahora)
    {
        if (cantidad < 1)
        {
            throw new ReglaNegocioException("La cantidad debe ser al menos 1.");
        }

        SubcuentaId = subcuentaId;
        ProductoId = productoId;
        NombreProducto = Validar.Texto(nombreProducto, "El nombre del producto", 80);
        Tamano = tamano;
        PrecioUnitario = Validar.Dinero(precioUnitario);
        Cantidad = cantidad;
        Nota = Validar.TextoOpcional(nota, 120);
        Estacion = estacion;
        CreadoEn = ahora;
    }

    public Guid SubcuentaId { get; private set; }

    public Guid ProductoId { get; private set; }

    public string NombreProducto { get; private set; } = string.Empty;

    public Tamano Tamano { get; private set; }

    public decimal PrecioUnitario { get; private set; }

    public int Cantidad { get; private set; }

    public string? Nota { get; private set; }

    public EstacionPreparacion Estacion { get; private set; }

    public DateTime CreadoEn { get; private set; }

    public DateTime? EnviadoEn { get; private set; }

    public decimal Importe => PrecioUnitario * Cantidad;

    public void MarcarEnviado(DateTime ahora) => EnviadoEn ??= ahora;

    internal void AsignarSubcuenta(Guid subcuentaId) => SubcuentaId = subcuentaId;
}
