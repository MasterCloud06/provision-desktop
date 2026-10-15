using Provision.Core.Api;
using Provision.Core.Configuracion;

namespace Provision.Desktop.Servicios;

/// <summary>Lo que dura mientras el usuario tiene la sesión abierta.</summary>
public sealed class Sesion(Entorno entorno, ProvisionApi api, Usuario usuario)
{
    public Entorno Entorno { get; } = entorno;
    public ProvisionApi Api { get; } = api;
    public Usuario Usuario { get; } = usuario;
    public bool EsAdmin => Usuario.Rol == Rol.Admin;
}
