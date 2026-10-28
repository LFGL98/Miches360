using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Catalogo;

public sealed record CategoriaDto(
    Guid Id,
    string Nombre,
    string ColorHex,
    EstacionPreparacion Estacion,
    bool ManejaTamanos,
    int Orden);

public sealed record ProductoDto(
    Guid Id,
    Guid CategoriaId,
    string Nombre,
    string ColorHex,
    int Orden,
    IReadOnlyDictionary<Tamano, decimal> Precios);

public sealed record CatalogoVentaDto(
    IReadOnlyList<CategoriaDto> Categorias,
    IReadOnlyList<ProductoDto> Productos);
