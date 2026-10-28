using Miches360.Dominio.Comun;

namespace Miches360.Dominio.Dispositivos;

public sealed class Dispositivo : Entidad
{
    private Dispositivo()
    {
    }

    public Dispositivo(string mesero, string? modelo, string tokenHash, DateTime ahora)
    {
        Mesero = Validar.Texto(mesero, "El nombre del mesero", 40);
        Modelo = Validar.TextoOpcional(modelo, 80);
        TokenHash = Validar.Texto(tokenHash, "El token", 128);
        VinculadoEn = ahora;
        UltimaConexion = ahora;
    }

    public string Mesero { get; private set; } = string.Empty;

    public string? Modelo { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public DateTime VinculadoEn { get; private set; }

    public DateTime? UltimaConexion { get; private set; }

    public bool Revocado { get; private set; }

    public void RegistrarConexion(DateTime ahora) => UltimaConexion = ahora;

    public void Revocar() => Revocado = true;
}
