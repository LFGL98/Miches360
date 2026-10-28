using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Miches360.Servicios.Impresion;

namespace Miches360.Convertidores;

public sealed class BoolAVisibilidadConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is true;
        if (Invertir(parameter))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static bool Invertir(object parameter) =>
        string.Equals(parameter as string, "invertir", StringComparison.OrdinalIgnoreCase);
}

public sealed class ValorAVisibilidadConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            string texto => !string.IsNullOrWhiteSpace(texto),
            int numero => numero != 0,
            decimal numero => numero != 0,
            _ => true
        };

        if (BoolAVisibilidadConverter.Invertir(parameter))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class VistaPreviaTicketConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is VistaPreviaTicket vista
            ? DocumentosTicket.Venta(vista.Ticket, vista.Datos, 300)
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
