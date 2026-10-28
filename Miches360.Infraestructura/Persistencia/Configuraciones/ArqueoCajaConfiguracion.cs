using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Miches360.Dominio.Caja;

namespace Miches360.Infraestructura.Persistencia.Configuraciones;

internal sealed class ArqueoCajaConfiguracion : IEntityTypeConfiguration<ArqueoCaja>
{
    public void Configure(EntityTypeBuilder<ArqueoCaja> builder)
    {
        builder.ToTable("ArqueosCaja");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Fondo).HasPrecision(10, 2);
        builder.Property(a => a.VentasEfectivo).HasPrecision(10, 2);
        builder.Property(a => a.Esperado).HasPrecision(10, 2);
        builder.Property(a => a.Contado).HasPrecision(10, 2);
        builder.Property(a => a.Diferencia).HasPrecision(10, 2);
        builder.Property(a => a.Usuario).HasMaxLength(60);
        builder.Property(a => a.Notas).HasMaxLength(200);

        builder.HasIndex(a => a.Dia);
    }
}
