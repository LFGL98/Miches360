using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Miches360.Aplicacion.Mesas;
using Miches360.Dominio.Comun;
using Miches360.Dominio.Enums;
using Miches360.Servicios;

namespace Miches360.ViewModels;

public sealed record OrdenMesaContexto(Guid CuentaId);

public sealed partial class LineaOrden : ObservableObject
{
    public LineaOrden(Guid subcuentaId, string persona, Guid productoId, string nombre, Tamano tamano, decimal precio)
    {
        SubcuentaId = subcuentaId;
        Persona = persona;
        ProductoId = productoId;
        Nombre = nombre;
        Tamano = tamano;
        Precio = precio;
    }

    public Guid SubcuentaId { get; }

    public string Persona { get; }

    public Guid ProductoId { get; }

    public string Nombre { get; }

    public Tamano Tamano { get; }

    public decimal Precio { get; }

    public string Detalle => Tamano == Tamano.Unico ? Persona : $"{Persona} · {Tamano}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Importe))]
    private int _cantidad = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoNota), nameof(TieneNota))]
    private string? _nota;

    public bool TieneNota => !string.IsNullOrWhiteSpace(Nota);

    public string TextoNota => TieneNota ? $"Nota: {Nota}" : "+ Agregar nota";

    public decimal Importe => Precio * Cantidad;
}

public sealed partial class OrdenMesaViewModel : ObservableObject
{
    private readonly OrdenMesaContexto _contexto;
    private readonly IMesasServicio _mesas;
    private readonly Navegador _navegador;
    private Guid? _mesaId;
    private bool _avisoDescartar;

    public OrdenMesaViewModel(OrdenMesaContexto contexto, SelectorProductosViewModel selector, IMesasServicio mesas, Navegador navegador)
    {
        _contexto = contexto;
        _mesas = mesas;
        _navegador = navegador;
        Selector = selector;
        Selector.ProductoElegido += AlElegirProducto;
        Lineas.CollectionChanged += (_, _) => RecalcularTotales();
    }

    public SelectorProductosViewModel Selector { get; }

    public ObservableCollection<OpcionSeleccionable<Guid>> Personas { get; } = new();

    public ObservableCollection<LineaOrden> Lineas { get; } = new();

    public EditorNotaViewModel EditorNota { get; } = new();

    [ObservableProperty]
    private string _titulo = "ORDEN";

    [ObservableProperty]
    private bool _esSeparada;

    [ObservableProperty]
    private Guid? _personaActualId;

    [ObservableProperty]
    private decimal _total;

    [ObservableProperty]
    private int _articulos;

    [ObservableProperty]
    private string? _mensaje;

