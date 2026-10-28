using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Miches360.Dominio.Dispositivos;

namespace Miches360.Infraestructura.Persistencia.Configuraciones;

internal sealed class DispositivoConfiguracion : IEntityTypeConfiguration<Dispositivo>
{
    public void Configure(EntityTypeBuilder<Dispositivo> builder)
    {
        builder.ToTable("Dispositivos");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Mesero).IsRequired().HasMaxLength(40);
        builder.Property(d => d.Modelo).HasMaxLength(80);
        builder.Property(d => d.TokenHash).IsRequired().HasMaxLength(128);

        builder.HasIndex(d => d.TokenHash).IsUnique();
    }
}
