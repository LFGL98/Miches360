using Miches360.Aplicacion.Ventas;

namespace Miches360.ViewModels;

public enum MetodoCobro
{
    Efectivo,
    Transferencia,
    Mixto
}

public sealed record LineaCobro(int Cantidad, string Nombre, string Detalle, decimal Importe)
{
    public string Texto => $"{Cantidad} × {Nombre}";
}

public sealed record CobroSolicitud(IReadOnlyList<PagoSolicitud> Pagos, decimal EfectivoRecibido, string? Usuario);

public sealed record CobroResultado(Guid CuentaId, Guid CobroId, int Folio, decimal Total, decimal Cambio, bool CuentaCerrada)
{
    public TicketCobrado Ticket => new(CuentaId, CobroId, Folio);
}

public sealed record TicketCobrado(Guid CuentaId, Guid CobroId, int Folio);

public sealed record CobroContexto(
    string Titulo,
    IReadOnlyList<LineaCobro> Lineas,
    decimal Total,
    MetodoCobro MetodoInicial,
    Func<CobroSolicitud, Task<CobroResultado>> Cobrar,
    Action<CobroResultado> AlCobrar,
    Action AlVolver);
