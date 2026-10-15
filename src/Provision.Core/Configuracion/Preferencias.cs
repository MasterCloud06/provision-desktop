using System.Text.Json;

namespace Provision.Core.Configuracion;

/// <summary>
/// Preferencias guardadas en la PC del usuario (%APPDATA%\Provision\preferencias.json en Windows).
/// Nunca guarda contraseñas ni tokens.
/// </summary>
public sealed class Preferencias
{
    public string Entorno { get; set; } = Configuracion.Entorno.Local.Clave;
    public string? UltimoEmail { get; set; }
    /// <summary>URL de la API para el entorno local, por si el backend corre en otro puerto.</summary>
    public string? UrlLocal { get; set; }

    private static string Carpeta =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Provision");

    private static string Archivo => Path.Combine(Carpeta, "preferencias.json");

    public static Preferencias Cargar()
    {
        try
        {
            return File.Exists(Archivo)
                ? JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Archivo)) ?? new()
                : new();
        }
        catch
        {
            return new(); // archivo dañado: se empieza de nuevo
        }
    }

    public void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Carpeta);
            File.WriteAllText(Archivo, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // No poder guardar preferencias no debe impedir usar la app.
        }
    }

    public Entorno EntornoSeleccionado()
    {
        var e = Configuracion.Entorno.PorClave(Entorno);
        return e == Configuracion.Entorno.Local ? e.ConUrl(UrlLocal) : e;
    }
}
