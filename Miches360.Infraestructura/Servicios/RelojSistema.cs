using Miches360.Aplicacion.Abstracciones;

namespace Miches360.Infraestructura.Servicios;

internal sealed class RelojSistema : IReloj
{
    public DateTime Ahora => DateTime.Now;
}
