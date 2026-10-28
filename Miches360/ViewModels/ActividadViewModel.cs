using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Aplicacion.Mesas;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public sealed partial class ActividadItem : ObservableObject
{
    public ActividadItem(Actividad actividad)
    {
        Actividad = actividad;
    }

    public Actividad Actividad { get; }

    public string Titulo => Actividad.Titulo;

    public string? Detalle => Actividad.Detalle;

    public bool TieneDetalle => !string.IsNullOrWhiteSpace(Actividad.Detalle);

    public string Hora => Actividad.Hora.ToString("HH:mm");

    public string Etiqueta => Actividad.Tipo switch
    {
        TipoActividad.Cobro => "COBRO",
        TipoActividad.OrdenEnviada => "ORDEN",
        TipoActividad.CuentaPedida => "CUENTA",
        TipoActividad.MesaAbierta => "MESA",
        _ => "CANCELADA"
    };

    public TipoActividad Tipo => Actividad.Tipo;

    public string Espera
    {
        get
        {
            var minutos = (int)Math.Max(0, (DateTime.Now - Actividad.Hora).TotalMinutes);
            return minutos switch
            {
                0 => "ahora",
                < 60 => $"hace {minutos} min",
                _ => Hora
            };
        }
    }

    public void ActualizarEspera() => OnPropertyChanged(nameof(Espera));
}

public sealed partial class ActividadViewModel : ObservableObject
{
    private const int Maximo = 60;

    private readonly Navegador _navegador;
    private readonly IMesasServicio _mesas;
    private readonly DispatcherTimer _reloj;

    public ActividadViewModel(Navegador navegador, IMesasServicio mesas)
    {
        _navegador = navegador;
        _mesas = mesas;

        _reloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _reloj.Tick += (_, _) =>
        {
            foreach (var item in Movimientos)
            {
                item.ActualizarEspera();
            }
        };
    }

    public ObservableCollection<ActividadItem> Movimientos { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMovimientos))]
    private ActividadItem? _ultimo;

    [ObservableProperty]
    private bool _panelAbierto;

    [ObservableProperty]
    private string _textoBoton = "Actividad";

    public bool HayMovimientos => Ultimo is not null;

    public void Agregar(Actividad actividad)
    {
        var item = new ActividadItem(actividad);
        Movimientos.Insert(0, item);
        while (Movimientos.Count > Maximo)
        {
            Movimientos.RemoveAt(Movimientos.Count - 1);
        }

        Ultimo = item;
        TextoBoton = $"Actividad · {Movimientos.Count}";
        _reloj.Start();
    }

    [RelayCommand]
    private void AlternarPanel() => PanelAbierto = !PanelAbierto && HayMovimientos;

    [RelayCommand]
    private void CerrarPanel() => PanelAbierto = false;

    [RelayCommand]
    private async Task AbrirAsync(ActividadItem? item)
    {
        item ??= Ultimo;
        if (item is null)
        {
            return;
        }

        PanelAbierto = false;
        var actividad = item.Actividad;

        if (actividad.Tipo is not (TipoActividad.Cobro or TipoActividad.VentaCancelada))
        {
            try
            {
                var mapa = await _mesas.ObtenerMapaAsync();
                if (mapa.Any(m => m.CuentaId == actividad.CuentaId))
                {
                    _navegador.IrACuentaMesa(actividad.CuentaId);
                    return;
                }
            }
            catch
            {
            }
        }

        _navegador.IrAHistorial(actividad.Hora.Date, actividad.CuentaId);
    }
}
