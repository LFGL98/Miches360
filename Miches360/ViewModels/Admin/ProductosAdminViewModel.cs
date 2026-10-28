using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Miches360.Aplicacion.Catalogo;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Mensajes;

namespace Miches360.ViewModels;

public sealed partial class ProductosAdminViewModel : ObservableObject
{
    private readonly ICatalogoAdminServicio _servicio;
    private List<CategoriaAdminDto> _categorias = new();
    private List<ProductoAdminDto> _productos = new();
    private Guid? _filtro;

    public ProductosAdminViewModel(ICatalogoAdminServicio servicio)
    {
        _servicio = servicio;
        Colores = Opciones.CrearColores();
        Estaciones = new List<OpcionSeleccionable<EstacionPreparacion?>>
        {
            new(null, "Igual que su categoría"),
            new(EstacionPreparacion.Barra, "Barra"),
            new(EstacionPreparacion.Cocina, "Cocina"),
            new(EstacionPreparacion.YaListo, "Ya listo")
        };
        Opciones.Marcar(Colores, ColorHex);
        Opciones.Marcar(Estaciones, Estacion);
    }

    public ObservableCollection<OpcionSeleccionable<Guid?>> Filtros { get; } = new();

    public ObservableCollection<ProductoAdminItem> Productos { get; } = new();

    public ObservableCollection<OpcionSeleccionable<Guid>> CategoriasFormulario { get; } = new();

    public IReadOnlyList<OpcionSeleccionable<string>> Colores { get; }

    public IReadOnlyList<OpcionSeleccionable<EstacionPreparacion?>> Estaciones { get; }

