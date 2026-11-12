using Provision.Core.Api;
using Provision.Core.Configuracion;

namespace Provision.Desktop.Servicios;

/// <summary>Lo que dura mientras el usuario tiene la sesión abierta.</summary>
public sealed class Sesion(Entorno entorno, ProvisionApi api, Usuario usuario)
{
    public Entorno Entorno { get; } = entorno;
    public ProvisionApi Api { get; } = api;
    public Usuario Usuario { get; private set; } = usuario;
    public event EventHandler? PermisosCambiados;
    private readonly SemaphoreSlim _refresco = new(1, 1);

    public async Task RefrescarAsync()
    {
        await _refresco.WaitAsync();
        try
        {
            var actual = await Api.YoAsync();
            var cambio = actual.Rol != Usuario.Rol || actual.SucursalId != Usuario.SucursalId ||
                actual.Nombre != Usuario.Nombre || !actual.Permisos.ToHashSet().SetEquals(Usuario.Permisos);
            Usuario = actual;
            if (cambio) PermisosCambiados?.Invoke(this, EventArgs.Empty);
        }
        finally { _refresco.Release(); }
    }
    public bool EsAdmin => Usuario.Rol == Rol.Admin;

    /// <summary>Los permisos vienen del backend; la app solo los usa para mostrar u ocultar cosas.</summary>
    public bool Puede(string permiso) => Usuario.Puede(permiso);
}
