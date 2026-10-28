using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Miches360.ViewModels;

namespace Miches360.Servicios;

public sealed partial class Navegador : ObservableObject
{
    public const string SeccionVenta = "Venta";
    public const string SeccionMesas = "Mesas";
    public const string SeccionHistorial = "Historial";
    public const string SeccionCorte = "Corte";
    public const string SeccionCatalogo = "Catalogo";
    public const string SeccionAjustes = "Ajustes";

    private readonly IServiceProvider _servicios;
    private readonly SesionAdministrador _sesion;

    [ObservableProperty]
    private object? _vistaActual;

    [ObservableProperty]
    private string _seccionActual = SeccionVenta;

    public Navegador(IServiceProvider servicios, SesionAdministrador sesion)
    {
        _servicios = servicios;
        _sesion = sesion;
    }

    public void IrAVenta()
    {
        _sesion.Cerrar();
        var venta = _servicios.GetRequiredService<VentaViewModel>();
        SeccionActual = SeccionVenta;
        VistaActual = venta;
        _ = venta.InicializarAsync();
    }

    public void IrACobro(CobroContexto contexto)
    {
        _sesion.Cerrar();
        SeccionActual = SeccionVenta;
        VistaActual = ActivatorUtilities.CreateInstance<CobroViewModel>(_servicios, contexto);
    }

    public void IrAMesas(Guid? mesaId = null, string? mensaje = null, TicketCobrado? ticket = null)
    {
        _sesion.Cerrar();
        var mesas = ActivatorUtilities.CreateInstance<MesasViewModel>(_servicios);
        SeccionActual = SeccionMesas;
        VistaActual = mesas;
        _ = mesas.InicializarAsync(mesaId, mensaje, ticket);
    }

    public void IrAOrdenMesa(Guid cuentaId)
    {
        _sesion.Cerrar();
        var orden = ActivatorUtilities.CreateInstance<OrdenMesaViewModel>(_servicios, new OrdenMesaContexto(cuentaId));
        SeccionActual = SeccionMesas;
        VistaActual = orden;
        _ = orden.InicializarAsync();
    }

    public void IrACuentaMesa(Guid cuentaId, string? mensaje = null, TicketCobrado? ticket = null)
    {
        _sesion.Cerrar();
        var cuenta = ActivatorUtilities.CreateInstance<CuentaMesaViewModel>(_servicios, new CuentaMesaContexto(cuentaId, mensaje, ticket));
        SeccionActual = SeccionMesas;
        VistaActual = cuenta;
        _ = cuenta.InicializarAsync();
    }

    public void IrAPartesIguales(Guid cuentaId)
    {
        _sesion.Cerrar();
        var partes = ActivatorUtilities.CreateInstance<PartesIgualesViewModel>(_servicios, new PartesIgualesContexto(cuentaId));
        SeccionActual = SeccionMesas;
        VistaActual = partes;
        _ = partes.InicializarAsync();
    }

    public void IrAHistorial(DateTime? dia = null, Guid? seleccionar = null)
    {
        _sesion.Cerrar();
        var historial = ActivatorUtilities.CreateInstance<HistorialViewModel>(_servicios);
        SeccionActual = SeccionHistorial;
        VistaActual = historial;
        _ = historial.InicializarAsync(dia, seleccionar);
    }

    public void IrACorte()
    {
        _sesion.Cerrar();
        var corte = ActivatorUtilities.CreateInstance<CorteViewModel>(_servicios);
        SeccionActual = SeccionCorte;
        VistaActual = corte;
        _ = corte.InicializarAsync();
    }

    public void IrACatalogo()
    {
        var catalogo = ActivatorUtilities.CreateInstance<CatalogoAdminViewModel>(_servicios);
        SeccionActual = SeccionCatalogo;
        VistaActual = catalogo;
        _ = catalogo.InicializarAsync();
    }

    public void IrAAjustes()
    {
        var ajustes = ActivatorUtilities.CreateInstance<AjustesViewModel>(_servicios);
        SeccionActual = SeccionAjustes;
        VistaActual = ajustes;
        _ = ajustes.InicializarAsync();
    }

    public void IrAEnConstruccion(string seccion, string titulo, string descripcion)
    {
        _sesion.Cerrar();
        SeccionActual = seccion;
        VistaActual = new EnConstruccionViewModel(titulo, descripcion);
    }
}
