using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Miches360.Dominio.Negocio;

namespace Miches360.Infraestructura.Persistencia.Configuraciones;

internal sealed class ConfiguracionNegocioConfiguracion : IEntityTypeConfiguration<ConfiguracionNegocio>
{
    public void Configure(EntityTypeBuilder<ConfiguracionNegocio> builder)
    {
        builder.ToTable("ConfiguracionNegocio");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Banco).IsRequired().HasMaxLength(60);
        builder.Property(c => c.Clabe).IsRequired().HasMaxLength(18);
        builder.Property(c => c.Titular).IsRequired().HasMaxLength(80);
        builder.Property(c => c.PinHash).IsRequired().HasMaxLength(200);
        builder.Ignore(c => c.TieneDatosTransferencia);

        builder.Property(c => c.NombreTicket).IsRequired().HasMaxLength(60);
        builder.Property(c => c.Direccion).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Telefono).IsRequired().HasMaxLength(30);
        builder.Property(c => c.PieTicket).IsRequired().HasMaxLength(120);
        builder.Property(c => c.ImpresoraTickets).IsRequired().HasMaxLength(200);

        builder.Property(c => c.AnchoTicketMm).HasAnnotation(InicializadorBaseDatos.AnotacionValorInicial, "80");
        builder.Property(c => c.ImprimirTicketAlCobrar).HasAnnotation(InicializadorBaseDatos.AnotacionValorInicial, "1");
        builder.Property(c => c.LogoEnTicket).HasAnnotation(InicializadorBaseDatos.AnotacionValorInicial, "1");
        builder.Property(c => c.PieTicket).HasAnnotation(InicializadorBaseDatos.AnotacionValorInicial, "'¡Gracias por su compra!'");
    }
}
