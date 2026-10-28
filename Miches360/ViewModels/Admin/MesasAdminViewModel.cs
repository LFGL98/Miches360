using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Aplicacion.Mesas;
using Miches360.Dominio.Comun;

namespace Miches360.ViewModels;

public sealed partial class MesasAdminViewModel : ObservableObject
{
    private readonly IMesasAdminServicio _servicio;

    public MesasAdminViewModel(IMesasAdminServicio servicio)
    {
        _servicio = servicio;
    }

    public ObservableCollection<MesaAdminItem> Mesas { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloFormulario), nameof(EsEdicion))]
    private Guid? _editandoId;

    [ObservableProperty]
    private string _nombre = string.Empty;

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

    public string TextoEliminar => ConfirmandoEliminar ? "¿Seguro? Toca otra vez para eliminar" : "Eliminar mesa";

    public string TituloFormulario => EditandoId is null ? "NUEVA MESA" : "EDITAR MESA";

    public bool EsEdicion => EditandoId is not null;

    public string TextoCambiarEstado => Activa ? "Quitar mesa" : "Volver a usar";

    public async Task CargarAsync()
    {
        try
        {
            var mesas = await _servicio.ObtenerMesasAsync();
            Mesas.Clear();
            foreach (var mesa in mesas)
            {
                Mesas.Add(new MesaAdminItem(mesa) { EstaSeleccionada = mesa.Id == EditandoId });
            }

            if (EditandoId is null && string.IsNullOrWhiteSpace(Nombre))
            {
                Nueva();
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudieron cargar las mesas: {ex.Message}", esError: true);
        }
    }

    [RelayCommand]
    private void Seleccionar(MesaAdminItem? item)
    {
        if (item is null)
        {
            return;
        }

        EditandoId = item.Mesa.Id;
        Nombre = item.Mesa.Nombre;
        Activa = item.Mesa.Activa;
        Mensaje = null;
        ConfirmandoEliminar = false;

        foreach (var otra in Mesas)
        {
            otra.EstaSeleccionada = ReferenceEquals(otra, item);
        }
    }

    [RelayCommand]
    private void Nueva()
    {
        var numero = 1;
        while (Mesas.Any(m => m.Mesa.Activa && string.Equals(m.Mesa.Nombre, $"Mesa {numero}", StringComparison.OrdinalIgnoreCase)))
        {
            numero++;
        }

        EditandoId = null;
        Nombre = $"Mesa {numero}";
        Activa = true;
        Mensaje = null;
        ConfirmandoEliminar = false;

        foreach (var mesa in Mesas)
        {
            mesa.EstaSeleccionada = false;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task GuardarAsync()
    {
        Procesando = true;
        try
        {
            var esNueva = EditandoId is null;
            var nombre = Nombre.Trim();
            await _servicio.GuardarMesaAsync(EditandoId, nombre);

            if (esNueva)
            {
                EditandoId = null;
                Nombre = string.Empty;
                await CargarAsync();
                MostrarMensaje($"{nombre} agregada. Ya puedes capturar la siguiente.", esError: false);
            }
            else
            {
                await CargarAsync();
                MostrarMensaje("Mesa guardada.", esError: false);
            }
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
            await _servicio.CambiarEstadoMesaAsync(id, !Activa);
            Activa = !Activa;
            await CargarAsync();
            MostrarMensaje(Activa ? "La mesa vuelve a aparecer en el mapa." : "Mesa quitada del mapa.", esError: false);
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensaje(ex.Message, esError: true);
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
            await _servicio.EliminarMesaAsync(id);
            EditandoId = null;
            Nombre = string.Empty;
            await CargarAsync();
            MostrarMensaje($"{nombre} eliminada. Sus ventas anteriores se conservan.", esError: false);
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
