using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public enum TipoAvisoMesa
{
    PideCuenta,
    NuevaOrden
}

public sealed record ProductoAvisoItem(string Texto, string? Detalle, string Importe)
{
    public bool TieneDetalle => !string.IsNullOrWhiteSpace(Detalle);
}

public sealed partial class AvisoMesaItem : ObservableObject
{
    private OrdenEnviada? _orden;

    public AvisoMesaItem(CuentaPedida cuenta)
    {
        Tipo = TipoAvisoMesa.PideCuenta;
        Cambiar(cuenta);
    }

    public AvisoMesaItem(OrdenEnviada orden)
    {
        Tipo = TipoAvisoMesa.NuevaOrden;
        Agregar(orden);
    }

    public TipoAvisoMesa Tipo { get; }

    public bool EsCuenta => Tipo == TipoAvisoMesa.PideCuenta;

    public bool EsOrden => Tipo == TipoAvisoMesa.NuevaOrden;

    public string TituloBanda => EsOrden ? "NUEVA ORDEN" : "PIDEN LA CUENTA";

    public Guid CuentaId { get; private set; }

    public ObservableCollection<ProductoAvisoItem> Productos { get; } = new();

    [ObservableProperty]
    private string _mesa = string.Empty;

    [ObservableProperty]
    private string _importe = string.Empty;

    [ObservableProperty]
    private string _detalle = string.Empty;

    [ObservableProperty]
    private string _resumen = string.Empty;

    [ObservableProperty]
    private DateTime _hora;

    public string EtiquetaImporte => EsOrden ? "Total de la orden" : "Pendiente";

    public string Espera
    {
        get
        {
            var minutos = (int)Math.Max(0, (DateTime.Now - Hora).TotalMinutes);
            return minutos == 0 ? "ahora" : $"hace {minutos} min";
        }
    }

    public void Cambiar(CuentaPedida cuenta)
    {
        CuentaId = cuenta.CuentaId;
        Mesa = cuenta.Mesa;
        Hora = cuenta.Hora;
        Importe = Formato.Dinero(cuenta.Pendiente);
        var personas = cuenta.PersonasPorPagar > 1 ? $"{cuenta.PersonasPorPagar} personas por pagar · " : string.Empty;
        Detalle = $"{personas}Avisó {cuenta.Mesero} a las {cuenta.Hora:HH:mm}";
        Resumen = $"{Mesa} · {Importe}";
        ActualizarEspera();
    }

    public void Agregar(OrdenEnviada orden)
    {
        _orden = _orden is null ? orden : _orden with { Hora = orden.Hora, Productos = _orden.Productos.Concat(orden.Productos).ToList() };
        CuentaId = orden.CuentaId;
        Mesa = orden.Mesa;
        Hora = orden.Hora;

        foreach (var producto in orden.Productos)
        {
            var tamano = string.IsNullOrWhiteSpace(producto.Tamano) ? string.Empty : $" {producto.Tamano}";
            var partes = new[] { producto.Persona, producto.Nota is null ? null : $"Nota: {producto.Nota}" }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            Productos.Add(new ProductoAvisoItem(
                $"{producto.Cantidad}× {producto.Producto}{tamano}",
                string.Join(" · ", partes),
                Formato.Dinero(producto.Importe)));
        }

        Importe = Formato.Dinero(_orden.Total);
        var piezas = _orden.Piezas;
        Detalle = $"{piezas} producto{(piezas == 1 ? string.Empty : "s")} · envió {orden.Mesero} a las {orden.Hora:HH:mm}";
        Resumen = $"{Mesa} · {piezas} prod.";
        ActualizarEspera();
    }

    public void ActualizarEspera() => OnPropertyChanged(nameof(Espera));
}

public sealed partial class AvisosMesasViewModel : ObservableObject
{
    private readonly Navegador _navegador;
    private readonly List<AvisoMesaItem> _cola = new();
    private readonly DispatcherTimer _reloj;

    public AvisosMesasViewModel(Navegador navegador)
    {
        _navegador = navegador;

        _reloj = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _reloj.Tick += (_, _) =>
        {
            foreach (var item in _cola.Concat(Minimizadas))
            {
                item.ActualizarEspera();
            }

            if (Actual is { } actual)
            {
                Sonar(actual.Tipo);
            }
        };
    }

