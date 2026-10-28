using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Ventas;

namespace Miches360.Aplicacion.Ventas;

public interface IVentaServicio
{
    Task<VentaRegistradaDto> RegistrarVentaDirectaAsync(VentaDirectaSolicitud solicitud, CancellationToken cancellationToken = default);
}

internal sealed class VentaServicio : IVentaServicio
{
    private readonly IAppDbContextFactory _fabrica;
    private readonly IReloj _reloj;
    private readonly INotificadorCambios _notificador;

    public VentaServicio(IAppDbContextFactory fabrica, IReloj reloj, INotificadorCambios notificador)
    {
        _fabrica = fabrica;
        _reloj = reloj;
        _notificador = notificador;
    }

    public async Task<VentaRegistradaDto> RegistrarVentaDirectaAsync(VentaDirectaSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        if (solicitud.Renglones.Count == 0)
        {
            throw new ReglaNegocioException("El ticket está vacío.");
        }

        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var ids = solicitud.Renglones.Select(r => r.ProductoId).Distinct().ToList();
        var productos = await db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Precios)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var folio = (await db.Cuentas.MaxAsync(c => (int?)c.Folio, cancellationToken) ?? 0) + 1;
        var ahora = _reloj.Ahora;

        var cuenta = Cuenta.ParaLlevar(folio, solicitud.Usuario, ahora);
        var subcuenta = cuenta.Subcuentas.First();

        foreach (var renglon in solicitud.Renglones)
        {
            if (!productos.TryGetValue(renglon.ProductoId, out var producto))
            {
                throw new ReglaNegocioException("Uno de los productos ya no existe. Recarga el catálogo.");
            }

            cuenta.AgregarRenglon(subcuenta.Id, producto, renglon.Tamano, renglon.Cantidad, renglon.Nota, ahora);
        }

        var pagos = solicitud.Pagos
            .Select(p => new PagoSolicitado(p.Metodo, p.Monto, p.Referencia))
            .ToList();

        var cobro = cuenta.Cobrar(new[] { subcuenta.Id }, pagos, solicitud.EfectivoRecibido, solicitud.Usuario, ahora);

        db.Cuentas.Add(cuenta);
        await db.SaveChangesAsync(cancellationToken);

        var quien = TextoActividad.Quien(solicitud.Usuario);
        _notificador.RegistrarActividad(new Actividad(
            TipoActividad.Cobro,
            cuenta.Id,
            quien,
            $"{quien} cobró venta para llevar · {CentralCambios.Dinero(cobro.Total)}",
            $"Folio {cuenta.Folio} · {TextoActividad.FormaDePago(solicitud.Pagos.Select(p => (p.Metodo, p.Monto)))}",
            ahora));

        return new VentaRegistradaDto(cuenta.Id, cobro.Id, cuenta.Folio, cobro.Total, cobro.Cambio, ahora);
    }
}
