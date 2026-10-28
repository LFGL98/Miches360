using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Dominio.Ventas;

namespace Miches360.Aplicacion.Historial;

public interface IHistorialServicio
{
    Task<IReadOnlyList<VentaResumenDto>> ListarAsync(DateTime desde, DateTime hasta, CancellationToken cancellationToken = default);

    Task<TicketDto> ObtenerTicketAsync(Guid cuentaId, Guid? cobroId = null, CancellationToken cancellationToken = default);

    Task CancelarVentaAsync(Guid cuentaId, string pin, string? motivo, string? usuario, CancellationToken cancellationToken = default);
}

internal sealed class HistorialServicio : IHistorialServicio
{
    private readonly IAppDbContextFactory _fabrica;
    private readonly IHasherPin _hasher;
    private readonly IReloj _reloj;
    private readonly INotificadorCambios _notificador;

    public HistorialServicio(IAppDbContextFactory fabrica, IHasherPin hasher, IReloj reloj, INotificadorCambios notificador)
    {
        _fabrica = fabrica;
        _hasher = hasher;
        _reloj = reloj;
        _notificador = notificador;
    }

    public async Task<IReadOnlyList<VentaResumenDto>> ListarAsync(DateTime desde, DateTime hasta, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var cuentas = await db.Cuentas
            .AsNoTracking()
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .Include(c => c.Cobros)
            .ThenInclude(c => c.Pagos)
            .Where(c => (c.AbiertaEn >= desde && c.AbiertaEn < hasta)
                        || c.Cobros.Any(x => x.Fecha >= desde && x.Fecha < hasta)
                        || (c.CanceladaEn >= desde && c.CanceladaEn < hasta))
            .ToListAsync(cancellationToken);

        cuentas = cuentas
            .Where(c => c.Estado != EstadoCuenta.Cancelada || c.MotivoCancelacion is not null)
            .ToList();

        var mesas = await NombresMesasAsync(db, cuentas, cancellationToken);

        return cuentas
            .OrderByDescending(c => c.Folio)
            .Select(c => new VentaResumenDto(
                c.Id,
                c.Folio,
                FechaVenta(c),
                Origen(c, mesas),
                c.Estado,
                c.Total,
                c.Cobros.Sum(x => x.Total),
                Metodos(c.Cobros.SelectMany(x => x.Pagos)),
                c.Subcuentas.SelectMany(s => s.Renglones).Sum(r => r.Cantidad),
                c.MotivoCancelacion))
            .ToList();
    }

