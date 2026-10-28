using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Miches360.Servicios.Impresion;

internal static class Marca
{
    public static readonly FontFamily FuenteTitulo = new("Bahnschrift SemiBold Condensed, Bahnschrift, Segoe UI");
    public static readonly FontFamily FuenteTexto = new("Segoe UI");

    public static readonly Brush Encabezado = Pincel(0x2A, 0x1A, 0x14);
    public static readonly Brush Acento = Pincel(0xE1, 0xAF, 0x5B);
    public static readonly Brush Terracota = Pincel(0xA8, 0x51, 0x2E);
    public static readonly Brush Crema = Pincel(0xF6, 0xE9, 0xD2);
    public static readonly Brush CremaClara = Pincel(0xFF, 0xF8, 0xEC);
    public static readonly Brush LineaCrema = Pincel(0xE2, 0xCF, 0xAE);
    public static readonly Brush TextoTicket = Pincel(0x26, 0x16, 0x12);
    public static readonly Brush TextoTicketSuave = Pincel(0x6B, 0x52, 0x40);
    public static readonly Brush TextoSuaveOscuro = Pincel(0xCD, 0xB7, 0x9A);

    private static readonly Lazy<ImageSource?> LogoColor = new(() => CargarLogo(grises: false));
    private static readonly Lazy<ImageSource?> LogoGrises = new(() => CargarLogo(grises: true));

    public static ImageSource? Logo => LogoColor.Value;

    public static ImageSource? LogoTermico => LogoGrises.Value;

    public static SolidColorBrush Pincel(string hex)
    {
        try
        {
            var pincel = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            pincel.Freeze();
            return pincel;
        }
        catch (FormatException)
        {
            return (SolidColorBrush)TextoTicketSuave;
        }
    }

    private static SolidColorBrush Pincel(byte r, byte g, byte b)
    {
        var pincel = new SolidColorBrush(Color.FromRgb(r, g, b));
        pincel.Freeze();
        return pincel;
    }

    private static ImageSource? CargarLogo(bool grises)
    {
        try
        {
            var imagen = new BitmapImage();
            imagen.BeginInit();
            imagen.UriSource = new Uri("pack://application:,,,/Assets/logo.png", UriKind.Absolute);
            imagen.CacheOption = BitmapCacheOption.OnLoad;
            imagen.EndInit();
            imagen.Freeze();

            if (!grises)
            {
                return imagen;
            }

            var gris = new FormatConvertedBitmap(imagen, PixelFormats.Gray8, null, 0);
            gris.Freeze();
            return gris;
        }
        catch
        {
            return null;
        }
    }
}
