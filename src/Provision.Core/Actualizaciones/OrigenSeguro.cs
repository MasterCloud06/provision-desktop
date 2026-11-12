namespace Provision.Core.Actualizaciones;

/// <summary>
/// Reglas para instalar una versión nueva: solo desde las Releases de Provisión en GitHub, por HTTPS, y con el
/// SHA-256 que publica el backend cuando lo trae (ENAI lo publica; el backend antiguo no).
/// </summary>
public static class OrigenSeguro
{
    /// <summary>La URL que publica el backend debe ser una Release del repositorio indicado en github.com.</summary>
    public static bool EsUrlPermitida(string? url, string repositorio) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttps
        && u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        && u.AbsolutePath.StartsWith($"/MasterCloud06/{repositorio}/releases/download/", StringComparison.OrdinalIgnoreCase);

    /// <summary>Destino final tras las redirecciones: github.com o sus servidores de descargas.</summary>
    public static bool EsDestinoPermitido(Uri? u) =>
        u is not null
        && u.Scheme == Uri.UriSchemeHttps
        && (u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || u.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// false solo si el backend publicó un hash y no coincide con lo descargado.
    /// shortcut: sin hash se acepta mientras las apps hablen con el backend antiguo, que no lo publica;
    /// hacerlo obligatorio en la versión que apunte a ENAI.
    /// </summary>
    public static bool HashAceptable(byte[] sha256Descargado, string? sha256Publicado) =>
        string.IsNullOrWhiteSpace(sha256Publicado)
        || Convert.ToHexString(sha256Descargado).Equals(sha256Publicado.Trim(), StringComparison.OrdinalIgnoreCase);
}
