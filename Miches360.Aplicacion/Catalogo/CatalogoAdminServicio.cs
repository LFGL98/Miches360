using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Dominio.Catalogo;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Catalogo;

public interface ICatalogoAdminServicio
{
    Task<IReadOnlyList<CategoriaAdminDto>> ObtenerCategoriasAsync(CancellationToken cancellationToken = default);

    Task<Guid> GuardarCategoriaAsync(CategoriaEdicion edicion, CancellationToken cancellationToken = default);

    Task CambiarEstadoCategoriaAsync(Guid categoriaId, bool activa, CancellationToken cancellationToken = default);

    Task EliminarCategoriaAsync(Guid categoriaId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProductoAdminDto>> ObtenerProductosAsync(bool incluirInactivos, CancellationToken cancellationToken = default);

    Task<Guid> GuardarProductoAsync(ProductoEdicion edicion, CancellationToken cancellationToken = default);

    Task CambiarDisponibleAsync(Guid productoId, bool disponible, CancellationToken cancellationToken = default);

    Task CambiarEstadoProductoAsync(Guid productoId, bool activo, CancellationToken cancellationToken = default);

    Task EliminarProductoAsync(Guid productoId, CancellationToken cancellationToken = default);
}

internal sealed class CatalogoAdminServicio : ICatalogoAdminServicio
{
    private static readonly Tamano[] TamanosVenta = { Tamano.Chico, Tamano.Mediano, Tamano.Litro };

    private readonly IAppDbContextFactory _fabrica;
    private readonly IReloj _reloj;
    private readonly INotificadorCambios _notificador;

    public CatalogoAdminServicio(IAppDbContextFactory fabrica, IReloj reloj, INotificadorCambios notificador)
    {
        _fabrica = fabrica;
        _reloj = reloj;
        _notificador = notificador;
    }

    public async Task<IReadOnlyList<CategoriaAdminDto>> ObtenerCategoriasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var categorias = await db.Categorias
            .AsNoTracking()
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .ToListAsync(cancellationToken);

        var conteos = await db.Productos
            .AsNoTracking()
            .Where(p => p.Activo)
            .GroupBy(p => p.CategoriaId)
            .Select(g => new { CategoriaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.CategoriaId, x => x.Total, cancellationToken);

        return categorias
            .Select(c => new CategoriaAdminDto(
                c.Id,
                c.Nombre,
                c.ColorHex,
                c.Estacion,
                c.ManejaTamanos,
                c.Orden,
                c.Activa,
                conteos.GetValueOrDefault(c.Id)))
            .ToList();
    }

