using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

public sealed record FilaUsuario(Usuario Usuario, string Sucursal)
{
    public string Nombre => Usuario.Nombre;
    public string Email => Usuario.Email;
    public string Rol => Usuario.NombreRol;
    public bool Activo => Usuario.Activo;
}

/// <summary>Alta de usuarios, cambio de rol y sucursal, activar/desactivar y restablecer contraseña.</summary>
public sealed partial class UsuariosViewModel(Sesion sesion) : ViewModelBase, ISeccion
{
    /// <summary>Descripción legible de cada permiso, para explicar qué hace cada rol.</summary>
    private static readonly Dictionary<string, string> Descripciones = new()
    {
        ["auditoria.ver"] = "Ver la auditoría de todos los roles",
        ["usuarios.administradores"] = "Gestionar administradores",
        ["usuarios.gestionar"] = "Gestionar usuarios",
        ["sucursales.gestionar"] = "Gestionar sucursales",
        ["sucursales.todas"] = "Operar en todas las sucursales",
        ["catalogo.ver"] = "Ver catálogo",
        ["catalogo.gestionar"] = "Crear, editar y eliminar productos",
        ["inventario.ver"] = "Ver inventario",
        ["inventario.recibir"] = "Recibir mercadería",
        ["inventario.bajas"] = "Registrar bajas",
        ["inventario.ajustar"] = "Ajustes de inventario y transferencias",
        ["clientes.ver"] = "Ver clientes",
        ["clientes.gestionar"] = "Registrar y editar clientes",
        ["recetas.gestionar"] = "Registrar recetas",
        ["ventas.ver"] = "Ver ventas",
        ["ventas.crear"] = "Registrar ventas",
        ["ventas.anular"] = "Anular ventas",
        ["precios.modificar"] = "Cambiar precios al vender",
        ["caja.operar"] = "Operar caja y cobrar",
        ["caja.retiro_dueno"] = "Retiros del dueño",
        ["laboratorio.ver"] = "Ver órdenes de laboratorio",
        ["laboratorio.gestionar"] = "Avanzar órdenes de laboratorio",
        ["laboratorio.entregar"] = "Entregar lentes al cliente",
        ["laboratorio.cancelar"] = "Cancelar órdenes",
        ["reportes.ver"] = "Ver reportes del negocio",
    };

    [ObservableProperty] private IReadOnlyList<FilaUsuario> _usuarios = [];
    [ObservableProperty] private IReadOnlyList<RolInfo> _roles = [];
    [ObservableProperty] private IReadOnlyList<Sucursal> _sucursales = [];

