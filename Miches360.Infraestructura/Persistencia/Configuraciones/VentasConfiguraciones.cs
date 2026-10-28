using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Miches360.Dominio.Catalogo;
using Miches360.Dominio.Mesas;
using Miches360.Dominio.Ventas;

namespace Miches360.Infraestructura.Persistencia.Configuraciones;

internal sealed class CuentaConfiguracion : IEntityTypeConfiguration<Cuenta>
{
    public void Configure(EntityTypeBuilder<Cuenta> builder)
    {
        builder.ToTable("Cuentas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Usuario).HasMaxLength(60);
        builder.Property(c => c.MotivoCancelacion).HasMaxLength(120);
        builder.Property(c => c.CanceladaPor).HasMaxLength(60);

        builder.Ignore(c => c.Total);
        builder.Ignore(c => c.EsVentaCancelada);
        builder.Ignore(c => c.Pendiente);

        builder.HasIndex(c => c.Folio).IsUnique();
        builder.HasIndex(c => c.Estado);

        builder.HasOne<Mesa>()
            .WithMany()
            .HasForeignKey(c => c.MesaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Subcuentas)
            .WithOne()
            .HasForeignKey(s => s.CuentaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Cobros)
            .WithOne()
            .HasForeignKey(x => x.CuentaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Subcuentas)
            .HasField("_subcuentas")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(c => c.Cobros)
            .HasField("_cobros")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SubcuentaConfiguracion : IEntityTypeConfiguration<Subcuenta>
{
    public void Configure(EntityTypeBuilder<Subcuenta> builder)
    {
        builder.ToTable("Subcuentas");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nombre).IsRequired().HasMaxLength(40);
        builder.Ignore(s => s.Total);
        builder.Ignore(s => s.Pagada);

        builder.HasOne<Cobro>()
            .WithMany()
            .HasForeignKey(s => s.CobroId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Renglones)
            .WithOne()
            .HasForeignKey(r => r.SubcuentaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Renglones)
            .HasField("_renglones")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class RenglonConfiguracion : IEntityTypeConfiguration<Renglon>
{
    public void Configure(EntityTypeBuilder<Renglon> builder)
    {
        builder.ToTable("Renglones");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.NombreProducto).IsRequired().HasMaxLength(80);
        builder.Property(r => r.Tamano).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Estacion).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.PrecioUnitario).HasPrecision(10, 2);
        builder.Property(r => r.Nota).HasMaxLength(120);
        builder.Ignore(r => r.Importe);

        builder.HasOne<Producto>()
            .WithMany()
            .HasForeignKey(r => r.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CobroConfiguracion : IEntityTypeConfiguration<Cobro>
{
    public void Configure(EntityTypeBuilder<Cobro> builder)
    {
        builder.ToTable("Cobros");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Total).HasPrecision(10, 2);
        builder.Property(c => c.EfectivoRecibido).HasPrecision(10, 2);
        builder.Property(c => c.Cambio).HasPrecision(10, 2);
        builder.Property(c => c.Usuario).HasMaxLength(60);

        builder.HasIndex(c => c.Fecha);

        builder.HasMany(c => c.Pagos)
            .WithOne()
            .HasForeignKey(p => p.CobroId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Pagos)
            .HasField("_pagos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class PagoCobroConfiguracion : IEntityTypeConfiguration<PagoCobro>
{
    public void Configure(EntityTypeBuilder<PagoCobro> builder)
    {
        builder.ToTable("PagosCobro");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Metodo).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Monto).HasPrecision(10, 2);
        builder.Property(p => p.Referencia).HasMaxLength(60);
    }
}
