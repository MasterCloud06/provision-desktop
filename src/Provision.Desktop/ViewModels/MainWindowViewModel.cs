using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Provision.Core.Api;
using Provision.Core.Configuracion;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Alterna entre la pantalla de ingreso y la aplicación, y bloquea todo si hay que actualizar.</summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _contenido;

    /// <summary>Si no es null, la app está bloqueada hasta instalar la versión nueva.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Bloqueada))]
    private ActualizacionViewModel? _actualizacion;

    public bool Bloqueada => Actualizacion is not null;

    private Sesion? _sesion;
    private readonly DispatcherTimer _revision;

    public MainWindowViewModel()
    {
        _contenido = new LoginViewModel(AlIngresar);
        // Cualquier respuesta 426 del backend (al ingresar o en cualquier pantalla) bloquea la app.
        ApiClient.ActualizacionRequerida += (_, ultima) => Dispatcher.UIThread.Post(() => Bloquear(ultima));
        // Además se revisa al abrir y cada 15 minutos, por si la app queda abierta todo el día.
        _ = RevisarAsync();
        _revision = new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
        _revision.Tick += (_, _) => _ = RevisarAsync();
        _revision.Start();
    }

    private void Bloquear(VersionPublicada? ultima)
    {
        if (Actualizador.Compilacion == 0) return; // compilación de desarrollo
        if (Actualizacion is { Descargando: true }) return;
        Actualizacion = new ActualizacionViewModel(ultima ?? Actualizacion?.Ultima);
    }

    /// <summary>Consulta la última versión en el servidor de la sesión (o el elegido al ingresar).</summary>
    private async Task RevisarAsync()
    {
        if (Actualizador.Compilacion == 0) return;
        var url = _sesion?.Entorno.UrlApi ?? Preferencias.Cargar().EntornoSeleccionado().UrlApi;
        try
        {
            using var http = new ApiClient(url);
            var info = await new ProvisionApi(http).ActualizacionesAsync();
            if (info is { Obligatoria: true, Escritorio: { } ultima } && ultima.Compilacion > Actualizador.Compilacion)
                Dispatcher.UIThread.Post(() => Bloquear(ultima));
        }
        catch
        {
            // Sin conexión o servidor dormido: se vuelve a intentar en la próxima revisión.
        }
    }

    private void AlIngresar(Sesion sesion)
    {
        _sesion = sesion;
        sesion.Api.Http.SesionExpirada += (_, _) =>
            Dispatcher.UIThread.Post(() => VolverAlLogin("Tu sesión expiró. Vuelve a ingresar."));
        Contenido = new ShellViewModel(sesion, () => VolverAlLogin(null));
    }

    private void VolverAlLogin(string? mensaje)
    {
        if (Contenido is ShellViewModel shell) shell.Sesion.Api.CerrarSesion();
        _sesion = null;
        var login = new LoginViewModel(AlIngresar) { Error = mensaje };
        Contenido = login;
    }
}
