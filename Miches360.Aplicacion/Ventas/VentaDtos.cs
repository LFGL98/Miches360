using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Ventas;

public sealed record RenglonSolicitud(Guid ProductoId, Tamano Tamano, int Cantidad, string? Nota);

public sealed record PagoSolicitud(MetodoPago Metodo, decimal Monto, string? Referencia);

public sealed record VentaDirectaSolicitud(
    IReadOnlyList<RenglonSolicitud> Renglones,
    IReadOnlyList<PagoSolicitud> Pagos,
    decimal EfectivoRecibido,
    string? Usuario);

public sealed record VentaRegistradaDto(
    Guid CuentaId,
    Guid CobroId,
    int Folio,
    decimal Total,
    decimal Cambio,
    DateTime Fecha);
