using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Ventas;

public sealed class Subcuenta : Entidad
{
    private readonly List<Renglon> _renglones = new();

    private Subcuenta()
    {
    }

    internal Subcuenta(Guid cuentaId, string nombre, int orden)
    {
        CuentaId = cuentaId;
        Nombre = Validar.Texto(nombre, "El nombre de la persona", 40);
        Orden = orden;
    }

    public Guid CuentaId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public int Orden { get; private set; }

    public Guid? CobroId { get; private set; }

    public IReadOnlyCollection<Renglon> Renglones => _renglones;

    public decimal Total => _renglones.Sum(r => r.Importe);

    public bool Pagada => CobroId.HasValue;

    internal void Renombrar(string nombre)
    {
        Nombre = Validar.Texto(nombre, "El nombre de la persona", 40);
    }

    internal Renglon AgregarRenglon(
        Guid productoId,
        string nombreProducto,
        Tamano tamano,
        decimal precioUnitario,
        int cantidad,
        string? nota,
        EstacionPreparacion estacion,
        DateTime ahora)
    {
        var renglon = new Renglon(Id, productoId, nombreProducto, tamano, precioUnitario, cantidad, nota, estacion, ahora);
        _renglones.Add(renglon);
        return renglon;
    }

    internal void QuitarRenglon(Guid renglonId)
    {
        _renglones.RemoveAll(r => r.Id == renglonId);
    }

    internal Renglon ExtraerRenglon(Guid renglonId)
    {
        var renglon = _renglones.First(r => r.Id == renglonId);
        _renglones.Remove(renglon);
        return renglon;
    }

    internal void RecibirRenglon(Renglon renglon)
    {
        renglon.AsignarSubcuenta(Id);
        _renglones.Add(renglon);
    }

    internal void MarcarPagada(Guid cobroId) => CobroId = cobroId;
}
