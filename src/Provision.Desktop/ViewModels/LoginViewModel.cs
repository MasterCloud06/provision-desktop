using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Core.Configuracion;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly Action<Sesion> _alIngresar;
    private readonly Preferencias _preferencias = Preferencias.Cargar();

    public LoginViewModel(Action<Sesion> alIngresar)
    {
        _alIngresar = alIngresar;
        var elegido = _preferencias.EntornoSeleccionado();
        _entornoSeleccionado = Entornos.First(e => e.Clave == elegido.Clave);
        _email = _preferencias.UltimoEmail ?? "";
    }

    public IReadOnlyList<Entorno> Entornos { get; } = Entorno.Todos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UrlDestino), nameof(EsDesarrollo))]
    private Entorno _entornoSeleccionado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IngresarCommand))]
    private string _email;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IngresarCommand))]
    private string _password = "";

    public string UrlDestino => Destino().UrlApi;
    public bool EsDesarrollo => EntornoSeleccionado.Clave == Entorno.Desarrollo.Clave;

    private Entorno Destino() =>
        EntornoSeleccionado.Clave == Entorno.Local.Clave ? EntornoSeleccionado.ConUrl(_preferencias.UrlLocal) : EntornoSeleccionado;

    private bool PuedeIngresar() => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrEmpty(Password) && !Ocupado;

    [RelayCommand(CanExecute = nameof(PuedeIngresar))]
    private async Task IngresarAsync()
    {
        var entorno = Destino();
        var api = new ProvisionApi(new ApiClient(entorno.UrlApi));
        RespuestaLogin? r = null;
        var espera = EsDesarrollo ? "Conectando… el servidor de desarrollo puede tardar hasta un minuto si estaba dormido." : "Conectando…";
        var ok = await IntentarAsync(async () => r = await api.LoginAsync(Email.Trim(), Password), mensajeEspera: espera);
        if (!ok || r is null)
        {
            api.Http.Dispose();
            return;
        }

        _preferencias.Entorno = entorno.Clave;
        _preferencias.UltimoEmail = Email.Trim();
        _preferencias.Guardar();
        Password = "";
        _alIngresar(new Sesion(entorno, api, r.Usuario));
    }

    protected override void AlCambiarOcupado(bool ocupado) => IngresarCommand.NotifyCanExecuteChanged();
}
