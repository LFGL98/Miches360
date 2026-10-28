using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Miches360.Aplicacion.Catalogo;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Mensajes;

namespace Miches360.ViewModels;

public sealed partial class CategoriasAdminViewModel : ObservableObject
{
    private readonly ICatalogoAdminServicio _servicio;

    public CategoriasAdminViewModel(ICatalogoAdminServicio servicio)
    {
        _servicio = servicio;
        Colores = Opciones.CrearColores();
        Estaciones = new List<OpcionSeleccionable<EstacionPreparacion>>
        {
            new(EstacionPreparacion.Barra, "Barra"),
            new(EstacionPreparacion.Cocina, "Cocina"),
            new(EstacionPreparacion.YaListo, "Ya listo")
        };
        Nueva();
    }

    public ObservableCollection<CategoriaAdminItem> Categorias { get; } = new();

    public IReadOnlyList<OpcionSeleccionable<string>> Colores { get; }

    public IReadOnlyList<OpcionSeleccionable<EstacionPreparacion>> Estaciones { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloFormulario), nameof(EsEdicion))]
    private Guid? _editandoId;

    [ObservableProperty]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _colorHex = Opciones.Paleta[0];

    [ObservableProperty]
    private EstacionPreparacion _estacion = EstacionPreparacion.Barra;

    [ObservableProperty]
    private bool _manejaTamanos = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoCambiarEstado))]
    private bool _activa = true;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(CambiarEstadoCommand), nameof(EliminarCommand))]
    private bool _procesando;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoEliminar))]
    private bool _confirmandoEliminar;

    public string TextoEliminar => ConfirmandoEliminar ? "¿Seguro? Toca otra vez para eliminar" : "Eliminar categoría";

    public string TituloFormulario => EditandoId is null ? "NUEVA CATEGORÍA" : "EDITAR CATEGORÍA";

    public bool EsEdicion => EditandoId is not null;

    public string TextoCambiarEstado => Activa ? "Ocultar categoría" : "Volver a mostrar";

    partial void OnColorHexChanged(string value) => Opciones.Marcar(Colores, value);

    partial void OnEstacionChanged(EstacionPreparacion value) => Opciones.Marcar(Estaciones, value);

    public async Task CargarAsync()
    {
        try
        {
            var categorias = await _servicio.ObtenerCategoriasAsync();
            Categorias.Clear();
            foreach (var categoria in categorias)
            {
                Categorias.Add(new CategoriaAdminItem(categoria) { EstaSeleccionada = categoria.Id == EditandoId });
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron cargar las categorías: {ex.Message}", esError: true);
        }
    }

    [RelayCommand]
    private void Seleccionar(CategoriaAdminItem? item)
    {
        if (item is null)
        {
            return;
        }

        var c = item.Categoria;
        EditandoId = c.Id;
        Nombre = c.Nombre;
        ColorHex = c.ColorHex;
        Estacion = c.Estacion;
        ManejaTamanos = c.ManejaTamanos;
        Activa = c.Activa;
        Mensaje = null;
        ConfirmandoEliminar = false;

        foreach (var otro in Categorias)
        {
            otro.EstaSeleccionada = ReferenceEquals(otro, item);
        }
    }

    [RelayCommand]
    private void Nueva()
    {
        EditandoId = null;
        Nombre = string.Empty;
        ColorHex = Opciones.Paleta[0];
        Estacion = EstacionPreparacion.Barra;
        ManejaTamanos = true;
        Activa = true;
        Mensaje = null;
        ConfirmandoEliminar = false;
        Opciones.Marcar(Colores, ColorHex);
        Opciones.Marcar(Estaciones, Estacion);

        foreach (var item in Categorias)
        {
            item.EstaSeleccionada = false;
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
    private void ElegirEstacion(OpcionSeleccionable<EstacionPreparacion>? estacion)
    {
        if (estacion is not null)
        {
            Estacion = estacion.Valor;
        }
    }

    [RelayCommand]
    private void FijarTamanos(string? valor) => ManejaTamanos = valor == "si";

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task GuardarAsync()
    {
        Procesando = true;
        try
        {
            var cambiaTamanos = EditandoId is Guid id
                && Categorias.FirstOrDefault(c => c.Categoria.Id == id)?.Categoria.ManejaTamanos != ManejaTamanos;

            EditandoId = await _servicio.GuardarCategoriaAsync(
                new CategoriaEdicion(EditandoId, Nombre, ColorHex, Estacion, ManejaTamanos));

            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new CatalogoActualizadoMensaje());

            MostrarMensaje(
                cambiaTamanos
                    ? "Categoría guardada. Cambiaste los tamaños: revisa los precios de sus productos."
                    : "Categoría guardada.",
                esError: false);
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
    private async Task CambiarEstadoAsync()
    {
        if (EditandoId is not Guid id)
        {
            return;
        }

        Procesando = true;
        try
        {
            await _servicio.CambiarEstadoCategoriaAsync(id, !Activa);
            Activa = !Activa;
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new CatalogoActualizadoMensaje());
            MostrarMensaje(
                Activa ? "La categoría se muestra otra vez en venta." : "Categoría oculta: ni ella ni sus productos aparecen en venta.",
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
            await _servicio.EliminarCategoriaAsync(id);
            Nueva();
            await CargarAsync();
            WeakReferenceMessenger.Default.Send(new CatalogoActualizadoMensaje());
            MostrarMensaje($"Categoría {nombre} eliminada.", esError: false);
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

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }
}
