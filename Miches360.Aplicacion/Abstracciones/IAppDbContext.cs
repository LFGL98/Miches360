using Microsoft.EntityFrameworkCore;
using Miches360.Dominio.Caja;
using Miches360.Dominio.Catalogo;
using Miches360.Dominio.Dispositivos;
using Miches360.Dominio.Mesas;
using Miches360.Dominio.Negocio;
using Miches360.Dominio.Ventas;

namespace Miches360.Aplicacion.Abstracciones;

public interface IAppDbContext : IAsyncDisposable
{
    DbSet<Categoria> Categorias { get; }

    DbSet<Producto> Productos { get; }

    DbSet<Mesa> Mesas { get; }

    DbSet<Cuenta> Cuentas { get; }

    DbSet<Cobro> Cobros { get; }

    DbSet<ConfiguracionNegocio> ConfiguracionesNegocio { get; }

    DbSet<ArqueoCaja> ArqueosCaja { get; }

    DbSet<Dispositivo> Dispositivos { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IAppDbContextFactory
{
    Task<IAppDbContext> CrearAsync(CancellationToken cancellationToken = default);
}

public interface IReloj
{
    DateTime Ahora { get; }
}

public interface IHasherPin
{
    string Calcular(string pin);

    bool Verificar(string pin, string hash);
}
