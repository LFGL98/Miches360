using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Miches360.ViewModels;

public sealed partial class EditorNotaViewModel : ObservableObject
{
    public const int Maximo = 120;

    private Action<string?>? _alGuardar;

    public IReadOnlyList<string> Sugerencias { get; } = new[]
    {
        "Sin chile",
        "Poco chile",
        "Extra chile",
        "Con clamato",
        "Sin clamato",
        "Escarchado",
        "Sin sal",
        "Poco hielo",
        "Sin hielo",
        "Extra limón",
        "Para llevar"
    };

    [ObservableProperty]
    private bool _abierto;

    [ObservableProperty]
    private string _producto = string.Empty;

    [ObservableProperty]
    private string _texto = string.Empty;

    public void Abrir(string producto, string? nota, Action<string?> alGuardar)
    {
        Producto = producto;
        Texto = nota ?? string.Empty;
        _alGuardar = alGuardar;
        Abierto = true;
    }

    [RelayCommand]
    private void AgregarSugerencia(string? sugerencia)
    {
        if (string.IsNullOrWhiteSpace(sugerencia)
            || Texto.Contains(sugerencia, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var actual = Texto.Trim().TrimEnd(',').Trim();
        var nuevo = actual.Length == 0 ? sugerencia : $"{actual}, {sugerencia}";
        if (nuevo.Length <= Maximo)
        {
            Texto = nuevo;
        }
    }

    [RelayCommand]
    private void Guardar()
    {
        var limpio = Texto.Trim().TrimEnd(',').Trim();
        _alGuardar?.Invoke(limpio.Length == 0 ? null : limpio);
        Cerrar();
    }

    [RelayCommand]
    private void Quitar()
    {
        _alGuardar?.Invoke(null);
        Cerrar();
    }

    [RelayCommand]
    private void Cerrar()
    {
        Abierto = false;
        _alGuardar = null;
    }
}
