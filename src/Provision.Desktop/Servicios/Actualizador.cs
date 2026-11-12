using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Provision.Core.Actualizaciones;
using Provision.Core.Api;

namespace Provision.Desktop.Servicios;

/// <summary>
/// Versión de esta app y reemplazo del ejecutable por la versión nueva.
/// La compilación sale del número de versión (0.1.N → N); las compilaciones locales son 0 y nunca se actualizan.
/// </summary>
public static class Actualizador
{
    public static int Compilacion { get; } = Assembly.GetEntryAssembly()?.GetName().Version?.Build ?? 0;

    public static string VersionTexto => Compilacion > 0 ? $"Versión 0.1.{Compilacion}" : "Versión de desarrollo";

    /// <summary>Ruta del Provision.exe que se está ejecutando (null si se corre con dotnet run).</summary>
    private static string? Ejecutable =>
        Environment.ProcessPath is { } p && Path.GetFileName(p).Equals("Provision.exe", StringComparison.OrdinalIgnoreCase) ? p : null;

    /// <summary>Si la app puede reemplazarse sola (Windows, ejecutable publicado).</summary>
    public static bool PuedeReemplazarse => OperatingSystem.IsWindows() && Ejecutable is not null && Compilacion > 0;

    /// <summary>Borra el ejecutable anterior que quedó de la última actualización.</summary>
    public static void LimpiarAnterior()
    {
        if (Ejecutable is not { } exe) return;
        try { File.Delete(exe + ".anterior"); } catch { /* sigue en uso o sin permiso: se borra la próxima vez */ }
    }

    /// <summary>
    /// Descarga la versión nueva junto al ejecutable actual, la pone en su lugar y la abre.
    /// Windows no deja sobrescribir un .exe en uso, pero sí renombrarlo: el actual pasa a ".anterior".
    /// </summary>
    public static async Task DescargarEInstalarAsync(VersionPublicada version, IProgress<double> progreso, CancellationToken ct = default)
    {
        var exe = Ejecutable ?? throw new InvalidOperationException("Esta copia de la app no se puede actualizar sola.");
        ExigirOrigen(version.Url);
        var nuevo = exe + ".nuevo";
        var anterior = exe + ".anterior";

        byte[] hash;
        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) })
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Provision-Escritorio");
            using var resp = await http.GetAsync(version.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            if (!OrigenSeguro.EsDestinoPermitido(resp.RequestMessage?.RequestUri))
            {
                throw new InvalidOperationException("La descarga fue redirigida fuera de GitHub. No se instaló nada.");
            }

            var total = resp.Content.Headers.ContentLength;
            using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var origen = await resp.Content.ReadAsStreamAsync(ct);
            await using var destino = File.Create(nuevo);
            var buffer = new byte[81920];
            long leidos = 0;
            int n;
            while ((n = await origen.ReadAsync(buffer, ct)) > 0)
            {
                await destino.WriteAsync(buffer.AsMemory(0, n), ct);
                sha256.AppendData(buffer, 0, n);
                leidos += n;
                if (total > 0) progreso.Report((double)leidos / total.Value);
            }
            if (total > 0 && leidos != total) throw new IOException("La descarga quedó incompleta. Intenta de nuevo.");
            hash = sha256.GetHashAndReset();
        }

        if (!OrigenSeguro.HashAceptable(hash, version.Sha256))
        {
            File.Delete(nuevo);
            throw new IOException("La descarga no coincide con la versión publicada (SHA-256). No se instaló nada.");
        }

        if (File.Exists(anterior)) File.Delete(anterior);
        File.Move(exe, anterior);
        try
        {
            File.Move(nuevo, exe);
        }
        catch
        {
            File.Move(anterior, exe); // deja todo como estaba
            throw;
        }
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! });
    }

    /// <summary>Plan B: abrir la descarga en el navegador.</summary>
    public static void AbrirEnNavegador(string url)
    {
        ExigirOrigen(url);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void ExigirOrigen(string url)
    {
        if (!OrigenSeguro.EsUrlPermitida(url, Repositorio))
        {
            throw new InvalidOperationException("La dirección de descarga no es una versión publicada de Provisión. No se instaló nada.");
        }
    }

    private const string Repositorio = "provision-desktop";
}