    public async Task<Guid> GuardarCategoriaAsync(CategoriaEdicion edicion, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var nombre = (edicion.Nombre ?? string.Empty).Trim();
        var idActual = edicion.Id ?? Guid.Empty;
        var nombreMinusculas = nombre.ToLower();

        if (await db.Categorias.AnyAsync(c => c.Id != idActual && c.Nombre.ToLower() == nombreMinusculas, cancellationToken))
        {
            throw new ReglaNegocioException($"Ya existe la categoría \"{nombre}\".");
        }

        Categoria categoria;
        if (edicion.Id is null)
        {
            var orden = (await db.Categorias.MaxAsync(c => (int?)c.Orden, cancellationToken) ?? 0) + 1;
            categoria = new Categoria(nombre, edicion.ColorHex, edicion.Estacion, edicion.ManejaTamanos, orden);
            db.Categorias.Add(categoria);
        }
        else
        {
            categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == edicion.Id, cancellationToken)
                ?? throw new ReglaNegocioException("La categoría ya no existe.");
            categoria.Actualizar(nombre, edicion.ColorHex, edicion.Estacion, edicion.ManejaTamanos, categoria.Orden);
        }

        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Catalogo);
        return categoria.Id;
    }

    public async Task CambiarEstadoCategoriaAsync(Guid categoriaId, bool activa, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId, cancellationToken)
            ?? throw new ReglaNegocioException("La categoría ya no existe.");

        if (activa)
        {
            categoria.Activar();
        }
        else
        {
            categoria.Desactivar();
        }

        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Catalogo);
    }

    public async Task EliminarCategoriaAsync(Guid categoriaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == categoriaId, cancellationToken)
            ?? throw new ReglaNegocioException("La categoría ya no existe.");

        var productos = await db.Productos.CountAsync(p => p.CategoriaId == categoriaId, cancellationToken);
        if (productos > 0)
        {
            throw new ReglaNegocioException(
                $"{categoria.Nombre} tiene {productos} producto{(productos == 1 ? string.Empty : "s")}. " +
                "Elimínalos o cámbialos de categoría antes de eliminarla.");
        }

        categoria.Eliminar(_reloj.Ahora);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Catalogo);
    }

    public async Task<IReadOnlyList<ProductoAdminDto>> ObtenerProductosAsync(bool incluirInactivos, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var productos = await db.Productos
            .AsNoTracking()
            .Include(p => p.Categoria)
            .Include(p => p.Precios)
            .Where(p => incluirInactivos || p.Activo)
            .OrderBy(p => p.Categoria!.Orden)
            .ThenBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);

        return productos
            .Select(p => new ProductoAdminDto(
                p.Id,
                p.CategoriaId,
                p.Categoria?.Nombre ?? string.Empty,
                p.Nombre,
                p.ColorHex,
                p.Orden,
                p.EstacionPropia,
                p.Disponible,
                p.Activo,
                p.ObtenerPrecio(Tamano.Unico),
                p.ObtenerPrecio(Tamano.Chico),
                p.ObtenerPrecio(Tamano.Mediano),
                p.ObtenerPrecio(Tamano.Litro)))
            .ToList();
    }

    public async Task<Guid> GuardarProductoAsync(ProductoEdicion edicion, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var categoria = await db.Categorias.FirstOrDefaultAsync(c => c.Id == edicion.CategoriaId, cancellationToken)
            ?? throw new ReglaNegocioException("Elige una categoría.");

        var nombre = (edicion.Nombre ?? string.Empty).Trim();
        var idActual = edicion.Id ?? Guid.Empty;
        var nombreMinusculas = nombre.ToLower();

        var duplicado = await db.Productos.AnyAsync(
            p => p.Id != idActual
                 && p.CategoriaId == categoria.Id
                 && p.Activo
                 && p.Nombre.ToLower() == nombreMinusculas,
            cancellationToken);

        if (duplicado)
        {
            throw new ReglaNegocioException($"Ya existe \"{nombre}\" en {categoria.Nombre}.");
        }

        Producto producto;
        if (edicion.Id is null)
        {
            var orden = await SiguienteOrdenAsync(db, categoria.Id, cancellationToken);
            producto = new Producto(categoria.Id, nombre, edicion.ColorHex, orden, edicion.EstacionPropia);
            db.Productos.Add(producto);
        }
        else
        {
            producto = await db.Productos
                .Include(p => p.Precios)
                .FirstOrDefaultAsync(p => p.Id == edicion.Id, cancellationToken)
                ?? throw new ReglaNegocioException("El producto ya no existe.");

            var orden = producto.Orden;
            if (producto.CategoriaId != categoria.Id)
            {
                producto.CambiarCategoria(categoria.Id);
                orden = await SiguienteOrdenAsync(db, categoria.Id, cancellationToken);
            }

            producto.Actualizar(nombre, edicion.ColorHex, orden, edicion.EstacionPropia);
        }

        AplicarPrecios(producto, categoria.ManejaTamanos, edicion);
        producto.MarcarDisponible(edicion.Disponible);

        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Catalogo);
        return producto.Id;
    }

    public async Task CambiarDisponibleAsync(Guid productoId, bool disponible, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var producto = await ObtenerProductoAsync(db, productoId, cancellationToken);
        producto.MarcarDisponible(disponible);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Catalogo);
    }

    public async Task CambiarEstadoProductoAsync(Guid productoId, bool activo, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var producto = await ObtenerProductoAsync(db, productoId, cancellationToken);

        if (activo)
        {
            producto.Activar();
        }
        else
        {
            producto.Desactivar();
        }

        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Catalogo);
    }

    public async Task EliminarProductoAsync(Guid productoId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var producto = await ObtenerProductoAsync(db, productoId, cancellationToken);
        producto.Eliminar(_reloj.Ahora);
        await db.SaveChangesAsync(cancellationToken);
        _notificador.Notificar(TipoCambio.Catalogo);
    }

    private static void AplicarPrecios(Producto producto, bool manejaTamanos, ProductoEdicion edicion)
    {
        if (manejaTamanos)
        {
            var precios = new Dictionary<Tamano, decimal?>
            {
                [Tamano.Chico] = edicion.PrecioChico,
                [Tamano.Mediano] = edicion.PrecioMediano,
                [Tamano.Litro] = edicion.PrecioLitro
            };

            if (precios.Values.All(v => v is null))
            {
                throw new ReglaNegocioException("Captura el precio de al menos un tamaño.");
            }

            foreach (var (tamano, precio) in precios)
            {
                if (precio is decimal valor)
                {
                    producto.FijarPrecio(tamano, valor);
                }
                else
                {
                    producto.QuitarPrecio(tamano);
                }
            }

            producto.QuitarPrecio(Tamano.Unico);
        }
        else
        {
            if (edicion.PrecioUnico is not decimal precio)
            {
                throw new ReglaNegocioException("Captura el precio.");
            }

            producto.FijarPrecio(Tamano.Unico, precio);
            foreach (var tamano in TamanosVenta)
            {
                producto.QuitarPrecio(tamano);
            }
        }
    }

    private static async Task<int> SiguienteOrdenAsync(IAppDbContext db, Guid categoriaId, CancellationToken cancellationToken) =>
        (await db.Productos
            .Where(p => p.CategoriaId == categoriaId)
            .MaxAsync(p => (int?)p.Orden, cancellationToken) ?? 0) + 1;

    private static async Task<Producto> ObtenerProductoAsync(IAppDbContext db, Guid productoId, CancellationToken cancellationToken) =>
        await db.Productos.FirstOrDefaultAsync(p => p.Id == productoId, cancellationToken)
        ?? throw new ReglaNegocioException("El producto ya no existe.");
}
