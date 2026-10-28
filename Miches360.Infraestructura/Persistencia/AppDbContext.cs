using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Dominio.Caja;
using Miches360.Dominio.Catalogo;
using Miches360.Dominio.Dispositivos;
using Miches360.Dominio.Mesas;
using Miches360.Dominio.Negocio;
using Miches360.Dominio.Ventas;

namespace Miches360.Infraestructura.Persistencia;

public sealed class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
    }

    public DbSet<Categoria> Categorias => Set<Categoria>();

    public DbSet<Producto> Productos => Set<Producto>();

    public DbSet<Mesa> Mesas => Set<Mesa>();

    public DbSet<Cuenta> Cuentas => Set<Cuenta>();

    public DbSet<Cobro> Cobros => Set<Cobro>();

    public DbSet<ConfiguracionNegocio> ConfiguracionesNegocio => Set<ConfiguracionNegocio>();

    public DbSet<ArqueoCaja> ArqueosCaja => Set<ArqueoCaja>();

    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var tipo in modelBuilder.Model.GetEntityTypes())
        {
            var id = tipo.FindProperty("Id");
            if (id is not null && id.ClrType == typeof(Guid))
            {
                id.ValueGenerated = ValueGenerated.Never;
            }
        }
    }
}