    [ObservableProperty]
    private bool _mensajeEsError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EnviarCommand), nameof(AgregarPersonaCommand))]
    private bool _procesando;

    partial void OnPersonaActualIdChanged(Guid? value) => Opciones.Marcar(Personas, value ?? Guid.Empty);

    public async Task InicializarAsync()
    {
        await Selector.InicializarAsync();
        await CargarCuentaAsync(null);
    }

    [RelayCommand]
    private void ElegirPersona(OpcionSeleccionable<Guid>? persona)
    {
        if (persona is not null)
        {
            PersonaActualId = persona.Valor;
        }
    }

    [RelayCommand(CanExecute = nameof(NoProcesando))]
    private async Task AgregarPersonaAsync()
    {
        Procesando = true;
        try
        {
            var id = await _mesas.AgregarPersonaAsync(_contexto.CuentaId, null);
            await CargarCuentaAsync(id);
        }
        catch (Exception ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void Incrementar(LineaOrden? linea)
    {
        if (linea is null)
        {
            return;
        }

        linea.Cantidad++;
        RecalcularTotales();
    }

    [RelayCommand]
    private void Decrementar(LineaOrden? linea)
    {
        if (linea is null)
        {
            return;
        }

        if (linea.Cantidad <= 1)
        {
            Lineas.Remove(linea);
        }
        else
        {
            linea.Cantidad--;
            RecalcularTotales();
        }
    }

    [RelayCommand]
    private void EditarNota(LineaOrden? linea)
    {
        if (linea is not null)
        {
            EditorNota.Abrir($"{linea.Nombre} · {linea.Detalle}", linea.Nota, nota => linea.Nota = nota);
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeEnviar))]
    private async Task EnviarAsync()
    {
        Procesando = true;
        try
        {
            var renglones = Lineas
                .Select(l => new RenglonMesaSolicitud(l.SubcuentaId, l.ProductoId, l.Tamano, l.Cantidad, l.Nota))
                .ToList();

            await _mesas.AgregarRenglonesAsync(_contexto.CuentaId, renglones);
            var enviados = Lineas.Sum(l => l.Cantidad);
            Lineas.Clear();
            _navegador.IrAMesas(_mesaId, $"Orden enviada · {enviados} producto{(enviados == 1 ? string.Empty : "s")}");
        }
        catch (ReglaNegocioException ex)
        {
            MostrarMensaje(ex.Message, esError: true);
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo enviar la orden: {ex.Message}", esError: true);
        }
        finally
        {
            Procesando = false;
        }
    }

    [RelayCommand]
    private void Volver()
    {
        if (Lineas.Count > 0 && !_avisoDescartar)
        {
            _avisoDescartar = true;
            MostrarMensaje($"Tienes {Articulos} producto(s) sin enviar. Toca Volver otra vez para descartarlos.", esError: true);
            return;
        }

        _navegador.IrAMesas(_mesaId, null);
    }

    private bool NoProcesando() => !Procesando;

    private bool PuedeEnviar() => !Procesando && Lineas.Count > 0;

    private void AlElegirProducto(ProductoItem producto, Tamano tamano, decimal precio)
    {
        var persona = Personas.FirstOrDefault(p => p.Valor == PersonaActualId);
        if (persona is null)
        {
            MostrarMensaje("Primero elige a quién va el producto.", esError: true);
            return;
        }

        var existente = Lineas.FirstOrDefault(l =>
            l.SubcuentaId == persona.Valor && l.ProductoId == producto.Id && l.Tamano == tamano && !l.TieneNota);

        if (existente is null)
        {
            Lineas.Add(new LineaOrden(persona.Valor, NombreCorto(persona.Texto), producto.Id, producto.Nombre, tamano, precio));
        }
        else
        {
            existente.Cantidad++;
            RecalcularTotales();
        }

        _avisoDescartar = false;
        Mensaje = null;
    }

    private async Task CargarCuentaAsync(Guid? seleccionar)
    {
        try
        {
            var cuenta = await _mesas.ObtenerCuentaAsync(_contexto.CuentaId);
            _mesaId = cuenta.MesaId;
            Titulo = $"{cuenta.MesaNombre.ToUpperInvariant()} · AGREGAR PRODUCTOS";
            EsSeparada = cuenta.Tipo == TipoCuenta.Separadas || cuenta.Personas.Count > 1;

            var anterior = seleccionar ?? PersonaActualId;
            Personas.Clear();
            foreach (var persona in cuenta.Personas.Where(p => !p.Pagada))
            {
                var texto = persona.Total > 0 ? $"{persona.Nombre} · {Formato.Dinero(persona.Total)}" : persona.Nombre;
                Personas.Add(new OpcionSeleccionable<Guid>(persona.SubcuentaId, texto));
            }

            PersonaActualId = Personas.Any(p => p.Valor == anterior) ? anterior : Personas.FirstOrDefault()?.Valor;
            Opciones.Marcar(Personas, PersonaActualId ?? Guid.Empty);

            if (Personas.Count == 0)
            {
                MostrarMensaje("Todas las personas de esta mesa ya pagaron. Agrega una persona para seguir pidiendo.", esError: true);
            }
        }
        catch (Exception ex)
        {
            MostrarMensaje($"No se pudo cargar la cuenta: {ex.Message}", esError: true);
        }
    }

    private void RecalcularTotales()
    {
        Total = Lineas.Sum(l => l.Importe);
        Articulos = Lineas.Sum(l => l.Cantidad);
        EnviarCommand.NotifyCanExecuteChanged();
    }

    private static string NombreCorto(string texto)
    {
        var separador = texto.IndexOf(" · ", StringComparison.Ordinal);
        return separador > 0 ? texto[..separador] : texto;
    }

    private void MostrarMensaje(string mensaje, bool esError)
    {
        MensajeEsError = esError;
        Mensaje = mensaje;
    }
}
