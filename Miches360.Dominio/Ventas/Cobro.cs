using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Ventas;

public sealed class Cobro : Entidad
{
    private readonly List<PagoCobro> _pagos = new();

    private Cobro()
    {
    }

    public Guid CuentaId { get; private set; }

    public DateTime Fecha { get; private set; }

    public decimal Total { get; private set; }

    public decimal EfectivoRecibido { get; private set; }

    public decimal Cambio { get; private set; }

    public string? Usuario { get; private set; }

    public IReadOnlyCollection<PagoCobro> Pagos => _pagos;

    internal static Cobro Crear(
        Guid cuentaId,
        decimal total,
        IReadOnlyCollection<PagoSolicitado> pagos,
        decimal efectivoRecibido,
        string? usuario,
        DateTime ahora)
    {
        if (pagos.Count == 0)
        {
            throw new ReglaNegocioException("Indica cómo se va a pagar.");
        }

        if (pagos.Any(p => p.Monto <= 0))
        {
            throw new ReglaNegocioException("Cada pago debe ser mayor a cero.");
        }

        total = Validar.Dinero(total);
        var suma = Validar.Dinero(pagos.Sum(p => p.Monto));
        if (suma != total)
        {
            throw new ReglaNegocioException($"Los pagos suman {suma:$#,0.00} y el total es {total:$#,0.00}.");
        }

        var montoEfectivo = Validar.Dinero(pagos.Where(p => p.Metodo == MetodoPago.Efectivo).Sum(p => p.Monto));
        efectivoRecibido = Validar.Dinero(efectivoRecibido);

        if (montoEfectivo > 0 && efectivoRecibido < montoEfectivo)
        {
            throw new ReglaNegocioException($"Faltan {montoEfectivo - efectivoRecibido:$#,0.00} en efectivo.");
        }

        var cobro = new Cobro
        {
            CuentaId = cuentaId,
            Fecha = ahora,
            Total = total,
            EfectivoRecibido = montoEfectivo > 0 ? efectivoRecibido : 0,
            Cambio = montoEfectivo > 0 ? efectivoRecibido - montoEfectivo : 0,
            Usuario = Validar.TextoOpcional(usuario, 60)
        };

        foreach (var pago in pagos)
        {
            cobro._pagos.Add(new PagoCobro(cobro.Id, pago.Metodo, Validar.Dinero(pago.Monto), pago.Referencia));
        }

        return cobro;
    }
}
