using Miches360.Dominio.Comun;

namespace Miches360.Dominio.Mesas;

public sealed class Mesa : Entidad
{
    private Mesa()
    {
    }

    public Mesa(string nombre, int orden)
    {
        Actualizar(nombre, orden);
    }

    public string Nombre { get; private set; } = string.Empty;

    public int Orden { get; private set; }

    public bool Activa { get; private set; } = true;

    public bool Eliminada { get; private set; }

    public DateTime? EliminadaEn { get; private set; }

    public void Actualizar(string nombre, int orden)
    {
        Nombre = Validar.Texto(nombre, "El nombre de la mesa", 30);
        Orden = orden;
    }

    public void Activar() => Activa = true;

    public void Desactivar() => Activa = false;

    public void Eliminar(DateTime ahora)
    {
        Activa = false;
        Eliminada = true;
        EliminadaEn = ahora;
    }
}
