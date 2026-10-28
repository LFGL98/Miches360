using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Ventas;

public sealed class PagoCobro : Entidad
{
    private PagoCobro()
    {
    }

    internal PagoCobro(Guid cobroId, MetodoPago metodo, decimal monto, string? referencia)
    {
        CobroId = cobroId;
        Metodo = metodo;
        Monto = monto;
        Referencia = Validar.TextoOpcional(referencia, 60);
    }

    public Guid CobroId { get; private set; }

    public MetodoPago Metodo { get; private set; }

    public decimal Monto { get; private set; }

    public string? Referencia { get; private set; }
}
