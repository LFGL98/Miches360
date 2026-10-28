using Miches360.Aplicacion.Mesas;
using Miches360.Dominio.Enums;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public static class CobroMesa
{
    public static CobroContexto Crear(
        CuentaDetalleDto cuenta,
        IReadOnlyList<PersonaCuentaDto> personas,
        IMesasServicio servicio,
        Navegador navegador,
        Action alVolver)
    {
        var varias = personas.Count > 1;
        var lineas = personas
            .SelectMany(p => p.Renglones.Select(r => new LineaCobro(
                r.Cantidad,
                r.Producto,
                Detalle(p, r, varias),
                r.Importe)))
            .ToList();

        var titulo = cuenta.Personas.Count > 1 && personas.Count == 1
            ? $"COBRAR · {cuenta.MesaNombre.ToUpperInvariant()} · {personas[0].Nombre.ToUpperInvariant()}"
            : $"COBRAR · {cuenta.MesaNombre.ToUpperInvariant()}";

        var ids = personas.Select(p => p.SubcuentaId).ToList();
        var total = personas.Sum(p => p.Total);

        return new CobroContexto(
            titulo,
            lineas,
            total,
            MetodoCobro.Efectivo,
            async solicitud =>
            {
                var resultado = await servicio.CobrarAsync(new CobroMesaSolicitud(
                    cuenta.CuentaId,
                    ids,
                    solicitud.Pagos,
                    solicitud.EfectivoRecibido,
                    solicitud.Usuario));
                return new CobroResultado(resultado.CuentaId, resultado.CobroId, resultado.Folio, resultado.Total, resultado.Cambio, resultado.CuentaCerrada);
            },
            resultado =>
            {
                var cambio = resultado.Cambio > 0 ? $" · Cambio {Formato.Dinero(resultado.Cambio)}" : string.Empty;
                if (resultado.CuentaCerrada)
                {
                    navegador.IrAMesas(cuenta.MesaId, $"{cuenta.MesaNombre} cobrada y libre{cambio}", resultado.Ticket);
                }
                else
                {
                    navegador.IrACuentaMesa(cuenta.CuentaId, $"Cobro registrado{cambio}. Falta cobrar al resto.", resultado.Ticket);
                }
            },
            alVolver);
    }

    private static string Detalle(PersonaCuentaDto persona, RenglonCuentaDto renglon, bool varias)
    {
        var partes = new List<string>();
        if (varias)
        {
            partes.Add(persona.Nombre);
        }

        if (renglon.Tamano != Tamano.Unico)
        {
            partes.Add(renglon.Tamano.ToString());
        }

        if (!string.IsNullOrWhiteSpace(renglon.Nota))
        {
            partes.Add($"Nota: {renglon.Nota}");
        }

        return string.Join(" · ", partes);
    }
}
