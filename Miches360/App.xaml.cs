using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Miches360.Aplicacion;
using Miches360.Infraestructura;
using Miches360.Infraestructura.Persistencia;
using Miches360.Servidor;
using Miches360.Servicios;
using Miches360.Servicios.Impresion;
using Miches360.ViewModels;
using Miches360.Vistas;

namespace Miches360;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += AlOcurrirErrorNoControlado;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("es-MX")));

        _ = Controles.CargandoMiches.PrecargarAsync();
        var carga = new VentanaCarga();
        carga.Show();
        var inicio = DateTime.UtcNow;

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory
        });

        var carpetaDatos = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Miches360");
        Directory.CreateDirectory(carpetaDatos);

        builder.Services.Configure<NegocioOpciones>(builder.Configuration.GetSection("Negocio"));
        builder.Services.AddInfraestructura(Path.Combine(carpetaDatos, "miches360.db"));
        builder.Services.AddAplicacion();
        builder.Services.Configure<ServidorOpciones>(builder.Configuration.GetSection("Servidor"));
        builder.Services.AddServidorLocal();

        builder.Services.AddSingleton<SesionAdministrador>();
        builder.Services.AddSingleton<IndicadorCarga>();
        builder.Services.AddSingleton<Navegador>();
        builder.Services.AddSingleton<ServicioImpresion>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<SelectorProductosViewModel>();
        builder.Services.AddSingleton<VentaViewModel>();
        builder.Services.AddTransient<AccesoAdminViewModel>();
        builder.Services.AddTransient<ProductosAdminViewModel>();
        builder.Services.AddTransient<CategoriasAdminViewModel>();
        builder.Services.AddTransient<MesasAdminViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        try
        {
            var servicios = _host.Services;
            SonidoAviso.Precargar();
            await Task.Run(() => InicializadorBaseDatos.InicializarAsync(servicios));
            await Task.Run(() => _host.StartAsync());

            var transcurrido = DateTime.UtcNow - inicio;
            var minimo = TimeSpan.FromMilliseconds(2400);
            if (transcurrido < minimo)
            {
                await Task.Delay(minimo - transcurrido);
            }
        }
        catch (Exception ex)
        {
            carga.Hide();
            MessageBox.Show(
                $"No se pudo preparar la base de datos.\n\n{ex.Message}\n\nCarpeta: {carpetaDatos}",
                "Miches 360",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        var ventana = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = ventana;
        ventana.Show();
        carga.Close();

        _host.Services.GetRequiredService<MainViewModel>().Iniciar();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }

        base.OnExit(e);
    }

    private void AlOcurrirErrorNoControlado(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ocurrió un error inesperado:\n\n{e.Exception.Message}",
            "Miches 360",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }
}