    public async Task<TicketDto> ObtenerTicketAsync(Guid cuentaId, Guid? cobroId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var cuenta = await db.Cuentas
            .AsNoTracking()
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .Include(c => c.Cobros)
            .ThenInclude(c => c.Pagos)
            .FirstOrDefaultAsync(c => c.Id == cuentaId, cancellationToken)
            ?? throw new ReglaNegocioException("La venta ya no existe.");

        var mesas = await NombresMesasAsync(db, new[] { cuenta }, cancellationToken);

        var cobros = cobroId is Guid id
            ? cuenta.Cobros.Where(c => c.Id == id).ToList()
            : cuenta.Cobros.OrderBy(c => c.Fecha).ToList();

        if (cobroId is not null && cobros.Count == 0)
        {
            throw new ReglaNegocioException("El cobro ya no existe.");
        }

        var subcuentas = cobroId is Guid soloCobro
            ? cuenta.Subcuentas.Where(s => s.CobroId == soloCobro).OrderBy(s => s.Orden).ToList()
            : cuenta.Subcuentas.OrderBy(s => s.Orden).ToList();

        var conPersonas = subcuentas.Count(s => s.Renglones.Count > 0) > 1;
        var renglones = subcuentas
            .SelectMany(s => s.Renglones
                .OrderBy(r => r.CreadoEn)
                .Select(r => new TicketRenglonDto(
                    r.Cantidad,
                    r.NombreProducto,
                    r.Tamano == Tamano.Unico ? string.Empty : r.Tamano.ToString(),
                    r.PrecioUnitario,
                    r.Importe,
                    conPersonas ? s.Nombre : null,
                    r.Nota)))
            .ToList();

        var personas = cobroId is not null && cuenta.Subcuentas.Count > 1
            ? string.Join(", ", subcuentas.Select(s => s.Nombre))
            : null;

        var total = cobroId is null ? cuenta.Total : cobros.Sum(c => c.Total);
        var fecha = cobros.Count > 0 ? cobros.Max(c => c.Fecha) : FechaVenta(cuenta);

        return new TicketDto(
            cuenta.Id,
            cuenta.Folio,
            fecha,
            Origen(cuenta, mesas),
            personas,
            cuenta.Estado,
            renglones,
            total,
            cobros.SelectMany(c => c.Pagos).Select(p => new TicketPagoDto(p.Metodo, p.Monto, p.Referencia)).ToList(),
            cobros.Sum(c => c.EfectivoRecibido),
            cobros.Sum(c => c.Cambio),
            cobroId is null && cuenta.Estado == EstadoCuenta.Abierta ? cuenta.Pendiente : 0,
            cobros.Select(c => c.Usuario).LastOrDefault(u => u is not null) ?? cuenta.Usuario,
            cuenta.CanceladaEn,
            cuenta.MotivoCancelacion,
            cuenta.CanceladaPor);
    }

    public async Task CancelarVentaAsync(Guid cuentaId, string pin, string? motivo, string? usuario, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var config = await db.ConfiguracionesNegocio.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (config is null || !_hasher.Verificar(pin ?? string.Empty, config.PinHash))
        {
            throw new ReglaNegocioException("PIN de administrador incorrecto.");
        }

        var cuenta = await db.Cuentas
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .FirstOrDefaultAsync(c => c.Id == cuentaId, cancellationToken)
            ?? throw new ReglaNegocioException("La venta ya no existe.");

        var ahora = _reloj.Ahora;
        cuenta.CancelarVenta(motivo, usuario, ahora);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);

        var nombres = await NombresMesasAsync(db, new[] { cuenta }, cancellationToken);
        var quien = TextoActividad.Quien(usuario);
        var venta = cuenta.MesaId is Guid mesaId && nombres.TryGetValue(mesaId, out var mesa) ? mesa : $"el folio {cuenta.Folio}";
        _notificador.RegistrarActividad(new Actividad(
            TipoActividad.VentaCancelada,
            cuenta.Id,
            quien,
            $"{quien} canceló {venta} · {CentralCambios.Dinero(cuenta.Total)}",
            $"Motivo: {motivo?.Trim()}",
            ahora));
    }

    private static async Task<Dictionary<Guid, string>> NombresMesasAsync(IAppDbContext db, IEnumerable<Cuenta> cuentas, CancellationToken cancellationToken)
    {
        var ids = cuentas.Where(c => c.MesaId != null).Select(c => c.MesaId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Mesas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken);
    }

    private static string Origen(Cuenta cuenta, IReadOnlyDictionary<Guid, string> mesas) =>
        cuenta.MesaId is Guid mesaId
            ? mesas.GetValueOrDefault(mesaId, "Mesa")
            : "Para llevar";

    private static DateTime FechaVenta(Cuenta cuenta) =>
        cuenta.Cobros.Count > 0 ? cuenta.Cobros.Max(c => c.Fecha) : cuenta.AbiertaEn;

    private static string Metodos(IEnumerable<PagoCobro> pagos)
    {
        var metodos = pagos.Select(p => p.Metodo).Distinct().ToList();
        return metodos.Count switch
        {
            0 => string.Empty,
            1 => metodos[0] == MetodoPago.Efectivo ? "Efectivo" : "Transferencia",
            _ => "Mixto"
        };
    }
}
