using Miches360.Dominio.Comun;

namespace Miches360.Dominio.Caja;

public sealed class ArqueoCaja : Entidad
{
    private ArqueoCaja()
    {
    }

    public ArqueoCaja(
        DateTime dia,
        decimal fondo,
        decimal ventasEfectivo,
        decimal contado,
        string? notas,
        string? usuario,
        DateTime ahora)
    {
        if (fondo < 0 || contado < 0)
        {
            throw new ReglaNegocioException("Los montos no pueden ser negativos.");
        }

        Dia = dia.Date;
        Fondo = Validar.Dinero(fondo);
        VentasEfectivo = Validar.Dinero(ventasEfectivo);
        Esperado = Fondo + VentasEfectivo;
        Contado = Validar.Dinero(contado);
        Diferencia = Contado - Esperado;
        Notas = Validar.TextoOpcional(notas, 200);
        Usuario = Validar.TextoOpcional(usuario, 60);
        RealizadoEn = ahora;
    }

    public DateTime Dia { get; private set; }

    public DateTime RealizadoEn { get; private set; }

    public string? Usuario { get; private set; }

    public decimal Fondo { get; private set; }

    public decimal VentasEfectivo { get; private set; }

    public decimal Esperado { get; private set; }

    public decimal Contado { get; private set; }

    public decimal Diferencia { get; private set; }

    public string? Notas { get; private set; }
}
