namespace Miches360.Dominio.Comun;

public abstract class Entidad
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
