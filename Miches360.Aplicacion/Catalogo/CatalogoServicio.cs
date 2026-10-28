using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;

namespace Miches360.Aplicacion.Catalogo;

public interface ICatalogoServicio
{
    Task<CatalogoVentaDto> ObtenerCatalogoVentaAsync(CancellationToken cancellationToken = default);
}

internal sealed class CatalogoServicio : ICatalogoServicio
{
    private readonly IAppDbContextFactory _fabrica;

    public CatalogoServicio(IAppDbContextFactory fabrica)
    {
        _fabrica = fabrica;
    }

    public async Task<CatalogoVentaDto> ObtenerCatalogoVentaAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);

        var categorias = await db.Categorias
            .AsNoTracking()
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Nombre)
            .Select(c => new CategoriaDto(c.Id, c.Nombre, c.ColorHex, c.Estacion, c.ManejaTamanos, c.Orden))
            .ToListAsync(cancellationToken);

        var productos = await db.Productos
            .AsNoTracking()
            .Include(p => p.Precios)
            .Where(p => p.Activo && p.Disponible && p.Categoria!.Activa)
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);

        var productosDto = productos
            .Select(p => new ProductoDto(
                p.Id,
                p.CategoriaId,
                p.Nombre,
                p.ColorHex,
                p.Orden,
                p.Precios.ToDictionary(x => x.Tamano, x => x.Precio)))
            .ToList();

        return new CatalogoVentaDto(categorias, productosDto);
    }
}
