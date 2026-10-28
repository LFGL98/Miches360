using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Miches360.Aplicacion.Ajustes;
using Miches360.Aplicacion.Catalogo;
using Miches360.Aplicacion.Dispositivos;
using Miches360.Aplicacion.Mesas;
using Miches360.Aplicacion.Notificaciones;
using Miches360.Aplicacion.Ventas;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Servidor;

public sealed class ServidorLocal : IHostedService, IAsyncDisposable
{
    private const string ClaveSesion = "Miches360.Sesion";

    private readonly IServiceProvider _servicios;
    private readonly ServidorOpciones _opciones;
    private readonly CentralCambios _cambios;
    private readonly IDispositivosServicio _dispositivos;
    private WebApplication? _web;
    private IHubContext<EventosHub>? _hub;

    public ServidorLocal(IServiceProvider servicios, IOptions<ServidorOpciones> opciones, CentralCambios cambios, IDispositivosServicio dispositivos)
    {
        _servicios = servicios;
        _opciones = opciones.Value;
        _cambios = cambios;
        _dispositivos = dispositivos;
    }

    public bool Activo { get; private set; }

    public string Estado { get; private set; } = "Sin iniciar";

    public int Puerto => _opciones.Puerto;

    public IReadOnlyList<string> Direcciones => ObtenerIps().Select(ip => $"{ip}:{Puerto}").ToList();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_opciones.Habilitado)
        {
            Estado = "Apagado (Servidor:Habilitado = false en appsettings.json)";
            return;
        }

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory
            });

            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenAnyIP(_opciones.Puerto));
            builder.Services.ConfigureHttpJsonOptions(json =>
            {
                json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
                json.SerializerOptions.Converters.Add(new FechaUtcConverter());
            });
            builder.Services.AddSignalR().AddJsonProtocol(json =>
                json.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            builder.Services.AddSingleton(_dispositivos);

            _web = builder.Build();
            MapearRutas(_web);
            _web.MapHub<EventosHub>("/eventos");
            _hub = _web.Services.GetRequiredService<IHubContext<EventosHub>>();

            await _web.StartAsync(cancellationToken);
            _cambios.Cambio += AlCambiar;

            Activo = true;
            Estado = $"Encendido en el puerto {_opciones.Puerto}";
        }
        catch (Exception ex)
        {
            Activo = false;
            Estado = ex is IOException or SocketException
                ? $"El puerto {_opciones.Puerto} está ocupado por otro programa. Cámbialo en appsettings.json."
                : $"No se pudo encender: {ex.Message}";

            if (_web is not null)
            {
                await _web.DisposeAsync();
                _web = null;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cambios.Cambio -= AlCambiar;
        Activo = false;

        if (_web is not null)
        {
            await _web.StopAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cambios.Cambio -= AlCambiar;
        if (_web is not null)
        {
            await _web.DisposeAsync();
            _web = null;
        }
    }

    private void AlCambiar(CambioRealizado cambio)
    {
        if (_hub is { } hub)
        {
            _ = hub.Clients.All.SendAsync("Cambio", cambio.Tipo.ToString(), cambio.Mensaje);
        }
    }

    private void MapearRutas(WebApplication web)
    {
        var catalogo = _servicios.GetRequiredService<ICatalogoServicio>();
        var mesas = _servicios.GetRequiredService<IMesasServicio>();
        var ajustes = _servicios.GetRequiredService<IAjustesServicio>();

        var publico = web.MapGroup("/api").AddEndpointFilter(ManejarErroresAsync);

        publico.MapGet("/salud", () =>
            Results.Ok(new SaludRespuesta("Miches360", 1, DateTime.Now.ToString("HH:mm:ss"))));

        publico.MapPost("/vincular", async (VincularPeticion peticion, CancellationToken ct) =>
            Results.Ok(await _dispositivos.VincularAsync(peticion.Codigo, peticion.Mesero, peticion.Modelo, ct)));

        var api = web.MapGroup("/api")
            .AddEndpointFilter(ManejarErroresAsync)
            .AddEndpointFilter(AutenticarAsync);

        api.MapGet("/yo", (HttpContext http) => Results.Ok(Sesion(http)));

        api.MapGet("/catalogo", async (CancellationToken ct) =>
            Results.Ok(await catalogo.ObtenerCatalogoVentaAsync(ct)));

        api.MapGet("/mesas", async (CancellationToken ct) =>
            Results.Ok(await mesas.ObtenerMapaAsync(ct)));

        api.MapGet("/cuentas/{id:guid}", async (Guid id, CancellationToken ct) =>
            Results.Ok(await mesas.ObtenerCuentaAsync(id, ct)));

        api.MapPost("/mesas/{mesaId:guid}/abrir", async (Guid mesaId, AbrirMesaPeticion peticion, HttpContext http, CancellationToken ct) =>
        {
            var id = await mesas.AbrirMesaAsync(mesaId, peticion.Tipo, peticion.Personas, Sesion(http).Mesero, ct);
            return Results.Ok(new IdRespuesta(id));
        });

        api.MapPost("/cuentas/{id:guid}/renglones", async (Guid id, List<RenglonMesaSolicitud> renglones, HttpContext http, CancellationToken ct) =>
        {
            await mesas.AgregarRenglonesAsync(id, renglones, ct);

            var cuenta = await mesas.ObtenerCuentaAsync(id, ct);
            var menu = await catalogo.ObtenerCatalogoVentaAsync(ct);
            var productosMenu = menu.Productos.ToDictionary(p => p.Id);
            var variasPersonas = cuenta.Personas.Count > 1;

            var enviados = renglones
                .Select(r =>
                {
                    productosMenu.TryGetValue(r.ProductoId, out var producto);
                    var precio = producto is not null && producto.Precios.TryGetValue(r.Tamano, out var precioTamano) ? precioTamano : 0m;
                    var persona = variasPersonas
                        ? cuenta.Personas.FirstOrDefault(x => x.SubcuentaId == r.SubcuentaId)?.Nombre
                        : null;
                    return new ProductoEnviado(
                        r.Cantidad,
                        producto?.Nombre ?? "Producto",
                        r.Tamano == Tamano.Unico ? null : r.Tamano.ToString(),
                        persona,
                        string.IsNullOrWhiteSpace(r.Nota) ? null : r.Nota.Trim(),
                        precio * r.Cantidad);
                })
                .ToList();

            _cambios.AvisarOrdenEnviada(new OrdenEnviada(id, cuenta.MesaNombre, Sesion(http).Mesero, DateTime.Now, enviados));
            return Results.NoContent();
        });

        api.MapPost("/cuentas/{id:guid}/personas", async (Guid id, NombrePeticion peticion, CancellationToken ct) =>
            Results.Ok(new IdRespuesta(await mesas.AgregarPersonaAsync(id, peticion.Nombre, ct))));

        api.MapPut("/cuentas/{id:guid}/personas/{subcuentaId:guid}", async (Guid id, Guid subcuentaId, NombrePeticion peticion, CancellationToken ct) =>
        {
            await mesas.RenombrarPersonaAsync(id, subcuentaId, peticion.Nombre ?? string.Empty, ct);
            return Results.NoContent();
        });

        api.MapDelete("/cuentas/{id:guid}/personas/{subcuentaId:guid}", async (Guid id, Guid subcuentaId, CancellationToken ct) =>
        {
            await mesas.QuitarPersonaAsync(id, subcuentaId, ct);
            return Results.NoContent();
        });

        api.MapPost("/cuentas/{id:guid}/renglones/{renglonId:guid}/mover", async (Guid id, Guid renglonId, MoverRenglonPeticion peticion, CancellationToken ct) =>
        {
            await mesas.MoverRenglonAsync(id, renglonId, peticion.SubcuentaDestinoId, ct);
            return Results.NoContent();
        });

        api.MapGet("/transferencia", async (CancellationToken ct) =>
            Results.Ok(await ajustes.ObtenerDatosTransferenciaAsync(ct)));

        api.MapPost("/cuentas/{id:guid}/cobrar", async (Guid id, CobrarPeticion peticion, HttpContext http, CancellationToken ct) =>
        {
            var mesero = Sesion(http).Mesero;
            var cuenta = await mesas.ObtenerCuentaAsync(id, ct);
            var resultado = await mesas.CobrarAsync(
                new CobroMesaSolicitud(id, peticion.SubcuentaIds ?? new List<Guid>(), peticion.Pagos ?? new List<PagoSolicitud>(), peticion.EfectivoRecibido, mesero),
                ct);

            var efectivo = (peticion.Pagos ?? new List<PagoSolicitud>()).Where(p => p.Metodo == MetodoPago.Efectivo).Sum(p => p.Monto);
            var transferencia = resultado.Total - efectivo;
            var forma = (efectivo > 0, transferencia > 0) switch
            {
                (true, true) => $"efectivo {Dinero(efectivo)} + transferencia {Dinero(transferencia)}",
                (true, false) => "efectivo",
                _ => "transferencia"
            };
            var estado = resultado.CuentaCerrada ? " · mesa libre" : string.Empty;
            _cambios.Notificar(TipoCambio.CobroMesero, $"{cuenta.MesaNombre}: {mesero} cobró {Dinero(resultado.Total)} ({forma}){estado}");

            return Results.Ok(resultado);
        });

        api.MapPost("/cuentas/{id:guid}/pedir-cuenta", async (Guid id, HttpContext http, CancellationToken ct) =>
        {
            var cuenta = await mesas.ObtenerCuentaAsync(id, ct);
            _cambios.AvisarCuentaPedida(new CuentaPedida(
                cuenta.CuentaId,
                cuenta.MesaNombre,
                cuenta.Pendiente,
                cuenta.Personas.Count(p => !p.Pagada),
                Sesion(http).Mesero,
                DateTime.Now));
            return Results.NoContent();
        });
    }

    private static string Dinero(decimal valor) =>
        valor.ToString(valor % 1 == 0 ? "$#,0" : "$#,0.00", System.Globalization.CultureInfo.GetCultureInfo("es-MX"));

    private static SesionDispositivo Sesion(HttpContext http) =>
        http.Items[ClaveSesion] as SesionDispositivo
        ?? throw new InvalidOperationException("Petición sin sesión.");

    private async ValueTask<object?> AutenticarAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var encabezado = contexto.HttpContext.Request.Headers.Authorization.ToString();
        var token = encabezado.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? encabezado[7..] : null;

        var sesion = await _dispositivos.ValidarAsync(token, contexto.HttpContext.RequestAborted);
        if (sesion is null)
        {
            return Results.Json(
                new ErrorRespuesta("Este celular ya no está vinculado a la caja. Vincúlalo otra vez."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        contexto.HttpContext.Items[ClaveSesion] = sesion;
        return await siguiente(contexto);
    }

    private static async ValueTask<object?> ManejarErroresAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        try
        {
            return await siguiente(contexto);
        }
        catch (ReglaNegocioException ex)
        {
            return Results.Json(new ErrorRespuesta(ex.Message), statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.Json(new ErrorRespuesta($"Error en la caja: {ex.Message}"), statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static IEnumerable<string> ObtenerIps()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork
                             && !IPAddress.IsLoopback(ip)
                             && !ip.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                .Select(ip => ip.ToString())
                .Distinct()
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public sealed class EventosHub : Hub
{
    private readonly IDispositivosServicio _dispositivos;

    public EventosHub(IDispositivosServicio dispositivos)
    {
        _dispositivos = dispositivos;
    }

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var encabezado = http?.Request.Headers.Authorization.ToString() ?? string.Empty;
        var token = encabezado.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? encabezado[7..]
            : http?.Request.Query["access_token"].ToString();
        if (await _dispositivos.ValidarAsync(token) is null)
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddServidorLocal(this IServiceCollection services)
    {
        services.AddSingleton<ServidorLocal>();
        services.AddHostedService(sp => sp.GetRequiredService<ServidorLocal>());
        return services;
    }
}
