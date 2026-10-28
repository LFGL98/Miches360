using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Miches360.Dominio.Catalogo;

namespace Miches360.Infraestructura.Persistencia.Configuraciones;

internal sealed class CategoriaConfiguracion : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.ToTable("Categorias");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(60);
        builder.Property(c => c.ColorHex).IsRequired().HasMaxLength(7);
        builder.Property(c => c.Estacion).HasConversion<string>().HasMaxLength(20);

        builder.HasQueryFilter(c => !c.Eliminada);

        builder.HasMany(c => c.Productos)
            .WithOne(p => p.Categoria)
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Productos)
            .HasField("_productos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        builder.ToTable("Productos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(80);
        builder.Property(p => p.ColorHex).IsRequired().HasMaxLength(7);
        builder.Property(p => p.EstacionPropia).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(p => p.EstacionEfectiva);

        builder.HasQueryFilter(p => !p.Eliminado);

        builder.HasMany(p => p.Precios)
            .WithOne()
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Precios)
            .HasField("_precios")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => new { p.CategoriaId, p.Orden });
    }
}

internal sealed class PrecioProductoConfiguracion : IEntityTypeConfiguration<PrecioProducto>
{
    public void Configure(EntityTypeBuilder<PrecioProducto> builder)
    {
        builder.ToTable("PreciosProducto");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Tamano).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Precio).HasPrecision(10, 2);

        builder.HasIndex(p => new { p.ProductoId, p.Tamano }).IsUnique();
    }
}
