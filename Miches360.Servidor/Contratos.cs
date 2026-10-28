using Miches360.Aplicacion.Ventas;
using Miches360.Dominio.Enums;

namespace Miches360.Servidor;

public sealed class ServidorOpciones
{
    public bool Habilitado { get; set; } = true;

    public int Puerto { get; set; } = 5360;
}

public sealed record VincularPeticion(string? Codigo, string? Mesero, string? Modelo);

public sealed record AbrirMesaPeticion(TipoCuenta Tipo, int Personas);

public sealed record NombrePeticion(string? Nombre);

public sealed record IdRespuesta(Guid Id);

public sealed record CobrarPeticion(List<Guid> SubcuentaIds, List<PagoSolicitud> Pagos, decimal EfectivoRecibido);

public sealed record MoverRenglonPeticion(Guid SubcuentaDestinoId);

public sealed record ErrorRespuesta(string Mensaje);

public sealed record SaludRespuesta(string App, int Version, string Hora);
