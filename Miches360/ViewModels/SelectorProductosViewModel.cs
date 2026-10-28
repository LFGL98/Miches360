using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Miches360.Aplicacion.Catalogo;
using Miches360.Dominio.Enums;
using Miches360.Mensajes;

namespace Miches360.ViewModels;

public sealed partial class SelectorProductosViewModel : ObservableObject
{
    private readonly ICatalogoServicio _catalogo;
    private List<ProductoDto> _productos = new();
    private bool _cargado;

    public SelectorProductosViewModel(ICatalogoServicio catalogo)
    {
        _catalogo = catalogo;

        Tamanos = new ObservableCollection<OpcionTamano>
        {
            new(Tamano.Chico, "Chico"),
            new(Tamano.Mediano, "Mediano"),
            new(Tamano.Litro, "Litro")
        };

        TamanoSeleccionado = Tamanos[1];
        TamanoSeleccionado.EstaSeleccionado = true;

        WeakReferenceMessenger.Default.Register<SelectorProductosViewModel, CatalogoActualizadoMensaje>(
            this,
            static (destinatario, _) => destinatario._cargado = false);
    }

    public event Action<ProductoItem, Tamano, decimal>? ProductoElegido;

    public ObservableCollection<CategoriaItem> Categorias { get; } = new();

    public ObservableCollection<ProductoItem> Productos { get; } = new();

    public ObservableCollection<OpcionTamano> Tamanos { get; }

    [ObservableProperty]
    private CategoriaItem? _categoriaSeleccionada;

    [ObservableProperty]
    private OpcionTamano _tamanoSeleccionado = null!;

    [ObservableProperty]
    private bool _mostrarTamanos;

    [ObservableProperty]
    private bool _cargando;

    [ObservableProperty]
    private string? _mensajeError;

    private Tamano TamanoEfectivo => MostrarTamanos ? TamanoSeleccionado.Tamano : Tamano.Unico;

    public async Task InicializarAsync()
    {
        if (_cargado || Cargando)
        {
            return;
        }

        await RecargarCatalogoAsync();
    }

    [RelayCommand]
    private async Task RecargarCatalogoAsync()
    {
        Cargando = true;
        MensajeError = null;
        try
        {
            var catalogo = await _catalogo.ObtenerCatalogoVentaAsync();
            _productos = catalogo.Productos.ToList();

            var seleccionAnterior = CategoriaSeleccionada?.Id;
            Categorias.Clear();
            foreach (var categoria in catalogo.Categorias)
            {
                Categorias.Add(new CategoriaItem(categoria));
            }

            SeleccionarCategoria(Categorias.FirstOrDefault(c => c.Id == seleccionAnterior) ?? Categorias.FirstOrDefault());
            _cargado = true;
        }
        catch (Exception ex)
        {
            MensajeError = $"No se pudo cargar el catálogo: {ex.Message}";
        }
        finally
        {
            Cargando = false;
        }
    }

    [RelayCommand]
    private void SeleccionarCategoria(CategoriaItem? categoria)
    {
        if (categoria is null)
        {
            Productos.Clear();
            return;
        }

        foreach (var item in Categorias)
        {
            item.EstaSeleccionada = ReferenceEquals(item, categoria);
        }

        CategoriaSeleccionada = categoria;
        MostrarTamanos = categoria.ManejaTamanos;

        Productos.Clear();
        foreach (var producto in _productos.Where(p => p.CategoriaId == categoria.Id))
        {
            Productos.Add(new ProductoItem(producto));
        }

        AplicarTamano();
    }

    [RelayCommand]
    private void SeleccionarTamano(OpcionTamano? opcion)
    {
        if (opcion is null)
        {
            return;
        }

        foreach (var item in Tamanos)
        {
            item.EstaSeleccionado = ReferenceEquals(item, opcion);
        }

        TamanoSeleccionado = opcion;
        AplicarTamano();
    }

    [RelayCommand]
    private void ElegirProducto(ProductoItem? producto)
    {
        if (producto?.PrecioActual is decimal precio)
        {
            ProductoElegido?.Invoke(producto, TamanoEfectivo, precio);
        }
    }

    private void AplicarTamano()
    {
        var tamano = TamanoEfectivo;
        foreach (var producto in Productos)
        {
            producto.AplicarTamano(tamano);
        }
    }
}
