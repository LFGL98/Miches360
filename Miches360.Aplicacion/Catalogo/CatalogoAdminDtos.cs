using Miches360.Dominio.Enums;

namespace Miches360.Aplicacion.Catalogo;

public sealed record CategoriaAdminDto(
    Guid Id,
    string Nombre,
    string ColorHex,
    EstacionPreparacion Estacion,
    bool ManejaTamanos,
    int Orden,
    bool Activa,
    int Productos);

public sealed record CategoriaEdicion(
    Guid? Id,
    string Nombre,
    string ColorHex,
    EstacionPreparacion Estacion,
    bool ManejaTamanos);

public sealed record ProductoAdminDto(
    Guid Id,
    Guid CategoriaId,
    string Categoria,
    string Nombre,
    string ColorHex,
    int Orden,
    EstacionPreparacion? EstacionPropia,
    bool Disponible,
    bool Activo,
    decimal? PrecioUnico,
    decimal? PrecioChico,
    decimal? PrecioMediano,
    decimal? PrecioLitro);

public sealed record ProductoEdicion(
    Guid? Id,
    Guid CategoriaId,
    string Nombre,
    string ColorHex,
    EstacionPreparacion? EstacionPropia,
    decimal? PrecioUnico,
    decimal? PrecioChico,
    decimal? PrecioMediano,
    decimal? PrecioLitro,
    bool Disponible);

public sealed record MesaAdminDto(Guid Id, string Nombre, int Orden, bool Activa);
