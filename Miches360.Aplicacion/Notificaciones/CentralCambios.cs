using System.Globalization;

namespace Miches360.Aplicacion.Notificaciones;

public enum TipoCambio
{
    Mesas,

    Catalogo,

    Aviso,

    CuentaPedida,

    OrdenEnviada,

    CobroMesero,

    Actividad
}

public enum TipoActividad
{
    MesaAbierta,
    OrdenEnviada,
    CuentaPedida,
    Cobro,
    VentaCancelada
}

public sealed record Actividad(TipoActividad Tipo, Guid CuentaId, string Quien, string Titulo, string? Detalle, DateTime Hora);

public sealed record CuentaPedida(Guid CuentaId, string Mesa, decimal Pendiente, int PersonasPorPagar, string Mesero, DateTime Hora);

public sealed record ProductoEnviado(int Cantidad, string Producto, string? Tamano, string? Persona, string? Nota, decimal Importe);

public sealed record OrdenEnviada(Guid CuentaId, string Mesa, string Mesero, DateTime Hora, IReadOnlyList<ProductoEnviado> Productos)
{
    public decimal Total => Productos.Sum(p => p.Importe);

    public int Piezas => Productos.Sum(p => p.Cantidad);
}

public sealed record CambioRealizado(
    TipoCambio Tipo,
    string? Mensaje = null,
    CuentaPedida? Cuenta = null,
    OrdenEnviada? Orden = null,
    Actividad? Actividad = null);

public interface INotificadorCambios
{
    void Notificar(TipoCambio tipo, string? mensaje = null);

    void AvisarCuentaPedida(CuentaPedida cuenta);

    void AvisarOrdenEnviada(OrdenEnviada orden);

    void RegistrarActividad(Actividad actividad);
}

public sealed class CentralCambios : INotificadorCambios
{
    public event Action<CambioRealizado>? Cambio;

    public void Notificar(TipoCambio tipo, string? mensaje = null) =>
        Publicar(new CambioRealizado(tipo, mensaje));

    public void AvisarCuentaPedida(CuentaPedida cuenta)
    {
        Publicar(new CambioRealizado(TipoCambio.CuentaPedida, $"{cuenta.Mesa} pide la cuenta", cuenta));
        RegistrarActividad(new Actividad(
            TipoActividad.CuentaPedida,
            cuenta.CuentaId,
            cuenta.Mesero,
            $"{cuenta.Mesero}: {cuenta.Mesa} pide la cuenta",
            $"Pendiente {Dinero(cuenta.Pendiente)}",
            cuenta.Hora));
    }

    public void AvisarOrdenEnviada(OrdenEnviada orden)
    {
        Publicar(new CambioRealizado(TipoCambio.OrdenEnviada, $"{orden.Mesa}: {orden.Mesero} envió {orden.Piezas} producto(s)", Orden: orden));
        var piezas = orden.Piezas;
        RegistrarActividad(new Actividad(
            TipoActividad.OrdenEnviada,
            orden.CuentaId,
            orden.Mesero,
            $"{orden.Mesero} envió la orden de {orden.Mesa}",
            $"{piezas} producto{(piezas == 1 ? string.Empty : "s")} · {Dinero(orden.Total)}",
            orden.Hora));
    }

    public void RegistrarActividad(Actividad actividad) =>
        Publicar(new CambioRealizado(TipoCambio.Actividad, actividad.Titulo, Actividad: actividad));

    public static string Dinero(decimal valor) =>
        valor.ToString(valor % 1 == 0 ? "$#,0" : "$#,0.00", CultureInfo.GetCultureInfo("es-MX"));

    private void Publicar(CambioRealizado cambio)
    {
        var manejadores = Cambio;
        if (manejadores is null)
        {
            return;
        }

        foreach (var manejador in manejadores.GetInvocationList().Cast<Action<CambioRealizado>>())
        {
            try
            {
                manejador(cambio);
            }
            catch
            {
            }
        }
    }
}
