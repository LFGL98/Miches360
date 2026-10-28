using System.Text.RegularExpressions;

namespace Miches360.Dominio.Comun;

internal static partial class Validar
{
    public static string Texto(string? valor, string campo, int maximo)
    {
        var limpio = valor?.Trim() ?? string.Empty;
        if (limpio.Length == 0)
        {
            throw new ReglaNegocioException($"{campo} es obligatorio.");
        }

        if (limpio.Length > maximo)
        {
            throw new ReglaNegocioException($"{campo} admite máximo {maximo} caracteres.");
        }

        return limpio;
    }

    public static string? TextoOpcional(string? valor, int maximo)
    {
        var limpio = valor?.Trim();
        if (string.IsNullOrEmpty(limpio))
        {
            return null;
        }

        return limpio.Length > maximo ? limpio[..maximo] : limpio;
    }

    public static string Color(string? valor)
    {
        var limpio = valor?.Trim() ?? string.Empty;
        if (!ColorHex().IsMatch(limpio))
        {
            throw new ReglaNegocioException("El color debe tener el formato #RRGGBB.");
        }

        return limpio.ToUpperInvariant();
    }

    public static decimal Dinero(decimal valor) => decimal.Round(valor, 2, MidpointRounding.AwayFromZero);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex ColorHex();
}
