using Miches360.Aplicacion.Ventas;
using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Mesas;

public sealed record MesaEstadoDto(
    Guid MesaId,
    string Nombre,
    Guid? CuentaId,
    int? Folio,
    TipoCuenta? Tipo,
    int Personas,
    int PersonasPagadas,
    DateTime? AbiertaEn,
    decimal Total,
    decimal Pendiente)
{
    public bool Ocupada => CuentaId is not null;
}

public sealed record RenglonCuentaDto(
    Guid Id,
    Guid ProductoId,
    string Producto,
    Tamano Tamano,
    int Cantidad,
    decimal PrecioUnitario,
    decimal Importe,
    string? Nota,
    EstacionPreparacion Estacion);

public sealed record PersonaCuentaDto(
    Guid SubcuentaId,
    string Nombre,
    int Orden,
    bool Pagada,
    decimal Total,
    IReadOnlyList<RenglonCuentaDto> Renglones);

public sealed record CuentaDetalleDto(
    Guid CuentaId,
    int Folio,
    Guid? MesaId,
    string MesaNombre,
    TipoCuenta Tipo,
    DateTime AbiertaEn,
    IReadOnlyList<PersonaCuentaDto> Personas,
    decimal Total,
    decimal Pendiente)
{
    public decimal Pagado => Total - Pendiente;
}

public sealed record RenglonMesaSolicitud(Guid SubcuentaId, Guid ProductoId, Tamano Tamano, int Cantidad, string? Nota);

public sealed record CobroMesaSolicitud(
    Guid CuentaId,
    IReadOnlyList<Guid> SubcuentaIds,
    IReadOnlyList<PagoSolicitud> Pagos,
    decimal EfectivoRecibido,
    string? Usuario);

public sealed record CobroRegistradoDto(Guid CuentaId, Guid CobroId, int Folio, decimal Total, decimal Cambio, bool CuentaCerrada);
