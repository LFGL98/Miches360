namespace Miches360.Aplicacion.Ajustes;

public sealed record DatosTransferenciaDto(
    string Banco,
    string ClabeFormateada,
    string Titular,
    bool Configurado);

public sealed record AjustesDto(
    string Banco,
    string Clabe,
    string Titular,
    bool PinEsInicial,
    ConfiguracionTicketDto Ticket);

public sealed record ConfiguracionTicketDto(
    string NombreTicket,
    string Direccion,
    string Telefono,
    string PieTicket,
    string Impresora,
    int AnchoMm,
    bool ImprimirAlCobrar,
    bool Logo);
