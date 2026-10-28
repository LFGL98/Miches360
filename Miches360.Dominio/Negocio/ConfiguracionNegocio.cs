using Miches360.Dominio.Comun;

namespace Miches360.Dominio.Negocio;

public sealed class ConfiguracionNegocio : Entidad
{
    public static readonly Guid IdUnico = new("00000000-0000-0000-0000-000000000001");

    private ConfiguracionNegocio()
    {
    }

    public ConfiguracionNegocio(string pinHash)
    {
        Id = IdUnico;
        PinHash = pinHash;
        PinEsInicial = true;
        AnchoTicketMm = 80;
        ImprimirTicketAlCobrar = true;
        LogoEnTicket = true;
        PieTicket = "¡Gracias por su compra!";
    }

    public static readonly int[] AnchosTicket = { 58, 80 };

    public string Banco { get; private set; } = string.Empty;

    public string Clabe { get; private set; } = string.Empty;

    public string Titular { get; private set; } = string.Empty;

    public string PinHash { get; private set; } = string.Empty;

    public bool PinEsInicial { get; private set; }

    public DateTime? ActualizadoEn { get; private set; }

    public string NombreTicket { get; private set; } = string.Empty;

    public string Direccion { get; private set; } = string.Empty;

    public string Telefono { get; private set; } = string.Empty;

    public string PieTicket { get; private set; } = string.Empty;

    public string ImpresoraTickets { get; private set; } = string.Empty;

    public int AnchoTicketMm { get; private set; }

    public bool ImprimirTicketAlCobrar { get; private set; }

    public bool LogoEnTicket { get; private set; }

    public bool TieneDatosTransferencia => Clabe.Length > 0;

    public void ActualizarDatosTransferencia(string? banco, string? clabe, string? titular, DateTime ahora)
    {
        var digitos = new string((clabe ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length != 18 && digitos.Length != 16)
        {
            throw new ReglaNegocioException("La CLABE debe tener 18 dígitos (o 16 si es número de tarjeta).");
        }

        Banco = Validar.Texto(banco, "El banco", 60);
        Titular = Validar.Texto(titular, "El titular de la cuenta", 80);
        Clabe = digitos;
        ActualizadoEn = ahora;
    }

    public void ActualizarTicket(
        string? nombre,
        string? direccion,
        string? telefono,
        string? pie,
        string? impresora,
        int anchoMm,
        bool imprimirAlCobrar,
        bool logoEnTicket,
        DateTime ahora)
    {
        if (!AnchosTicket.Contains(anchoMm))
        {
            throw new ReglaNegocioException("El ancho del ticket debe ser 58 u 80 mm.");
        }

        NombreTicket = Validar.TextoOpcional(nombre, 60) ?? string.Empty;
        Direccion = Validar.TextoOpcional(direccion, 120) ?? string.Empty;
        Telefono = Validar.TextoOpcional(telefono, 30) ?? string.Empty;
        PieTicket = Validar.TextoOpcional(pie, 120) ?? string.Empty;
        ImpresoraTickets = Validar.TextoOpcional(impresora, 200) ?? string.Empty;
        AnchoTicketMm = anchoMm;
        ImprimirTicketAlCobrar = imprimirAlCobrar;
        LogoEnTicket = logoEnTicket;
        ActualizadoEn = ahora;
    }

    public void CambiarPin(string nuevoPinHash, DateTime ahora)
    {
        PinHash = nuevoPinHash;
        PinEsInicial = false;
        ActualizadoEn = ahora;
    }
}