    [ObservableProperty]
    private bool _mostrarInactivos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloFormulario), nameof(EsEdicion))]
    private Guid? _editandoId;

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private Guid? _categoriaId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsaPrecioUnico))]
    private bool _usaTamanos;

    [ObservableProperty]
    private string _precioUnico = string.Empty;

    [ObservableProperty]
    private string _precioChico = string.Empty;

    [ObservableProperty]
    private string _precioMediano = string.Empty;

    [ObservableProperty]
    private string _precioLitro = string.Empty;

    [ObservableProperty]
    private EstacionPreparacion? _estacion;

    [ObservableProperty]
    private string _colorHex = Opciones.Paleta[0];

    [ObservableProperty]
    private bool _disponible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCambiarActivo))]
    private bool _activo = true;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(CambiarActivoCommand), nameof(CambiarDisponibleCommand), nameof(EliminarCommand))]
    private bool _procesando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoEliminar))]
    private bool _confirmandoEliminar;

    public string TextoEliminar => ConfirmandoEliminar ? "¿Seguro? Toca otra vez para eliminar" : "Eliminar producto";

    public string TituloFormulario => EditandoId is null ? "NUEVO PRODUCTO" : "EDITAR PRODUCTO";

    public bool EsEdicion => EditandoId is not null;

    public bool UsaPrecioUnico => !UsaTamanos;

    public string TextoCambiarActivo => Activo ? "Quitar del catálogo" : "Regresar al catálogo";

    partial void OnMostrarInactivosChanged(bool value) => _ = CargarAsync();

    partial void OnCategoriaIdChanged(Guid? value)
    {
        UsaTamanos = _categorias.FirstOrDefault(c => c.Id == value)?.ManejaTamanos ?? false;
        Opciones.Marcar(CategoriasFormulario, value ?? Guid.Empty);
    }

    partial void OnColorHexChanged(string value) => Opciones.Marcar(Colores, value);

    partial void OnEstacionChanged(EstacionPreparacion? value) => Opciones.Marcar(Estaciones, value);

    public async Task CargarAsync()
    {
        try
        {
            _categorias = (await _servicio.ObtenerCategoriasAsync()).ToList();
            _productos = (await _servicio.ObtenerProductosAsync(MostrarInactivos)).ToList();

            if (_filtro is Guid filtro && _categorias.All(c => c.Id != filtro))
            {
                _filtro = null;
            }

            ReconstruirFiltros();
            ReconstruirCategoriasFormulario();
            MostrarProductos();

            if (EditandoId is null && CategoriaId is null)
            {
                Nuevo();
            }
            else
            {
                UsaTamanos = _categorias.FirstOrDefault(c => c.Id == CategoriaId)?.ManejaTamanos ?? false;
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron cargar los productos: {ex.Message}", esError: true);
        }
    }

    [RelayCommand]
    private void Filtrar(OpcionSeleccionable<Guid?>? filtro)
    {
        _filtro = filtro?.Valor;
        Opciones.Marcar(Filtros, _filtro);
        MostrarProductos();
    }

    [RelayCommand]
    private void Seleccionar(ProductoAdminItem? item)
    {
        if (item is null)
        {
            return;
        }

        var p = item.Producto;
        EditandoId = p.Id;
        Nombre = p.Nombre;
        CategoriaId = p.CategoriaId;
        ReconstruirCategoriasFormulario();
        PrecioUnico = Opciones.TextoPrecio(p.PrecioUnico);
        PrecioChico = Opciones.TextoPrecio(p.PrecioChico);
        PrecioMediano = Opciones.TextoPrecio(p.PrecioMediano);
        PrecioLitro = Opciones.TextoPrecio(p.PrecioLitro);
        Estacion = p.EstacionPropia;
        ColorHex = p.ColorHex;
        Disponible = p.Disponible;
        Activo = p.Activo;
        Mensaje = null;
        ConfirmandoEliminar = false;

        foreach (var otro in Productos)
        {
            otro.EstaSeleccionado = ReferenceEquals(otro, item);
        }
    }

    [RelayCommand]
    private void Nuevo()
    {
        var categoria = _categorias.FirstOrDefault(c => c.Id == _filtro && c.Activa)
            ?? _categorias.FirstOrDefault(c => c.Activa);

        EditandoId = null;
        Nombre = string.Empty;
        CategoriaId = categoria?.Id;
        ReconstruirCategoriasFormulario();
        PrecioUnico = string.Empty;
        PrecioChico = string.Empty;
        PrecioMediano = string.Empty;
        PrecioLitro = string.Empty;
        Estacion = null;
        ColorHex = categoria?.ColorHex ?? Opciones.Paleta[0];
        Disponible = true;
        Activo = true;
        Mensaje = null;
        ConfirmandoEliminar = false;
        Opciones.Marcar(Colores, ColorHex);
        Opciones.Marcar(Estaciones, Estacion);

        foreach (var item in Productos)
        {
            item.EstaSeleccionado = false;
        }
    }

    [RelayCommand]
    private void ElegirCategoria(OpcionSeleccionable<Guid>? categoria)
    {
        if (categoria is not null)
        {
            CategoriaId = categoria.Valor;
        }
    }

    [RelayCommand]
    private void ElegirColor(OpcionSeleccionable<string>? color)
    {
        if (color is not null)
        {
            ColorHex = color.Valor;
        }
    }

    [RelayCommand]
    private void ElegirEstacion(OpcionSeleccionable<EstacionPreparacion?>? estacion)
    {
        if (estacion is not null)
        {
            Estacion = estacion.Valor;
        }
    }

    [RelayCommand]
    private void FijarDisponible(string? valor) => Disponible = valor == "si";

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task GuardarAsync()
    {
        if (CategoriaId is not Guid categoriaId)
        {
            MostrarMensaje("Elige una categoría.", esError: true);
            return;
        }

        if (!Opciones.IntentarLeerPrecio(PrecioUnico, out var unico)
            || !Opciones.IntentarLeerPrecio(PrecioChico, out var chico)
            || !Opciones.IntentarLeerPrecio(PrecioMediano, out var mediano)
            || !Opciones.IntentarLeerPrecio(PrecioLitro, out var litro))
        {
            MostrarMensaje("Revisa los precios: solo números, por ejemplo 85 o 85.50.", esError: true);
            return;
        }

        Procesando = true;
        try
        {
            var edicion = new ProductoEdicion(
                EditandoId,
                categoriaId,
                Nombre,
                ColorHex,
                Estacion,
                UsaTamanos ? null : unico,
                UsaTamanos ? chico : null,
                UsaTamanos ? mediano : null,
                UsaTamanos ? litro : null,
                Disponible);

            EditandoId = await _servicio.GuardarProductoAsync(edicion);
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new CatalogoActualizadoMensaje());
            MostrarMensaje($"\"{Nombre.Trim()}\" guardado. Ya aparece en venta.", esError: false);
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo guardar: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task CambiarDisponibleAsync(ProductoAdminItem? item)
    {
        if (item is null)
        {
            return;
        }

        Procesando = true;
        try
        {
            var disponible = !item.Producto.Disponible;
            await _servicio.CambiarDisponibleAsync(item.Producto.Id, disponible);
            if (EditandoId == item.Producto.Id)
            {
                Disponible = disponible;
            }

            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new CatalogoActualizadoMensaje());
        }
        catch (Exception ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task CambiarActivoAsync()
    {
        if (EditandoId is not Guid id)
        {
            return;
        }

        Procesando = true;
        try
        {
            await _servicio.CambiarEstadoProductoAsync(id, !Activo);
            Activo = !Activo;
            if (!Activo && !MostrarInactivos)
            {
                Nuevo();
            }

            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new CatalogoActualizadoMensaje());
            MostrarMensaje(
                Activo ? "El producto regresó al catálogo." : "Producto quitado del catálogo. Sus ventas anteriores se conservan.",
                esError: false);
        }
        catch (Exception ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task EliminarAsync()
    {
        if (EditandoId is not Guid id)
        {
            return;
        }

        if (!ConfirmandoEliminar)
        {
            ConfirmandoEliminar = true;
            return;
        }

        Procesando = true;
        try
        {
            var nombre = Nombre.Trim();
            await _servicio.EliminarProductoAsync(id);
            Nuevo();
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new CatalogoActualizadoMensaje());
            MostrarMensaje($"{nombre} eliminado. Sus ventas anteriores se conservan.", esError: false);
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo eliminar: {ex.Message}", esError: true);
        }
        finally
        {
            ConfirmandoEliminar = false;
            Procesando = false;
        }
    }

    private bool NoProcesando() => !Procesando;

    private void ReconstruirFiltros()
    {
        Filtros.Clear();
        Filtros.Add(new OpcionSeleccionable<Guid?>(null, "Todas"));
        foreach (var categoria in _categorias.Where(c => c.Activa || c.Id == _filtro))
        {
            Filtros.Add(new OpcionSeleccionable<Guid?>(categoria.Id, categoria.Nombre, categoria.ColorHex));
        }

        Opciones.Marcar(Filtros, _filtro);
    }

    private void ReconstruirCategoriasFormulario()
    {
        CategoriasFormulario.Clear();
        foreach (var categoria in _categorias.Where(c => c.Activa || c.Id == CategoriaId))
        {
            CategoriasFormulario.Add(new OpcionSeleccionable<Guid>(categoria.Id, categoria.Nombre, categoria.ColorHex));
        }

        Opciones.Marcar(CategoriasFormulario, CategoriaId ?? Guid.Empty);
    }

    private void MostrarProductos()
    {
        Productos.Clear();
        foreach (var producto in _productos.Where(p => _filtro is null || p.CategoriaId == _filtro))
        {
            Productos.Add(new ProductoAdminItem(producto) { EstaSeleccionado = producto.Id == EditandoId });
        }
    }

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }
}
