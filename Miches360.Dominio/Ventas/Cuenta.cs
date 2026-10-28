using Miches360.Dominio.Catalogo;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;

namespace Miches360.Dominio.Ventas;

public sealed class Cuenta : Entidad
{
    private readonly List<Subcuenta> _subcuentas = new();
    private readonly List<Cobro> _cobros = new();

    private Cuenta()
    {
    }

    private Cuenta(int folio, Guid? mesaId, TipoCuenta tipo, string? usuario, DateTime ahora)
    {
        if (folio <= 0)
        {
            throw new ReglaNegocioException("El folio debe ser mayor a cero.");
        }

        Folio = folio;
        MesaId = mesaId;
        Tipo = tipo;
        Estado = EstadoCuenta.Abierta;
        AbiertaEn = ahora;
        Usuario = Validar.TextoOpcional(usuario, 60);
    }

    public int Folio { get; private set; }

    public Guid? MesaId { get; private set; }

    public TipoCuenta Tipo { get; private set; }

    public EstadoCuenta Estado { get; private set; }

    public DateTime AbiertaEn { get; private set; }

    public DateTime? CerradaEn { get; private set; }

    public string? Usuario { get; private set; }

    public DateTime? CanceladaEn { get; private set; }

    public string? MotivoCancelacion { get; private set; }

    public string? CanceladaPor { get; private set; }

    public bool EsVentaCancelada => Estado == EstadoCuenta.Cancelada && MotivoCancelacion is not null;

    public IReadOnlyCollection<Subcuenta> Subcuentas => _subcuentas;

    public IReadOnlyCollection<Cobro> Cobros => _cobros;

    public decimal Total => _subcuentas.Sum(s => s.Total);

    public decimal Pendiente => _subcuentas.Where(s => !s.Pagada).Sum(s => s.Total);

    public static Cuenta ParaLlevar(int folio, string? usuario, DateTime ahora)
    {
        var cuenta = new Cuenta(folio, null, TipoCuenta.Unica, usuario, ahora);
        cuenta.AgregarPersona("General");
        return cuenta;
    }

    public static Cuenta AbrirMesa(int folio, Guid mesaId, TipoCuenta tipo, int personas, string? usuario, DateTime ahora)
    {
        if (personas < 1)
        {
            throw new ReglaNegocioException("La mesa debe tener al menos una persona.");
        }

        var cuenta = new Cuenta(folio, mesaId, tipo, usuario, ahora);
        if (tipo == TipoCuenta.Unica)
        {
            cuenta.AgregarPersona("General");
        }
        else
        {
            for (var i = 1; i <= personas; i++)
            {
                cuenta.AgregarPersona($"Persona {i}");
            }
        }

        return cuenta;
    }

    public Subcuenta AgregarPersona(string nombre)
    {
        AsegurarAbierta();
        var orden = _subcuentas.Count == 0 ? 1 : _subcuentas.Max(s => s.Orden) + 1;
        var subcuenta = new Subcuenta(Id, nombre, orden);
        _subcuentas.Add(subcuenta);
        return subcuenta;
    }

    public void QuitarPersona(Guid subcuentaId, DateTime ahora)
    {
        AsegurarAbierta();
        var subcuenta = ObtenerSubcuenta(subcuentaId);

        if (subcuenta.Pagada)
        {
            throw new ReglaNegocioException($"{subcuenta.Nombre} ya pagó; no se puede quitar.");
        }

        if (subcuenta.Renglones.Count > 0)
        {
            throw new ReglaNegocioException($"{subcuenta.Nombre} tiene productos. Pásalos a otra persona antes de quitarla.");
        }

        if (_subcuentas.Count == 1)
        {
            throw new ReglaNegocioException("La cuenta debe tener al menos una persona.");
        }

        _subcuentas.Remove(subcuenta);

        if (_subcuentas.Any(s => s.Pagada) && _subcuentas.All(s => s.Pagada || s.Total == 0))
        {
            Estado = EstadoCuenta.Pagada;
            CerradaEn = ahora;
        }
    }

    public void RenombrarPersona(Guid subcuentaId, string nombre)
    {
        AsegurarAbierta();
        ObtenerSubcuenta(subcuentaId).Renombrar(nombre);
    }

