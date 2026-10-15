namespace Provision.Core.Configuracion;

/// <summary>
/// Los mismos entornos que el backend: cada uno apunta a su propia API y base de datos.
/// </summary>
public sealed record Entorno(string Clave, string Nombre, string UrlApi)
{
    public static readonly Entorno Local =
        new("local", "Local", "http://localhost:3000/api/v1");

    public static readonly Entorno Desarrollo =
        new("desarrollo", "Desarrollo", "https://provision-backend-ib50.onrender.com/api/v1");

    public static IReadOnlyList<Entorno> Todos { get; } = [Local, Desarrollo];

    public static Entorno PorClave(string? clave) =>
        Todos.FirstOrDefault(e => string.Equals(e.Clave, clave, StringComparison.OrdinalIgnoreCase)) ?? Local;

    /// <summary>Permite apuntar a otra URL (por ejemplo, otro puerto local) conservando la clave.</summary>
    public Entorno ConUrl(string? url) => string.IsNullOrWhiteSpace(url) ? this : this with { UrlApi = url.TrimEnd('/') };

    public override string ToString() => Nombre;
}
