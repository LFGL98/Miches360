using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Infraestructura.Persistencia;
using Miches360.Infraestructura.Servicios;

namespace Miches360.Infraestructura;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, string rutaBaseDatos)
    {
        services.AddDbContextFactory<AppDbContext>(opciones =>
            opciones.UseSqlite($"Data Source={rutaBaseDatos}"));

        services.AddSingleton<IAppDbContextFactory, FabricaAppDbContext>();
        services.AddSingleton<IReloj, RelojSistema>();
        services.AddSingleton<IHasherPin, HasherPin>();
        return services;
    }
}