    public ObservableCollection<AvisoMesaItem> Minimizadas { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarModal))]
    private AvisoMesaItem? _actual;

    [ObservableProperty]
    private string _textoEnCola = string.Empty;

    [ObservableProperty]
    private bool _hayMasEnCola;

    [ObservableProperty]
    private bool _hayMinimizadas;

    [ObservableProperty]
    private string _textoMinimizadas = string.Empty;

    [ObservableProperty]
    private bool _listaAbierta;

    public bool MostrarModal => Actual is not null;

    public bool HayAvisos => _cola.Count > 0 || Minimizadas.Count > 0;

    public void Agregar(CuentaPedida cuenta)
    {
        var existente = Buscar(TipoAvisoMesa.PideCuenta, cuenta.CuentaId);
        if (existente is not null)
        {
            existente.Cambiar(cuenta);
            Traer(existente);
        }
        else
        {
            _cola.Add(new AvisoMesaItem(cuenta));
        }

        Sonar(TipoAvisoMesa.PideCuenta);
        Actualizar();
    }

    public void Agregar(OrdenEnviada orden)
    {
        if (orden.Productos.Count == 0)
        {
            return;
        }

        var existente = Buscar(TipoAvisoMesa.NuevaOrden, orden.CuentaId);
        if (existente is not null)
        {
            existente.Agregar(orden);
            Traer(existente);
        }
        else
        {
            _cola.Add(new AvisoMesaItem(orden));
        }

        Sonar(TipoAvisoMesa.NuevaOrden);
        Actualizar();
    }

    public void QuitarCerradas(IReadOnlySet<Guid> cuentasAbiertas)
    {
        _cola.RemoveAll(i => !cuentasAbiertas.Contains(i.CuentaId));
        foreach (var item in Minimizadas.Where(i => !cuentasAbiertas.Contains(i.CuentaId)).ToList())
        {
            Minimizadas.Remove(item);
        }

        Actualizar();
    }

    [RelayCommand]
    private void IrAMesa(AvisoMesaItem? item)
    {
        item ??= Actual;
        if (item is null)
        {
            return;
        }

        Quitar(item);
        _navegador.IrACuentaMesa(item.CuentaId);
    }

    [RelayCommand]
    private void Enterado()
    {
        if (Actual is { } item)
        {
            Quitar(item);
        }
    }

    [RelayCommand]
    private void Minimizar()
    {
        if (Actual is not { } item)
        {
            return;
        }

        _cola.Remove(item);
        Minimizadas.Add(item);
        Actualizar();
    }

    [RelayCommand]
    private void MinimizarTodas()
    {
        foreach (var item in _cola)
        {
            Minimizadas.Add(item);
        }

        _cola.Clear();
        Actualizar();
    }

    [RelayCommand]
    private void AlternarLista() => ListaAbierta = !ListaAbierta && HayMinimizadas;

    [RelayCommand]
    private void Restaurar(AvisoMesaItem? item)
    {
        if (item is null || !Minimizadas.Remove(item))
        {
            return;
        }

        _cola.Insert(0, item);
        ListaAbierta = false;
        Actualizar();
    }

    [RelayCommand]
    private void RestaurarTodas()
    {
        _cola.InsertRange(0, Minimizadas);
        Minimizadas.Clear();
        ListaAbierta = false;
        Actualizar();
    }

    [RelayCommand]
    private void Descartar(AvisoMesaItem? item)
    {
        if (item is not null)
        {
            Quitar(item);
        }
    }

    private static void Sonar(TipoAvisoMesa tipo)
    {
        if (tipo == TipoAvisoMesa.NuevaOrden)
        {
            SonidoAviso.TocarOrden();
        }
        else
        {
            SonidoAviso.TocarCuenta();
        }
    }

    private AvisoMesaItem? Buscar(TipoAvisoMesa tipo, Guid cuentaId) =>
        _cola.Concat(Minimizadas).FirstOrDefault(i => i.Tipo == tipo && i.CuentaId == cuentaId);

    private void Traer(AvisoMesaItem item)
    {
        if (Minimizadas.Remove(item))
        {
            _cola.Add(item);
        }
    }

    private void Quitar(AvisoMesaItem item)
    {
        _cola.Remove(item);
        Minimizadas.Remove(item);
        Actualizar();
    }

    private void Actualizar()
    {
        Actual = _cola.FirstOrDefault();
        var restantes = _cola.Count - 1;
        HayMasEnCola = restantes > 0;
        TextoEnCola = restantes switch
        {
            <= 0 => string.Empty,
            1 => "Hay 1 aviso más esperando",
            _ => $"Hay {restantes} avisos más esperando"
        };

        HayMinimizadas = Minimizadas.Count > 0;
        var ordenes = Minimizadas.Count(i => i.EsOrden);
        var cuentas = Minimizadas.Count - ordenes;
        TextoMinimizadas = Minimizadas.Count == 1
            ? Minimizadas[0].Resumen
            : string.Join(" · ", new[]
            {
                ordenes switch { 0 => null, 1 => "1 orden", _ => $"{ordenes} órdenes" },
                cuentas switch { 0 => null, 1 => "1 cuenta", _ => $"{cuentas} cuentas" }
            }.Where(t => t is not null));

        if (!HayMinimizadas)
        {
            ListaAbierta = false;
        }

        OnPropertyChanged(nameof(HayAvisos));

        if (HayAvisos)
        {
            _reloj.Start();
        }
        else
        {
            _reloj.Stop();
        }
    }
}
