using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Dominio.Caja;
using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Corte;

public interface ICorteServicio
{
    Task<CorteDto> ObtenerCorteAsync(DateTime desde, DateTime hasta, CancellationToken cancellationToken = default);

    Task<ArqueoDto> RegistrarArqueoAsync(DateTime dia, decimal fondo, decimal contado, string? notas, string? usuario, CancellationToken cancellationToken = default);
}

internal sealed class CorteServicio : ICorteServicio
{
    private readonly IAppDbContextFactory _fabrica;
    private readonly IReloj _reloj;

    public CorteServicio(IAppDbContextFactory fabrica, IReloj reloj)
    {
        _fabrica = fabrica;
        _reloj = reloj;
    }

    public async Task<CorteDto> ObtenerCorteAsync(DateTime desde, DateTime hasta, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var cobros = await db.Cobros
            .AsNoTracking()
            .Include(c => c.Pagos)
            .Where(c => c.Fecha >= desde && c.Fecha < hasta)
            .Where(c => db.Cuentas.Any(x => x.Id == c.CuentaId && x.Estado != EstadoCuenta.Cancelada))
            .ToListAsync(cancellationToken);

        var idsCobros = cobros.Select(c => c.Id).ToHashSet();

        var cuentas = await db.Cuentas
            .AsNoTracking()
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .Where(c => c.Estado != EstadoCuenta.Cancelada && c.Cobros.Any(x => x.Fecha >= desde && x.Fecha < hasta))
            .ToListAsync(cancellationToken);

        var renglones = cuentas
            .SelectMany(c => c.Subcuentas)
            .Where(s => s.CobroId is Guid id && idsCobros.Contains(id))
            .SelectMany(s => s.Renglones)
            .ToList();

        var idsProductos = renglones.Select(r => r.ProductoId).Distinct().ToList();
        var productos = await db.Productos
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Where(p => idsProductos.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var pagos = cobros.SelectMany(c => c.Pagos).ToList();
        var total = cobros.Sum(c => c.Total);
        var efectivo = pagos.Where(p => p.Metodo == MetodoPago.Efectivo).Sum(p => p.Monto);
        var transferencia = pagos.Where(p => p.Metodo == MetodoPago.Transferencia).Sum(p => p.Monto);

        var totalRenglones = renglones.Sum(r => r.Importe);
        var porCategoria = renglones
            .GroupBy(r => productos.TryGetValue(r.ProductoId, out var p) && p.Categoria is not null
                ? (p.Categoria.Nombre, p.Categoria.ColorHex)
                : ("Sin categoría", "#6B5248"))
            .Select(g => new CorteCategoriaDto(
                g.Key.Item1,
                g.Key.Item2,
                g.Sum(r => r.Cantidad),
                g.Sum(r => r.Importe),
                totalRenglones == 0 ? 0 : g.Sum(r => r.Importe) / totalRenglones))
            .OrderByDescending(c => c.Importe)
            .ToList();

        var porProducto = renglones
            .GroupBy(r => (r.NombreProducto, r.Tamano))
            .Select(g => new CorteProductoDto(
                g.Key.NombreProducto,
                g.Key.Tamano == Tamano.Unico ? string.Empty : g.Key.Tamano.ToString(),
                g.Sum(r => r.Cantidad),
                g.Sum(r => r.Importe)))
            .OrderByDescending(p => p.Importe)
            .ThenBy(p => p.Producto)
            .ToList();

        var porDia = cobros
            .GroupBy(c => c.Fecha.Date)
            .Select(g => new CorteDiaDto(
                g.Key,
                g.Count(),
                g.Sum(c => c.Total),
                g.SelectMany(c => c.Pagos).Where(p => p.Metodo == MetodoPago.Efectivo).Sum(p => p.Monto),
                g.SelectMany(c => c.Pagos).Where(p => p.Metodo == MetodoPago.Transferencia).Sum(p => p.Monto)))
            .OrderBy(d => d.Dia)
            .ToList();

        var abiertas = await db.Cuentas
            .AsNoTracking()
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .Where(c => c.Estado == EstadoCuenta.Abierta)
            .ToListAsync(cancellationToken);

        var canceladas = await db.Cuentas
            .AsNoTracking()
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .Where(c => c.Estado == EstadoCuenta.Cancelada
                        && c.MotivoCancelacion != null
                        && c.CanceladaEn >= desde
                        && c.CanceladaEn < hasta)
            .OrderBy(c => c.CanceladaEn)
            .ToListAsync(cancellationToken);

        var idsMesas = canceladas.Where(c => c.MesaId != null).Select(c => c.MesaId!.Value).Distinct().ToList();
        var mesas = await db.Mesas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => idsMesas.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Nombre, cancellationToken);

        var cancelaciones = canceladas
            .Select(c => new CorteCancelacionDto(
                c.Folio,
                c.CanceladaEn ?? c.CerradaEn ?? c.AbiertaEn,
                c.MesaId is Guid mesaId ? mesas.GetValueOrDefault(mesaId, "Mesa") : "Para llevar",
                c.Total,
                c.MotivoCancelacion ?? string.Empty,
                c.CanceladaPor))
            .ToList();

        var diaDesde = desde.Date;
        var arqueos = await db.ArqueosCaja
            .AsNoTracking()
            .Where(a => a.Dia >= diaDesde && a.Dia < hasta)
            .OrderByDescending(a => a.RealizadoEn)
            .ToListAsync(cancellationToken);

        return new CorteDto(
            desde,
            hasta,
            cobros.Count,
            total,
            efectivo,
            transferencia,
            cobros.Sum(c => c.Cambio),
            cobros.Count == 0 ? 0 : decimal.Round(total / cobros.Count, 2),
            porCategoria,
            porProducto,
            porDia,
            abiertas.Count,
            abiertas.Sum(c => c.Pendiente),
            arqueos.Select(ADto).ToList(),
            cancelaciones);
    }

    public async Task<ArqueoDto> RegistrarArqueoAsync(DateTime dia, decimal fondo, decimal contado, string? notas, string? usuario, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var desde = dia.Date;
        var hasta = desde.AddDays(1);

        var pagosEfectivo = await db.Cobros
            .AsNoTracking()
            .Where(c => c.Fecha >= desde && c.Fecha < hasta)
            .Where(c => db.Cuentas.Any(x => x.Id == c.CuentaId && x.Estado != EstadoCuenta.Cancelada))
            .SelectMany(c => c.Pagos)
            .Where(p => p.Metodo == MetodoPago.Efectivo)
            .Select(p => p.Monto)
            .ToListAsync(cancellationToken);

        var arqueo = new ArqueoCaja(desde, fondo, pagosEfectivo.Sum(), contado, notas, usuario, _reloj.Ahora);
        db.ArqueosCaja.Add(arqueo);
        await db.SaveChangesAsync(cancellationToken);

        return ADto(arqueo);
    }

    private static ArqueoDto ADto(ArqueoCaja a) =>
        new(a.Dia, a.RealizadoEn, a.Usuario, a.Fondo, a.VentasEfectivo, a.Esperado, a.Contado, a.Diferencia, a.Notas);
}
