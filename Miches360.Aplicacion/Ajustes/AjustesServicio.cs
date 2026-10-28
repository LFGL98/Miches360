using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Negocio;

namespace Miches360.Aplicacion.Ajustes;

public interface IAjustesServicio
{
    Task<DatosTransferenciaDto> ObtenerDatosTransferenciaAsync(CancellationToken cancellationToken = default);

    Task<bool> PinEsInicialAsync(CancellationToken cancellationToken = default);

    Task<bool> VerificarPinAsync(string pin, CancellationToken cancellationToken = default);

    Task<AjustesDto?> AbrirAjustesAsync(string pin, CancellationToken cancellationToken = default);

    Task GuardarDatosTransferenciaAsync(string pin, string? banco, string? clabe, string? titular, CancellationToken cancellationToken = default);

    Task CambiarPinAsync(string pinActual, string? pinNuevo, string? confirmacion, CancellationToken cancellationToken = default);

    Task<ConfiguracionTicketDto> ObtenerConfiguracionTicketAsync(CancellationToken cancellationToken = default);

    Task GuardarConfiguracionTicketAsync(string pin, ConfiguracionTicketDto ticket, CancellationToken cancellationToken = default);
}

internal sealed partial class AjustesServicio : IAjustesServicio
{
    private readonly IAppDbContextFactory _fabrica;
    private readonly IHasherPin _hasher;
    private readonly IReloj _reloj;

    public AjustesServicio(IAppDbContextFactory fabrica, IHasherPin hasher, IReloj reloj)
    {
        _fabrica = fabrica;
        _hasher = hasher;
        _reloj = reloj;
    }

    public async Task<DatosTransferenciaDto> ObtenerDatosTransferenciaAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await db.ConfiguracionesNegocio.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        if (config is null || !config.TieneDatosTransferencia)
        {
            return new DatosTransferenciaDto(string.Empty, string.Empty, string.Empty, false);
        }

        return new DatosTransferenciaDto(config.Banco, FormatearClabe(config.Clabe), config.Titular, true);
    }

    public async Task<bool> PinEsInicialAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await db.ConfiguracionesNegocio.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return config?.PinEsInicial ?? true;
    }

    public async Task<bool> VerificarPinAsync(string pin, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await db.ConfiguracionesNegocio.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return config is not null && _hasher.Verificar(pin, config.PinHash);
    }

    public async Task<AjustesDto?> AbrirAjustesAsync(string pin, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await ObtenerAsync(db, cancellationToken);

        if (!_hasher.Verificar(pin, config.PinHash))
        {
            return null;
        }

        return new AjustesDto(config.Banco, config.Clabe, config.Titular, config.PinEsInicial, ATicket(config));
    }

    public async Task GuardarDatosTransferenciaAsync(string pin, string? banco, string? clabe, string? titular, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await ObtenerAsync(db, cancellationToken);
        AsegurarPin(pin, config);

        config.ActualizarDatosTransferencia(banco, clabe, titular, _reloj.Ahora);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CambiarPinAsync(string pinActual, string? pinNuevo, string? confirmacion, CancellationToken cancellationToken = default)
    {
        if (pinNuevo is null || !FormatoPin().IsMatch(pinNuevo))
        {
            throw new ReglaNegocioException("El nuevo PIN debe tener de 4 a 6 números.");
        }

        if (pinNuevo != confirmacion)
        {
            throw new ReglaNegocioException("La confirmación no coincide con el nuevo PIN.");
        }

        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await ObtenerAsync(db, cancellationToken);
        AsegurarPin(pinActual, config);

        if (_hasher.Verificar(pinNuevo, config.PinHash))
        {
            throw new ReglaNegocioException("El nuevo PIN debe ser distinto al actual.");
        }

        config.CambiarPin(_hasher.Calcular(pinNuevo), _reloj.Ahora);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ConfiguracionTicketDto> ObtenerConfiguracionTicketAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await db.ConfiguracionesNegocio.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return config is null
            ? new ConfiguracionTicketDto(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, 80, false, true)
            : ATicket(config);
    }

    public async Task GuardarConfiguracionTicketAsync(string pin, ConfiguracionTicketDto ticket, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var config = await ObtenerAsync(db, cancellationToken);
        AsegurarPin(pin, config);

        config.ActualizarTicket(
            ticket.NombreTicket,
            ticket.Direccion,
            ticket.Telefono,
            ticket.PieTicket,
            ticket.Impresora,
            ticket.AnchoMm,
            ticket.ImprimirAlCobrar,
            ticket.Logo,
            _reloj.Ahora);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static ConfiguracionTicketDto ATicket(ConfiguracionNegocio config) =>
        new(
            config.NombreTicket,
            config.Direccion,
            config.Telefono,
            config.PieTicket,
            config.ImpresoraTickets,
            config.AnchoTicketMm == 58 ? 58 : 80,
            config.ImprimirTicketAlCobrar,
            config.LogoEnTicket);

    private void AsegurarPin(string pin, ConfiguracionNegocio config)
    {
        if (!_hasher.Verificar(pin, config.PinHash))
        {
            throw new ReglaNegocioException("PIN de administrador incorrecto.");
        }
    }

    private static async Task<ConfiguracionNegocio> ObtenerAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        await db.ConfiguracionesNegocio.FirstOrDefaultAsync(cancellationToken)
        ?? throw new ReglaNegocioException("Falta la configuración del negocio. Reinicia el sistema.");

    private static string FormatearClabe(string digitos) => digitos.Length switch
    {
        18 => $"{digitos[..3]} {digitos[3..6]} {digitos[6..17]} {digitos[17..]}",
        16 => $"{digitos[..4]} {digitos[4..8]} {digitos[8..12]} {digitos[12..]}",
        _ => digitos
    };

    [GeneratedRegex(@"^\d{4,6}$")]
    private static partial Regex FormatoPin();
}