    public Renglon AgregarRenglon(Guid subcuentaId, Producto producto, Tamano tamano, int cantidad, string? nota, DateTime ahora)
    {
        AsegurarAbierta();

        if (!producto.Activo || !producto.Disponible)
        {
            throw new ReglaNegocioException($"{producto.Nombre} no está disponible.");
        }

        var precio = producto.ObtenerPrecio(tamano)
            ?? throw new ReglaNegocioException($"{producto.Nombre} no tiene precio para el tamaño {tamano}.");

        var subcuenta = ObtenerSubcuenta(subcuentaId);
        if (subcuenta.Pagada)
        {
            throw new ReglaNegocioException($"La cuenta de {subcuenta.Nombre} ya está pagada.");
        }

        return subcuenta.AgregarRenglon(producto.Id, producto.Nombre, tamano, precio, cantidad, nota, producto.EstacionEfectiva, ahora);
    }

    public void QuitarRenglon(Guid renglonId)
    {
        AsegurarAbierta();
        var subcuenta = BuscarSubcuentaDeRenglon(renglonId);
        if (subcuenta.Pagada)
        {
            throw new ReglaNegocioException("No se puede quitar un producto de una cuenta pagada.");
        }

        subcuenta.QuitarRenglon(renglonId);
    }

    public void MoverRenglon(Guid renglonId, Guid subcuentaDestinoId)
    {
        AsegurarAbierta();
        var origen = BuscarSubcuentaDeRenglon(renglonId);
        var destino = ObtenerSubcuenta(subcuentaDestinoId);
        if (origen.Pagada || destino.Pagada)
        {
            throw new ReglaNegocioException("No se pueden mover productos de o hacia una cuenta pagada.");
        }

        if (origen.Id == destino.Id)
        {
            return;
        }

        var renglon = origen.ExtraerRenglon(renglonId);
        destino.RecibirRenglon(renglon);
    }

    public Cobro Cobrar(
        IReadOnlyCollection<Guid> subcuentaIds,
        IReadOnlyCollection<PagoSolicitado> pagos,
        decimal efectivoRecibido,
        string? usuario,
        DateTime ahora)
    {
        AsegurarAbierta();

        if (subcuentaIds.Count == 0)
        {
            throw new ReglaNegocioException("Indica qué cuenta se va a cobrar.");
        }

        var subcuentas = subcuentaIds.Distinct().Select(ObtenerSubcuenta).ToList();
        if (subcuentas.Any(s => s.Pagada))
        {
            throw new ReglaNegocioException("Alguna de las cuentas ya está pagada.");
        }

        var total = Validar.Dinero(subcuentas.Sum(s => s.Total));
        if (total <= 0)
        {
            throw new ReglaNegocioException("No hay nada que cobrar.");
        }

        var cobro = Cobro.Crear(Id, total, pagos, efectivoRecibido, usuario, ahora);
        _cobros.Add(cobro);

        foreach (var subcuenta in subcuentas)
        {
            subcuenta.MarcarPagada(cobro.Id);
        }

        if (_subcuentas.All(s => s.Pagada || s.Total == 0))
        {
            Estado = EstadoCuenta.Pagada;
            CerradaEn = ahora;
        }

        return cobro;
    }

    public void Cancelar(DateTime ahora)
    {
        AsegurarAbierta();
        if (_cobros.Count > 0)
        {
            throw new ReglaNegocioException("La cuenta ya tiene cobros; no se puede cancelar.");
        }

        Estado = EstadoCuenta.Cancelada;
        CerradaEn = ahora;
    }

    public void CancelarVenta(string? motivo, string? usuario, DateTime ahora)
    {
        if (Estado == EstadoCuenta.Cancelada)
        {
            throw new ReglaNegocioException("La venta ya estaba cancelada.");
        }

        var motivoLimpio = Validar.Texto(motivo, "El motivo de la cancelación", 120);
        if (motivoLimpio.Length < 3)
        {
            throw new ReglaNegocioException("Escribe un motivo de al menos 3 letras.");
        }

        Estado = EstadoCuenta.Cancelada;
        MotivoCancelacion = motivoLimpio;
        CanceladaPor = Validar.TextoOpcional(usuario, 60);
        CanceladaEn = ahora;
        CerradaEn ??= ahora;
    }

    private Subcuenta ObtenerSubcuenta(Guid subcuentaId) =>
        _subcuentas.FirstOrDefault(s => s.Id == subcuentaId)
        ?? throw new ReglaNegocioException("La persona indicada no pertenece a esta cuenta.");

    private Subcuenta BuscarSubcuentaDeRenglon(Guid renglonId) =>
        _subcuentas.FirstOrDefault(s => s.Renglones.Any(r => r.Id == renglonId))
        ?? throw new ReglaNegocioException("El producto indicado no pertenece a esta cuenta.");

    private void AsegurarAbierta()
    {
        if (Estado != EstadoCuenta.Abierta)
        {
            throw new ReglaNegocioException("La cuenta ya está cerrada.");
        }
    }
}
