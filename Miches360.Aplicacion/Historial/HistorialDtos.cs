using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Historial;

public sealed record VentaResumenDto(
    Guid CuentaId,
    int Folio,
    DateTime Fecha,
    string Origen,
    EstadoCuenta Estado,
    decimal Total,
    decimal Pagado,
    string Metodos,
    int Articulos,
    string? MotivoCancelacion);

public sealed record TicketRenglonDto(
    int Cantidad,
    string Producto,
    string Tamano,
    decimal PrecioUnitario,
    decimal Importe,
    string? Persona,
    string? Nota);

public sealed record TicketPagoDto(MetodoPago Metodo, decimal Monto, string? Referencia);

public sealed record TicketDto(
    Guid CuentaId,
    int Folio,
    DateTime Fecha,
    string Origen,
    string? Personas,
    EstadoCuenta Estado,
    IReadOnlyList<TicketRenglonDto> Renglones,
    decimal Total,
    IReadOnlyList<TicketPagoDto> Pagos,
    decimal EfectivoRecibido,
    decimal Cambio,
    decimal Pendiente,
    string? Usuario,
    DateTime? CanceladaEn,
    string? MotivoCancelacion,
    string? CanceladaPor)
{
    public bool Cancelada => Estado == EstadoCuenta.Cancelada;

    public bool SinCobrar => Pagos.Count == 0;
}
