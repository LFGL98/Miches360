using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public sealed partial class CatalogoAdminViewModel : ObservableObject
{
    public const string PestanaProductos = "Productos";
    public const string PestanaCategorias = "Categorias";
    public const string PestanaMesas = "Mesas";

    public CatalogoAdminViewModel(
        SesionAdministrador sesion,
        AccesoAdminViewModel acceso,
        ProductosAdminViewModel productos,
        CategoriasAdminViewModel categorias,
        MesasAdminViewModel mesas)
    {
        Sesion = sesion;
        Acceso = acceso;
        Productos = productos;
        Categorias = categorias;
        Mesas = mesas;
        PropertyChangedEventManager.AddHandler(sesion, AlCambiarSesion, nameof(SesionAdministrador.Activa));
    }

    public SesionAdministrador Sesion { get; }

    public AccesoAdminViewModel Acceso { get; }

    public ProductosAdminViewModel Productos { get; }

    public CategoriasAdminViewModel Categorias { get; }

    public MesasAdminViewModel Mesas { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsProductos), nameof(EsCategorias), nameof(EsMesas))]
    private string _pestana = PestanaProductos;

    public bool EsProductos => Pestana == PestanaProductos;

    public bool EsCategorias => Pestana == PestanaCategorias;

    public bool EsMesas => Pestana == PestanaMesas;

    public async Task InicializarAsync()
    {
        if (Sesion.Activa)
        {
            await CargarPestanaAsync();
        }
        else
        {
            await Acceso.InicializarAsync();
        }
    }

    [RelayCommand]
    private async Task CambiarPestanaAsync(string? pestana)
    {
        if (string.IsNullOrEmpty(pestana))
        {
            return;
        }

        Pestana = pestana;
        await CargarPestanaAsync();
    }

    [RelayCommand]
    private void Bloquear() => Sesion.Cerrar();

    private Task CargarPestanaAsync() => Pestana switch
    {
        PestanaCategorias => Categorias.CargarAsync(),
        PestanaMesas => Mesas.CargarAsync(),
        _ => Productos.CargarAsync()
    };

    private void AlCambiarSesion(object? sender, PropertyChangedEventArgs e)
    {
        if (Sesion.Activa)
        {
            _ = CargarPestanaAsync();
        }
        else
        {
            _ = Acceso.InicializarAsync();
        }
    }
}
