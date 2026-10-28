using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Miches360.Aplicacion.Abstracciones;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Dispositivos;

namespace Miches360.Aplicacion.Dispositivos;

public sealed record DispositivoDto(Guid Id, string Mesero, string? Modelo, DateTime VinculadoEn, DateTime? UltimaConexion);

public sealed record VinculacionDto(Guid DispositivoId, string Token, string Mesero);

public sealed record SesionDispositivo(Guid DispositivoId, string Mesero);

public interface IDispositivosServicio
{
    string CodigoVinculacion { get; }

    string GenerarCodigo();

    Task<VinculacionDto> VincularAsync(string? codigo, string? mesero, string? modelo, CancellationToken cancellationToken = default);

    Task<SesionDispositivo?> ValidarAsync(string? token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DispositivoDto>> ListarAsync(CancellationToken cancellationToken = default);

    Task RevocarAsync(Guid dispositivoId, CancellationToken cancellationToken = default);
}

internal sealed class DispositivosServicio : IDispositivosServicio
{
    private const int IntentosPermitidos = 5;

    private readonly IAppDbContextFactory _fabrica;
    private readonly IReloj _reloj;
    private readonly object _candado = new();
    private string _codigo;
    private int _intentosFallidos;

    public DispositivosServicio(IAppDbContextFactory fabrica, IReloj reloj)
    {
        _fabrica = fabrica;
        _reloj = reloj;
        _codigo = NuevoCodigo();
    }

    public string CodigoVinculacion
    {
        get
        {
            lock (_candado)
            {
                return _codigo;
            }
        }
    }

    public string GenerarCodigo()
    {
        lock (_candado)
        {
            _codigo = NuevoCodigo();
            _intentosFallidos = 0;
            return _codigo;
        }
    }

    public async Task<VinculacionDto> VincularAsync(string? codigo, string? mesero, string? modelo, CancellationToken cancellationToken = default)
    {
        lock (_candado)
        {
            if (!string.Equals((codigo ?? string.Empty).Trim(), _codigo, StringComparison.Ordinal))
            {
                _intentosFallidos++;
                if (_intentosFallidos >= IntentosPermitidos)
                {
                    _codigo = NuevoCodigo();
                    _intentosFallidos = 0;
                    throw new ReglaNegocioException("Código incorrecto. La caja generó uno nuevo: revísalo en Ajustes → Celulares.");
                }

                throw new ReglaNegocioException("Código incorrecto. Revísalo en la caja: Ajustes → Celulares.");
            }

            _intentosFallidos = 0;
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var dispositivo = new Dispositivo(mesero ?? string.Empty, modelo, Hash(token), _reloj.Ahora);

        await using var db = await _fabrica.CrearAsync(cancellationToken);
        db.Dispositivos.Add(dispositivo);
        await db.SaveChangesAsync(cancellationToken);

        return new VinculacionDto(dispositivo.Id, token, dispositivo.Mesero);
    }

    public async Task<SesionDispositivo?> ValidarAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Hash(token.Trim());
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var dispositivo = await db.Dispositivos.FirstOrDefaultAsync(d => d.TokenHash == hash, cancellationToken);
        if (dispositivo is null || dispositivo.Revocado)
        {
            return null;
        }

        var ahora = _reloj.Ahora;
        if (dispositivo.UltimaConexion is not DateTime ultima || ahora - ultima > TimeSpan.FromMinutes(1))
        {
            dispositivo.RegistrarConexion(ahora);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new SesionDispositivo(dispositivo.Id, dispositivo.Mesero);
    }

    public async Task<IReadOnlyList<DispositivoDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        return await db.Dispositivos
            .AsNoTracking()
            .Where(d => !d.Revocado)
            .OrderBy(d => d.Mesero)
            .Select(d => new DispositivoDto(d.Id, d.Mesero, d.Modelo, d.VinculadoEn, d.UltimaConexion))
            .ToListAsync(cancellationToken);
    }

    public async Task RevocarAsync(Guid dispositivoId, CancellationToken cancellationToken = default)
    {
        await using var db = await _fabrica.CrearAsync(cancellationToken);
        var dispositivo = await db.Dispositivos.FirstOrDefaultAsync(d => d.Id == dispositivoId, cancellationToken)
            ?? throw new ReglaNegocioException("El celular ya no existe.");
        dispositivo.Revocar();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NuevoCodigo() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
