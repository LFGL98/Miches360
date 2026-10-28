using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Miches360.Controles;

public partial class CargandoMiches : UserControl
{
    private const int Lado = 340;
    private const int Columnas = 8;
    private const int Cuadros = 57;
    private static readonly TimeSpan PorCuadro = TimeSpan.FromMilliseconds(80);

    private static Task<BitmapSource?>? _hoja;

    public static readonly DependencyProperty TextoProperty = DependencyProperty.Register(
        nameof(Texto),
        typeof(string),
        typeof(CargandoMiches),
        new PropertyMetadata(string.Empty, (d, e) => ((CargandoMiches)d).Etiqueta.Text = e.NewValue as string ?? string.Empty));

    public static readonly DependencyProperty TamanoProperty = DependencyProperty.Register(
        nameof(Tamano),
        typeof(double),
        typeof(CargandoMiches),
        new PropertyMetadata(260d, (d, e) =>
        {
            var control = (CargandoMiches)d;
            control.Marco.Width = (double)e.NewValue;
            control.Marco.Height = (double)e.NewValue;
        }));

    private readonly Storyboard _puntos;
    private Storyboard? _animacion;
    private bool _corriendo;

    public CargandoMiches()
    {
        InitializeComponent();
        _puntos = (Storyboard)Resources["Puntos"];

        IsVisibleChanged += (_, _) => _ = ActualizarAsync();
        Loaded += (_, _) => _ = ActualizarAsync();
        Unloaded += (_, _) => Detener();
    }

    public string Texto
    {
        get => (string)GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    public double Tamano
    {
        get => (double)GetValue(TamanoProperty);
        set => SetValue(TamanoProperty, value);
    }

    public static Task<BitmapSource?> PrecargarAsync()
    {
        if (_hoja is not null)
        {
            return _hoja;
        }

        Stream? flujo = null;
        try
        {
            flujo = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/cargando_cuadros.jpg"))?.Stream;
        }
        catch
        {
        }

        _hoja = flujo is null
            ? Task.FromResult<BitmapSource?>(null)
            : Task.Run<BitmapSource?>(() =>
            {
                try
                {
                    var imagen = new BitmapImage();
                    imagen.BeginInit();
                    imagen.CacheOption = BitmapCacheOption.OnLoad;
                    imagen.StreamSource = flujo;
                    imagen.EndInit();
                    imagen.Freeze();
                    return imagen;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    flujo.Dispose();
                }
            });

        return _hoja;
    }

    private async Task ActualizarAsync()
    {
        if (!IsVisible || !IsLoaded)
        {
            Detener();
            return;
        }

        if (_corriendo)
        {
            return;
        }

        _corriendo = true;
        _puntos.Begin(this, isControllable: true);

        if (_animacion is null)
        {
            var hoja = await PrecargarAsync();
            if (hoja is null || !_corriendo)
            {
                return;
            }

            Hoja.Source = hoja;
            Hoja.Width = hoja.PixelWidth;
            Hoja.Height = hoja.PixelHeight;
            _animacion = CrearAnimacion();
        }

        _animacion.Begin(this, isControllable: true);
    }

    private void Detener()
    {
        if (!_corriendo)
        {
            return;
        }

        _corriendo = false;
        _puntos.Stop(this);
        _animacion?.Stop(this);
    }

    private Storyboard CrearAnimacion()
    {
        var x = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromTicks(PorCuadro.Ticks * Cuadros) };
        var y = new DoubleAnimationUsingKeyFrames { Duration = x.Duration };

        for (var i = 0; i < Cuadros; i++)
        {
            var tiempo = KeyTime.FromTimeSpan(TimeSpan.FromTicks(PorCuadro.Ticks * i));
            x.KeyFrames.Add(new DiscreteDoubleKeyFrame(-(i % Columnas) * Lado, tiempo));
            y.KeyFrames.Add(new DiscreteDoubleKeyFrame(-(i / Columnas) * Lado, tiempo));
        }

        Storyboard.SetTargetName(x, nameof(Desplazamiento));
        Storyboard.SetTargetProperty(x, new PropertyPath(System.Windows.Media.TranslateTransform.XProperty));
        Storyboard.SetTargetName(y, nameof(Desplazamiento));
        Storyboard.SetTargetProperty(y, new PropertyPath(System.Windows.Media.TranslateTransform.YProperty));

        var animacion = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        animacion.Children.Add(x);
        animacion.Children.Add(y);
        return animacion;
    }
}
