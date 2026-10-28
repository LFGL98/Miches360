using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Dominio.Ventas;

namespace Miches360.Aplicacion.Mesas;

public interface IMesasServicio
{
    Task<IReadOnlyList<MesaEstadoDto>> ObtenerMapaAsync(CancellationToken cancellationToken = default);

    Task<CuentaDetalleDto> ObtenerCuentaAsync(Guid cuentaId, CancellationToken cancellationToken = default);

    Task<Guid> AbrirMesaAsync(Guid mesaId, TipoCuenta tipo, int personas, string? usuario, CancellationToken cancellationToken = default);

    Task AgregarRenglonesAsync(Guid cuentaId, IReadOnlyList<RenglonMesaSolicitud> renglones, CancellationToken cancellationToken = default);

    Task QuitarRenglonAsync(Guid cuentaId, Guid renglonId, CancellationToken cancellationToken = default);

    Task MoverRenglonAsync(Guid cuentaId, Guid renglonId, Guid subcuentaDestinoId, CancellationToken cancellationToken = default);

    Task<Guid> AgregarPersonaAsync(Guid cuentaId, string? nombre, CancellationToken cancellationToken = default);

    Task RenombrarPersonaAsync(Guid cuentaId, Guid subcuentaId, string nombre, CancellationToken cancellationToken = default);

    Task QuitarPersonaAsync(Guid cuentaId, Guid subcuentaId, CancellationToken cancellationToken = default);

    Task<CobroRegistradoDto> CobrarAsync(CobroMesaSolicitud solicitud, CancellationToken cancellationToken = default);

    Task CancelarCuentaAsync(Guid cuentaId, CancellationToken cancellationToken = default);
}

internal sealed class MesasServicio : IMesasServicio
{
    private readonly IAppDbContextFactory _fabrica;
    private readonly IReloj _reloj;
    private readonly INotificadorCambios _notificador;

    public MesasServicio(IAppDbContextFactory fabrica, IReloj reloj, INotificadorCambios notificador)
    {
        _fabrica = fabrica;
        _reloj = reloj;
        _notificador = notificador;
    }

    public async Task<IReadOnlyList<MesaEstadoDto>> ObtenerMapaAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var mesas = await db.Mesas
            .AsNoTracking()
            .Where(m => m.Activa)
            .OrderBy(m => m.Orden)
            .ThenBy(m => m.Nombre)
            .ToListAsync(cancellationToken);

        var cuentas = await db.Cuentas
            .AsNoTracking()
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .Where(c => c.Estado == EstadoCuenta.Abierta && c.MesaId != null)
            .ToListAsync(cancellationToken);

