using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Miches360.Dominio.Mesas;

namespace Miches360.Infraestructura.Persistencia.Configuraciones;

internal sealed class MesaConfiguracion : IEntityTypeConfiguration<Mesa>
{
    public void Configure(EntityTypeBuilder<Mesa> builder)
    {
        builder.ToTable("Mesas");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Nombre).IsRequired().HasMaxLength(30);

        builder.HasQueryFilter(m => !m.Eliminada);
    }
}
