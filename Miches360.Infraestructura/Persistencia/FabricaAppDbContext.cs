using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;

namespace Miches360.Infraestructura.Persistencia;

internal sealed class FabricaAppDbContext : IAppDbContextFactory
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;

    public FabricaAppDbContext(IDbContextFactory<AppDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    public async Task<IAppDbContext> CrearAsync(CancellationToken cancellationToken = default) =>
        await _fabrica.CreateDbContextAsync(cancellationToken);
}