        return mesas
            .Select(m =>
            {
                var cuenta = cuentas.FirstOrDefault(c => c.MesaId == m.Id);
                return new MesaEstadoDto(
                    m.Id,
                    m.Nombre,
                    cuenta?.Id,
                    cuenta?.Folio,
                    cuenta?.Tipo,
                    cuenta?.Subcuentas.Count ?? 0,
                    cuenta?.Subcuentas.Count(s => s.Pagada) ?? 0,
                    cuenta?.AbiertaEn,
                    cuenta?.Total ?? 0,
                    cuenta?.Pendiente ?? 0);
            })
            .ToList();
    }

    public async Task<CuentaDetalleDto> ObtenerCuentaAsync(Guid cuentaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var cuenta = await db.Cuentas
            .AsNoTracking()
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones)
            .FirstOrDefaultAsync(c => c.Id == cuentaId, cancellationToken)
            ?? throw new ReglaNegocioException("La cuenta ya no existe.");

        var mesaNombre = cuenta.MesaId is Guid mesaId
            ? await db.Mesas.Where(m => m.Id == mesaId).Select(m => m.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;

        var personas = cuenta.Subcuentas
            .OrderBy(s => s.Orden)
            .Select(s => new PersonaCuentaDto(
                s.Id,
                s.Nombre,
                s.Orden,
                s.Pagada,
                s.Total,
                s.Renglones
                    .OrderBy(r => r.CreadoEn)
                    .Select(r => new RenglonCuentaDto(
                        r.Id,
                        r.ProductoId,
                        r.NombreProducto,
                        r.Tamano,
                        r.Cantidad,
                        r.PrecioUnitario,
                        r.Importe,
                        r.Nota,
                        r.Estacion))
                    .ToList()))
            .ToList();

        return new CuentaDetalleDto(
            cuenta.Id,
            cuenta.Folio,
            cuenta.MesaId,
            mesaNombre ?? "Para llevar",
            cuenta.Tipo,
            cuenta.AbiertaEn,
            personas,
            cuenta.Total,
            cuenta.Pendiente);
    }

    public async Task<Guid> AbrirMesaAsync(Guid mesaId, TipoCuenta tipo, int personas, string? usuario, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var mesa = await db.Mesas.FirstOrDefaultAsync(m => m.Id == mesaId, cancellationToken)
            ?? throw new ReglaNegocioException("La mesa ya no existe.");

        if (!mesa.Activa)
        {
            throw new ReglaNegocioException($"{mesa.Nombre} no está activa.");
        }

        var ocupada = await db.Cuentas.AnyAsync(c => c.MesaId == mesaId && c.Estado == EstadoCuenta.Abierta, cancellationToken);
        if (ocupada)
        {
            throw new ReglaNegocioException($"{mesa.Nombre} ya tiene una cuenta abierta.");
        }

        var folio = (await db.Cuentas.MaxAsync(c => (int?)c.Folio, cancellationToken) ?? 0) + 1;
        var ahora = _reloj.Ahora;
        var cuenta = Cuenta.AbrirMesa(folio, mesaId, tipo, personas, usuario, ahora);

        db.Cuentas.Add(cuenta);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);

        var quien = TextoActividad.Quien(usuario);
        _notificador.RegistrarActividad(new Actividad(
            TipoActividad.MesaAbierta,
            cuenta.Id,
            quien,
            $"{quien} abrió {mesa.Nombre}",
            tipo == TipoCuenta.Separadas ? $"Cuentas separadas · {personas} personas" : "Cuenta general",
            ahora));
        return cuenta.Id;
    }

    public async Task AgregarRenglonesAsync(Guid cuentaId, IReadOnlyList<RenglonMesaSolicitud> renglones, CancellationToken cancellationToken = default)
    {
        if (renglones.Count == 0)
        {
            throw new ReglaNegocioException("No hay productos por enviar.");
        }

        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, cuentaId, incluirCobros: false, cancellationToken);

        var ids = renglones.Select(r => r.ProductoId).Distinct().ToList();
        var productos = await db.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Precios)
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var ahora = _reloj.Ahora;
        foreach (var solicitud in renglones)
        {
            if (!productos.TryGetValue(solicitud.ProductoId, out var producto))
            {
                throw new ReglaNegocioException("Uno de los productos ya no existe. Recarga el catálogo.");
            }

            var renglon = cuenta.AgregarRenglon(solicitud.SubcuentaId, producto, solicitud.Tamano, solicitud.Cantidad, solicitud.Nota, ahora);

            renglon.MarcarEnviado(ahora);
        }

        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }

    public async Task QuitarRenglonAsync(Guid cuentaId, Guid renglonId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, cuentaId, incluirCobros: false, cancellationToken);
        cuenta.QuitarRenglon(renglonId);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }

    public async Task MoverRenglonAsync(Guid cuentaId, Guid renglonId, Guid subcuentaDestinoId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, cuentaId, incluirCobros: false, cancellationToken);
        cuenta.MoverRenglon(renglonId, subcuentaDestinoId);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }

    public async Task<Guid> AgregarPersonaAsync(Guid cuentaId, string? nombre, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, cuentaId, incluirCobros: false, cancellationToken);

        var nombreFinal = string.IsNullOrWhiteSpace(nombre) ? SiguienteNombre(cuenta) : nombre;

        var subcuenta = cuenta.AgregarPersona(nombreFinal);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
        return subcuenta.Id;
    }

    public async Task RenombrarPersonaAsync(Guid cuentaId, Guid subcuentaId, string nombre, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, cuentaId, incluirCobros: false, cancellationToken);
        cuenta.RenombrarPersona(subcuentaId, nombre);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }

    public async Task<CobroRegistradoDto> CobrarAsync(CobroMesaSolicitud solicitud, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, solicitud.CuentaId, incluirCobros: false, cancellationToken);

        var pagos = solicitud.Pagos
            .Select(p => new PagoSolicitado(p.Metodo, p.Monto, p.Referencia))
            .ToList();

        var ahora = _reloj.Ahora;
        var cobro = cuenta.Cobrar(solicitud.SubcuentaIds.ToList(), pagos, solicitud.EfectivoRecibido, solicitud.Usuario, ahora);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);

        var mesaNombre = cuenta.MesaId is Guid mesaId
            ? await db.Mesas.IgnoreQueryFilters().Where(m => m.Id == mesaId).Select(m => m.Nombre).FirstOrDefaultAsync(cancellationToken)
            : null;
        var quien = TextoActividad.Quien(solicitud.Usuario);
        var estado = cuenta.Estado != EstadoCuenta.Abierta ? "mesa libre" : "falta cobrar al resto";
        _notificador.RegistrarActividad(new Actividad(
            TipoActividad.Cobro,
            cuenta.Id,
            quien,
            $"{quien} cobró {mesaNombre ?? $"el folio {cuenta.Folio}"} · {CentralCambios.Dinero(cobro.Total)}",
            $"{TextoActividad.FormaDePago(solicitud.Pagos.Select(p => (p.Metodo, p.Monto)))} · {estado}",
            ahora));

        return new CobroRegistradoDto(cuenta.Id, cobro.Id, cuenta.Folio, cobro.Total, cobro.Cambio, cuenta.Estado != EstadoCuenta.Abierta);
    }

    public async Task CancelarCuentaAsync(Guid cuentaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, cuentaId, incluirCobros: true, cancellationToken);

        if (cuenta.Total > 0)
        {
            throw new ReglaNegocioException("La cuenta tiene productos. Quítalos o cóbralos antes de liberar la mesa.");
        }

        cuenta.Cancelar(_reloj.Ahora);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }

    public async Task QuitarPersonaAsync(Guid cuentaId, Guid subcuentaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var cuenta = await CargarCuentaAsync(db, cuentaId, incluirCobros: false, cancellationToken);
        cuenta.QuitarPersona(subcuentaId, _reloj.Ahora);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Mesas);
    }

    private static string SiguienteNombre(Cuenta cuenta)
    {
        var numero = cuenta.Subcuentas.Count + 1;
        while (cuenta.Subcuentas.Any(s => s.Nombre == $"Persona {numero}"))
        {
            numero++;
        }

        return $"Persona {numero}";
    }

    private static async Task<Cuenta> CargarCuentaAsync(IAppDbContext db, Guid cuentaId, bool incluirCobros, CancellationToken cancellationToken)
    {
        IQueryable<Cuenta> consulta = db.Cuentas
            .Include(c => c.Subcuentas)
            .ThenInclude(s => s.Renglones);

        if (incluirCobros)
        {
            consulta = consulta.Include(c => c.Cobros);
        }

        return await consulta.FirstOrDefaultAsync(c => c.Id == cuentaId, cancellationToken)
            ?? throw new ReglaNegocioException("La cuenta ya no existe.");
    }
}
