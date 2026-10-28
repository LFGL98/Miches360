using System.ComponentModel;
using System.Diagnostics;

namespace Miches360.Servicios;

public static class FirewallWindows
{
    public const string NombreRegla = "Miches 360 - celulares";

    public static async Task<string?> PermitirPuertoAsync(int puerto)
    {
        var comando =
            $"/c netsh advfirewall firewall delete rule name=\"{NombreRegla}\" >nul 2>&1 & " +
            $"netsh advfirewall firewall add rule name=\"{NombreRegla}\" dir=in action=allow protocol=TCP localport={puerto} profile=private,public";

        try
        {
            using var proceso = Process.Start(new ProcessStartInfo("cmd.exe", comando)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (proceso is null)
            {
                return "No se pudo ejecutar el comando del firewall.";
            }

            await proceso.WaitForExitAsync();
            return proceso.ExitCode == 0 ? null : $"El firewall respondió con el código {proceso.ExitCode}.";
        }
        catch (Win32Exception)
        {
            return "Se canceló el permiso de administrador.";
        }
        catch (Exception ex)
        {
            return $"No se pudo abrir el puerto: {ex.Message}";
        }
    }
}
