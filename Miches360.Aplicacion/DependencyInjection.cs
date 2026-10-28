using Microsoft.Extensions.DependencyInjection;
using Miches360.Aplicacion.Ajustes;
using Miches360.Aplicacion.Catalogo;
using Miches360.Aplicacion.Corte;
using Miches360.Aplicacion.Dispositivos;
using Miches360.Aplicacion.Historial;
using Miches360.Aplicacion.Mesas;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Aplicacion.Ventas;

namespace Miches360.Aplicacion;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        services.AddSingleton<CentralCambios>();
        services.AddSingleton<INotificadorCambios>(sp => sp.GetRequiredService<CentralCambios>());
        services.AddSingleton<IDispositivosServicio, DispositivosServicio>();

        services.AddSingleton<ICatalogoServicio, CatalogoServicio>();
        services.AddSingleton<ICatalogoAdminServicio, CatalogoAdminServicio>();
        services.AddSingleton<IMesasAdminServicio, MesasAdminServicio>();
        services.AddSingleton<IMesasServicio, MesasServicio>();
        services.AddSingleton<IVentaServicio, VentaServicio>();
        services.AddSingleton<ICorteServicio, CorteServicio>();
        services.AddSingleton<IHistorialServicio, HistorialServicio>();
        services.AddSingleton<IAjustesServicio, AjustesServicio>();
        return services;
    }
}
