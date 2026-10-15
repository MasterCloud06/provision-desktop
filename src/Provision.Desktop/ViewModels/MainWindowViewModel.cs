using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Alterna entre la pantalla de ingreso y la aplicación.</summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private ViewModelBase _contenido;

    public MainWindowViewModel()
    {
        _contenido = new LoginViewModel(AlIngresar);
    }

    private void AlIngresar(Sesion sesion)
    {
        sesion.Api.Http.SesionExpirada += (_, _) =>
            Dispatcher.UIThread.Post(() => VolverAlLogin("Tu sesión expiró. Vuelve a ingresar."));
        Contenido = new ShellViewModel(sesion, () => VolverAlLogin(null));
    }

    private void VolverAlLogin(string? mensaje)
    {
        if (Contenido is ShellViewModel shell) shell.Sesion.Api.CerrarSesion();
        var login = new LoginViewModel(AlIngresar) { Error = mensaje };
        Contenido = login;
    }
}
