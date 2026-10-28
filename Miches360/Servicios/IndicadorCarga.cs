using CommunityToolkit.Mvvm.ComponentModel;

namespace Miches360.Servicios;

public sealed partial class IndicadorCarga : ObservableObject
{
    private static readonly TimeSpan Espera = TimeSpan.FromMilliseconds(300);

    private static readonly TimeSpan MinimoVisible = TimeSpan.FromMilliseconds(1200);

    private int _pendientes;
    private DateTime _mostradoEn;

    [ObservableProperty]
    private bool _visible;

    [ObservableProperty]
    private string _texto = "Un momento…";

    public async Task EjecutarAsync(string texto, Func<Task> operacion)
    {
        await EjecutarAsync(texto, async () =>
        {
            await operacion();
            return true;
        });
    }

    public async Task<T> EjecutarAsync<T>(string texto, Func<Task<T>> operacion)
    {
        _pendientes++;
        var tarea = operacion();
        try
        {
            var termino = await Task.WhenAny(tarea, Task.Delay(Espera));
            if (termino != tarea)
            {
                Texto = texto;
                if (!Visible)
                {
                    _mostradoEn = DateTime.UtcNow;
                    Visible = true;
                }
            }

            return await tarea;
        }
        finally
        {
            _pendientes--;
            if (_pendientes == 0 && Visible)
            {
                var falta = MinimoVisible - (DateTime.UtcNow - _mostradoEn);
                if (falta > TimeSpan.Zero)
                {
                    await Task.Delay(falta);
                }

                if (_pendientes == 0)
                {
                    Visible = false;
                }
            }
        }
    }
}
