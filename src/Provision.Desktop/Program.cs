using Avalonia;

namespace Provision.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Cada petición dice qué compilación es: el backend bloquea las que ya no son la última.
        Core.Api.ApiClient.IdentificacionApp = $"escritorio/{Servicios.Actualizador.Compilacion}";
        Servicios.Actualizador.LimpiarAnterior();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // También lo usa el diseñador visual del IDE.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
