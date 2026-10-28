using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Ventas;

public sealed record PagoSolicitado(MetodoPago Metodo, decimal Monto, string? Referencia);
