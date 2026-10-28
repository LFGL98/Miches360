using System.IO;
using System.Media;

namespace Miches360.Servicios;

public static class SonidoAviso
{
    private const int Muestreo = 44100;

    private static readonly Lazy<SoundPlayer> Orden = new(() => Cargar("destapar.wav", (1568.0, 0.18), (2093.0, 0.35)));

    private static readonly Lazy<SoundPlayer> Cuenta = new(() => Cargar("caja_registradora.wav", (1318.5, 0.22), (1046.5, 0.45)));

    private static readonly Lazy<SoundPlayer> Cobro = new(() => Cargar("monedas.wav", (2637.0, 0.12), (3136.0, 0.12), (3520.0, 0.3)));

    public static void TocarOrden() => Reproducir(Orden);

    public static void TocarCuenta() => Reproducir(Cuenta);

    public static void TocarCobro() => Reproducir(Cobro);

    public static void Precargar() => Task.Run(() =>
    {
        _ = Orden.Value;
        _ = Cuenta.Value;
        _ = Cobro.Value;
    });

    private static void Reproducir(Lazy<SoundPlayer> sonido)
    {
        try
        {
            sonido.Value.Play();
        }
        catch
        {
        }
    }

    private static SoundPlayer Cargar(string archivo, params (double Frecuencia, double Segundos)[] respaldo)
    {
        try
        {
            var memoria = new MemoryStream();
            using (var recurso = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{archivo}"))!.Stream)
            {
                recurso.CopyTo(memoria);
            }

            memoria.Position = 0;
            var reproductor = new SoundPlayer(memoria);
            reproductor.Load();
            return reproductor;
        }
        catch
        {
            var reproductor = new SoundPlayer(CrearCampana(respaldo));
            reproductor.Load();
            return reproductor;
        }
    }

    private static MemoryStream CrearCampana((double Frecuencia, double Segundos)[] notas)
    {
        var muestras = new List<short>();
        foreach (var (frecuencia, segundos) in notas)
        {
            var total = (int)(Muestreo * segundos);
            for (var i = 0; i < total; i++)
            {
                var t = (double)i / Muestreo;

                var ataque = Math.Min(1.0, i / (Muestreo * 0.008));
                var caida = Math.Exp(-4.5 * t / segundos);
                var tono = Math.Sin(2 * Math.PI * frecuencia * t)
                           + 0.35 * Math.Sin(2 * Math.PI * frecuencia * 2 * t)
                           + 0.15 * Math.Sin(2 * Math.PI * frecuencia * 3 * t);

                muestras.Add((short)(tono / 1.5 * ataque * caida * short.MaxValue * 0.9));
            }
        }

        muestras.AddRange(Enumerable.Repeat((short)0, Muestreo / 10));

        var flujo = new MemoryStream();
        using (var escritor = new BinaryWriter(flujo, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            var bytesDatos = muestras.Count * 2;
            escritor.Write("RIFF"u8.ToArray());
            escritor.Write(36 + bytesDatos);
            escritor.Write("WAVE"u8.ToArray());
            escritor.Write("fmt "u8.ToArray());
            escritor.Write(16);
            escritor.Write((short)1);
            escritor.Write((short)1);
            escritor.Write(Muestreo);
            escritor.Write(Muestreo * 2);
            escritor.Write((short)2);
            escritor.Write((short)16);
            escritor.Write("data"u8.ToArray());
            escritor.Write(bytesDatos);
            foreach (var muestra in muestras)
            {
                escritor.Write(muestra);
            }
        }

        flujo.Position = 0;
        return flujo;
    }
}
