namespace Miches360.ViewModels;

public sealed class EnConstruccionViewModel
{
    public EnConstruccionViewModel(string titulo, string descripcion)
    {
        Titulo = titulo;
        Descripcion = descripcion;
    }

    public string Titulo { get; }

    public string Descripcion { get; }
}
