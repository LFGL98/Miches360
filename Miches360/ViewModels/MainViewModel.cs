using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Mesas;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Mensajes;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IMesasServicio _mesas;

    public MainViewModel(Navegador navegador, IOptions<NegocioOpciones> negocio, CentralCambios cambios, IMesasServicio mesas, IndicadorCarga carga)
    {
        Navegador = navegador;
        Carga = carga;
        _mesas = mesas;
        Avisos = new AvisosMesasViewModel(navegador);
        BarraActividad = new ActividadViewModel(navegador, mesas);
        NombreCaja = negocio.Value.NombreCaja;

        Secciones = new ObservableCollection<SeccionItem>
        {
            new(Navegador.SeccionVenta, "Venta"),
            new(Navegador.SeccionMesas, "Mesas"),
            new(Navegador.SeccionHistorial, "Historial"),
            new(Navegador.SeccionCorte, "Corte de caja"),
            new(Navegador.SeccionCatalogo, "Catálogo"),
            new(Navegador.SeccionAjustes, "Ajustes")
        };

        Navegador.PropertyChanged += AlCambiarNavegador;
        MarcarSeccion(Navegador.SeccionActual);

        WeakReferenceMessenger.Default.Register<MainViewModel, AvisoMensaje>(this, static (vm, mensaje) => vm.MostrarAviso(mensaje));

        cambios.Cambio += cambio => System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => AlCambiarDatos(cambio));
    }

    private void AlCambiarDatos(CambioRealizado cambio)
    {
        switch (cambio.Tipo)
        {
            case TipoCambio.Aviso when !string.IsNullOrWhiteSpace(cambio.Mensaje):
                MostrarAviso(new AvisoMensaje(cambio.Mensaje, EsError: false));
                break;
            case TipoCambio.CobroMesero when !string.IsNullOrWhiteSpace(cambio.Mensaje):
                MostrarAviso(new AvisoMensaje(cambio.Mensaje, EsError: false));
                SonidoAviso.TocarCobro();
                break;
            case TipoCambio.CuentaPedida when cambio.Cuenta is not null:
                Avisos.Agregar(cambio.Cuenta);
                break;
            case TipoCambio.OrdenEnviada when cambio.Orden is not null:
                Avisos.Agregar(cambio.Orden);
                break;
            case TipoCambio.Actividad when cambio.Actividad is not null:
                BarraActividad.Agregar(cambio.Actividad);
                break;
            case TipoCambio.Mesas:
                if (Navegador.VistaActual is MesasViewModel mesas)
                {
                    _ = mesas.RefrescarAsync();
                }
                else if (Navegador.VistaActual is CuentaMesaViewModel cuenta)
                {
                    _ = cuenta.RefrescarAsync();
                }

                _ = QuitarAvisosCerradosAsync();
                break;
        }
    }

    public IndicadorCarga Carga { get; }

    public AvisosMesasViewModel Avisos { get; }

    public ActividadViewModel BarraActividad { get; }

    private async Task QuitarAvisosCerradosAsync()
    {
        if (!Avisos.HayAvisos)
        {
            return;
        }

        try
        {
            var mapa = await _mesas.ObtenerMapaAsync();
            var abiertas = mapa.Where(m => m.CuentaId is not null).Select(m => m.CuentaId!.Value).ToHashSet();
            Avisos.QuitarCerradas(abiertas);
        }
        catch
        {
        }
    }

    private int _versionAviso;

    [ObservableProperty]
    private string? _aviso;

    [ObservableProperty]
    private bool _avisoEsError;

    [RelayCommand]
    private void CerrarAviso() => Aviso = null;

    private async void MostrarAviso(AvisoMensaje mensaje)
    {
        var version = ++_versionAviso;
        AvisoEsError = mensaje.EsError;
        Aviso = mensaje.Texto;

        await Task.Delay(mensaje.EsError ? 8000 : 4000);
        if (version == _versionAviso)
        {
            Aviso = null;
        }
    }

    public Navegador Navegador { get; }

    public string NombreCaja { get; }

    public ObservableCollection<SeccionItem> Secciones { get; }

    public void Iniciar() => Navegador.IrAVenta();

    [RelayCommand]
    private void Navegar(SeccionItem? seccion)
    {
        switch (seccion?.Clave)
        {
            case Navegador.SeccionVenta:
                Navegador.IrAVenta();
                break;
            case Navegador.SeccionMesas:
                Navegador.IrAMesas();
                break;
            case Navegador.SeccionHistorial:
                Navegador.IrAHistorial();
                break;
            case Navegador.SeccionCorte:
                Navegador.IrACorte();
                break;
            case Navegador.SeccionCatalogo:
                Navegador.IrACatalogo();
                break;
            case Navegador.SeccionAjustes:
                Navegador.IrAAjustes();
                break;
        }
    }

    private void AlCambiarNavegador(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Navegador.SeccionActual))
        {
            MarcarSeccion(Navegador.SeccionActual);
        }
    }

    private void MarcarSeccion(string clave)
    {
        foreach (var seccion in Secciones)
        {
            seccion.EstaSeleccionada = seccion.Clave == clave;
        }
    }
}