    public Task ActivarAsync() => CargarAsync();

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        // Solo los roles que este usuario puede asignar (un administrador no asigna administradores).
        if (Roles.Count == 0) Roles = (await sesion.Api.RolesAsync()).Where(r => r.Asignable).ToList();
        Sucursales = await sesion.Api.SucursalesAsync();
        var nombres = Sucursales.ToDictionary(s => s.Id, s => s.Nombre);
        var seleccionadoId = Seleccionado?.Usuario.Id;
        Usuarios = (await sesion.Api.UsuariosAsync())
            .Select(u => new FilaUsuario(u, u.SucursalId is Guid id && nombres.TryGetValue(id, out var n) ? n : "—"))
            .ToList();
        Seleccionado = Usuarios.FirstOrDefault(f => f.Usuario.Id == seleccionadoId);
        NuevoRol ??= Roles.FirstOrDefault(r => r.Rol == Rol.Vendedor);
        NuevaSucursal ??= Sucursales.FirstOrDefault(s => s.Id == sesion.Usuario.SucursalId) ?? Sucursales.FirstOrDefault();
    });

    private static string Explicar(RolInfo? rol) =>
        rol is null ? "" : string.Join(" · ", rol.Permisos.Select(p => Descripciones.GetValueOrDefault(p, p)));

    /* ─────────── Editar ─────────── */

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion), nameof(EsUnoMismo))]
    private FilaUsuario? _seleccionado;

    public bool HaySeleccion => Seleccionado is not null;
    public bool EsUnoMismo => Seleccionado?.Usuario.Id == sesion.Usuario.Id;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editable))]
    private bool _protegido;

    /// <summary>Se puede editar si no es una cuenta protegida para este usuario.</summary>
    public bool Editable => !Protegido;

    [ObservableProperty] private string _editNombre = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PermisosEdit))]
    private RolInfo? _editRol;

    [ObservableProperty] private Sucursal? _editSucursal;
    [ObservableProperty] private bool _editActivo;
    [ObservableProperty] private string _editPassword = "";

    public string PermisosEdit => Explicar(EditRol);

    partial void OnSeleccionadoChanged(FilaUsuario? value)
    {
        if (value is null) return;
        EditNombre = value.Usuario.Nombre;
        EditRol = Roles.FirstOrDefault(r => r.Rol == value.Usuario.Rol);
        // Cuentas que este usuario no puede modificar (p. ej. un administrador frente a un super administrador).
        Protegido = EditRol is null;
        EditSucursal = Sucursales.FirstOrDefault(s => s.Id == value.Usuario.SucursalId);
        EditActivo = value.Usuario.Activo;
        EditPassword = "";
    }

    [RelayCommand]
    private async Task GuardarCambiosAsync()
    {
        if (Seleccionado is not { } fila) return;
        if (EditNombre.Trim().Length < 2) { Falla("El nombre es muy corto"); return; }
        if (EditPassword.Length is > 0 and < 8) { Falla("La contraseña nueva debe tener al menos 8 caracteres"); return; }
        var cambios = new CambiosUsuario
        {
            Nombre = EditNombre.Trim(),
            Rol = EditRol?.Rol,
            SucursalId = EditSucursal?.Id,
            Activo = EditActivo,
            Password = EditPassword.Length > 0 ? EditPassword : null,
        };
        if (!await IntentarAsync(() => sesion.Api.ActualizarUsuarioAsync(fila.Usuario.Id, cambios))) return;
        EditPassword = "";
        await CargarAsync();
        Aviso = $"{cambios.Nombre} actualizado. Los cambios aplican desde su próxima acción, sin volver a ingresar.";
    }

    /* ─────────── Nuevo ─────────── */

    [ObservableProperty] private bool _creando;
    [ObservableProperty] private string _nuevoNombre = "";
    [ObservableProperty] private string _nuevoEmail = "";
    [ObservableProperty] private string _nuevoPassword = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PermisosNuevo))]
    private RolInfo? _nuevoRol;

    [ObservableProperty] private Sucursal? _nuevaSucursal;

    public string PermisosNuevo => Explicar(NuevoRol);

    [RelayCommand]
    private void AlternarNuevo() => Creando = !Creando;

    [RelayCommand]
    private async Task CrearAsync()
    {
        if (NuevoNombre.Trim().Length < 2 || !NuevoEmail.Contains('@')) { Falla("Completa nombre y un correo válido"); return; }
        if (NuevoPassword.Length < 8) { Falla("La contraseña debe tener al menos 8 caracteres"); return; }
        if (NuevoRol is null) { Falla("Elige el rol"); return; }
        var nuevo = new NuevoUsuario
        {
            Nombre = NuevoNombre.Trim(),
            Email = NuevoEmail.Trim(),
            Password = NuevoPassword,
            Rol = NuevoRol.Rol,
            SucursalId = NuevaSucursal?.Id,
        };
        if (!await IntentarAsync(() => sesion.Api.CrearUsuarioAsync(nuevo))) return;
        NuevoNombre = NuevoEmail = NuevoPassword = "";
        Creando = false;
        await CargarAsync();
        Aviso = $"Usuario {nuevo.Email} creado como {NuevoRol.Nombre}. Ya puede ingresar con la contraseña que le diste.";
    }
}
