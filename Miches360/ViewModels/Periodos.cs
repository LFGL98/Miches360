using System.Globalization;

namespace Miches360.ViewModels;

public enum TipoPeriodo
{
    Dia,
    Semana,
    Mes
}

public static class Periodos
{
    public static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-MX");

    public static DateTime Inicio(TipoPeriodo tipo, DateTime dia) => tipo switch
    {
        TipoPeriodo.Semana => dia.Date.AddDays(-(((int)dia.DayOfWeek + 6) % 7)),
        TipoPeriodo.Mes => new DateTime(dia.Year, dia.Month, 1),
        _ => dia.Date
    };

    public static DateTime Fin(TipoPeriodo tipo, DateTime inicio) => tipo switch
    {
        TipoPeriodo.Semana => inicio.AddDays(7),
        TipoPeriodo.Mes => inicio.AddMonths(1),
        _ => inicio.AddDays(1)
    };

    public static DateTime Mover(TipoPeriodo tipo, DateTime inicio, int pasos) => tipo switch
    {
        TipoPeriodo.Semana => inicio.AddDays(7 * pasos),
        TipoPeriodo.Mes => inicio.AddMonths(pasos),
        _ => inicio.AddDays(pasos)
    };

    public static string Nombre(TipoPeriodo tipo) => tipo switch
    {
        TipoPeriodo.Semana => "semanal",
        TipoPeriodo.Mes => "mensual",
        _ => "diario"
    };

    public static string Texto(TipoPeriodo tipo, DateTime inicio) => tipo switch
    {
        TipoPeriodo.Semana => TextoSemana(inicio, inicio.AddDays(6)),
        TipoPeriodo.Mes => Capital(inicio.ToString("MMMM yyyy", Cultura)),
        _ => TextoDia(inicio)
    };

    public static string TextoDia(DateTime dia)
    {
        var hoy = DateTime.Today;
        var texto = Capital(dia.ToString("dddd d 'de' MMMM yyyy", Cultura));
        if (dia.Date == hoy)
        {
            return $"Hoy · {texto}";
        }

        return dia.Date == hoy.AddDays(-1) ? $"Ayer · {texto}" : texto;
    }

    public static string TextoSemana(DateTime lunes, DateTime domingo)
    {
        if (lunes.Month == domingo.Month)
        {
            return $"Semana del {lunes.Day} al {domingo.ToString("d 'de' MMMM yyyy", Cultura)}";
        }

        var inicio = lunes.Year == domingo.Year
            ? lunes.ToString("d 'de' MMMM", Cultura)
            : lunes.ToString("d 'de' MMMM yyyy", Cultura);
        return $"Semana del {inicio} al {domingo.ToString("d 'de' MMMM yyyy", Cultura)}";
    }

    public static string Capital(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], Cultura) + texto[1..];
}
