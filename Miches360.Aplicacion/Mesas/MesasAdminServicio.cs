using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Aplicacion.Catalogo;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Dominio.Mesas;

namespace Miches360.Aplicacion.Mesas;

public interface IMesasAdminServicio
{
    Task<IReadOnlyList<MesaAdminDto>> ObtenerMesasAsync(CancellationToken cancellationToken = default);

    Task<Guid> GuardarMesaAsync(Guid? mesaId, string nombre, CancellationToken cancellationToken = default);

    Task CambiarEstadoMesaAsync(Guid mesaId, bool activa, CancellationToken cancellationToken = default);

    Task EliminarMesaAsync(Guid mesaId, CancellationToken cancellationToken = default);
}

internal sealed class MesasAdminServicio : IMesasAdminServicio
{
    private readonly IAppDbContextFactory _fabrica;
    private readonly IReloj _reloj;
    private readonly INotificadorCambios _notificador;

    public MesasAdminServicio(IAppDbContextFactory fabrica, IReloj reloj, INotificadorCambios notificador)
    {
        _fabrica = fabrica;
        _reloj = reloj;
        _notificador = notificador;
    }

    public async Task<IReadOnlyList<MesaAdminDto>> ObtenerMesasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        return await db.Mesas
            .AsNoTracking()
            .OrderBy(m => m.Orden)
            .ThenBy(m => m.Nombre)
            .Select(m => new MesaAdminDto(m.Id, m.Nombre, m.Orden, m.Activa))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> GuardarMesaAsync(Guid? mesaId, string nombre, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var limpio = (nombre ?? string.Empty).Trim();
        var idActual = mesaId ?? Guid.Empty;
        var nombreMinusculas = limpio.ToLower();

        if (await db.Mesas.AnyAsync(m => m.Id != idActual && m.Activa && m.Nombre.ToLower() == nombreMinusculas, cancellationToken))
        {
            throw new ReglaNegocioException($"Ya existe una mesa llamada \"{limpio}\".");
        }

        Mesa mesa;
        if (mesaId is null)
        {
            var orden = (await db.Mesas.MaxAsync(m => (int?)m.Orden, cancellationToken) ?? 0) + 1;
            mesa = new Mesa(limpio, orden);
            db.Mesas.Add(mesa);
        }
        else
        {
            mesa = await db.Mesas.FirstOrDefaultAsync(m => m.Id == mesaId, cancellationToken)
                ?? throw new ReglaNegocioException("La mesa ya no existe.");
            mesa.Actualizar(limpio, mesa.Orden);
        }

        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
        return mesa.Id;
    }

    public async Task CambiarEstadoMesaAsync(Guid mesaId, bool activa, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var mesa = await db.Mesas.FirstOrDefaultAsync(m => m.Id == mesaId, cancellationToken)
            ?? throw new ReglaNegocioException("La mesa ya no existe.");

        if (!activa)
        {
            var tieneCuentaAbierta = await db.Cuentas.AnyAsync(
                c => c.MesaId == mesaId && c.Estado == EstadoCuenta.Abierta,
                cancellationToken);

            if (tieneCuentaAbierta)
            {
                throw new ReglaNegocioException($"{mesa.Nombre} tiene una cuenta abierta. Cóbrala antes de quitarla.");
            }

            mesa.Desactivar();
        }
        else
        {
            mesa.Activar();
        }

        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }

    public async Task EliminarMesaAsync(Guid mesaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var mesa = await db.Mesas.FirstOrDefaultAsync(m => m.Id == mesaId, cancellationToken)
            ?? throw new ReglaNegocioException("La mesa ya no existe.");

        var tieneCuentaAbierta = await db.Cuentas.AnyAsync(
            c => c.MesaId == mesaId && c.Estado == EstadoCuenta.Abierta,
            cancellationToken);

        if (tieneCuentaAbierta)
        {
            throw new ReglaNegocioException($"{mesa.Nombre} tiene una cuenta abierta. Cóbrala o cancélala antes de eliminarla.");
        }

        mesa.Eliminar(_reloj.Ahora);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }
}
