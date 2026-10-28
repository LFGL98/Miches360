using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Notificaciones;

internal static class TextoActividad
{
    public static string Quien(string? usuario) => string.IsNullOrWhiteSpace(usuario) ? "Caja" : usuario.Trim();

    public static string FormaDePago(IEnumerable<(MetodoPago Metodo, decimal Monto)> pagos)
    {
        var lista = pagos.ToList();
        var efectivo = lista.Where(p => p.Metodo == MetodoPago.Efectivo).Sum(p => p.Monto);
        var transferencia = lista.Where(p => p.Metodo == MetodoPago.Transferencia).Sum(p => p.Monto);
        return (efectivo > 0, transferencia > 0) switch
        {
            (true, true) => $"Efectivo {CentralCambios.Dinero(efectivo)} + transferencia {CentralCambios.Dinero(transferencia)}",
            (true, false) => $"Efectivo {CentralCambios.Dinero(efectivo)}",
            _ => $"Transferencia {CentralCambios.Dinero(transferencia)}"
        };
    }
}
