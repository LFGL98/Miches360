namespace Miches360.Aplicacion.Corte;

public sealed record CorteCategoriaDto(string Categoria, string ColorHex, int Cantidad, decimal Importe, decimal Porcentaje);

public sealed record CorteProductoDto(string Producto, string Tamano, int Cantidad, decimal Importe);

public sealed record CorteCancelacionDto(int Folio, DateTime CanceladaEn, string Origen, decimal Total, string Motivo, string? Usuario);

public sealed record CorteDiaDto(DateTime Dia, int Cobros, decimal Total, decimal Efectivo, decimal Transferencia);

public sealed record ArqueoDto(
    DateTime Dia,
    DateTime RealizadoEn,
    string? Usuario,
    decimal Fondo,
    decimal VentasEfectivo,
    decimal Esperado,
    decimal Contado,
    decimal Diferencia,
    string? Notas);

public sealed record CorteDto(
    DateTime Desde,
    DateTime Hasta,
    int Cobros,
    decimal Total,
    decimal Efectivo,
    decimal Transferencia,
    decimal CambioEntregado,
    decimal TicketPromedio,
    IReadOnlyList<CorteCategoriaDto> PorCategoria,
    IReadOnlyList<CorteProductoDto> PorProducto,
    IReadOnlyList<CorteDiaDto> PorDia,
    int CuentasAbiertas,
    decimal PendienteAbierto,
    IReadOnlyList<ArqueoDto> Arqueos,
    IReadOnlyList<CorteCancelacionDto> Cancelaciones)
{
    public decimal ImporteCancelado => Cancelaciones.Sum(c => c.Total);
}
