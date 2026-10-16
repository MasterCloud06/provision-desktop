using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Configuracion;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Una pantalla del menú lateral. Se recarga cada vez que se muestra.</summary>
public interface ISeccion
{
    Task ActivarAsync();
}

public sealed partial class OpcionMenu(string clave, string titulo, string icono, Func<ViewModelBase> crear) : ObservableObject
{
    private ViewModelBase? _vista;

    /// <summary>Identificador de la pantalla (el mismo que usa el backend en las tareas del panel).</summary>
    public string Clave { get; } = clave;
    public string Titulo { get; } = titulo;
    public string Icono { get; } = icono;

    [ObservableProperty] private bool _activo;

    /// <summary>La pantalla se crea la primera vez y se conserva (así el carrito de venta no se pierde).</summary>
    public ViewModelBase Vista => _vista ??= crear();

    public IRelayCommand? Seleccionar { get; set; }
}

public sealed partial class ShellViewModel : ViewModelBase
{
    private readonly Action _alSalir;

    public ShellViewModel(Sesion sesion, Action alSalir)
    {
        Sesion = sesion;
        _alSalir = alSalir;
        // Cada pantalla aparece solo si el rol tiene el permiso que la habilita (null = todos).
        var opciones = new (string clave, string titulo, string icono, string? permiso, Func<ViewModelBase> crear)[]
        {
            ("panel", "Mi panel", "◈", null, () => new PanelViewModel(sesion, IrAAsync, TienePantalla)),
            ("indicadores", "Indicadores", "◧", "reportes.ver", () => new InicioViewModel(sesion)),
            ("auditoria", "Auditoría", "◉", "auditoria.ver", () => new AuditoriaViewModel(sesion)),
            ("vender", "Vender", "＋", "ventas.crear", () => new VenderViewModel(sesion)),
            ("ventas", "Ventas", "≡", "ventas.ver", () => new VentasViewModel(sesion)),
            ("pedidos", "Pedidos web", "⬚", "tienda.gestionar", () => new PedidosWebViewModel(sesion)),
            ("caja", "Caja", "▣", "caja.operar", () => new CajaViewModel(sesion)),
            ("laboratorio", "Laboratorio", "◎", "laboratorio.ver", () => new LaboratorioViewModel(sesion)),
            ("clientes", "Clientes", "☺", "clientes.ver", () => new ClientesViewModel(sesion)),
            ("inventario", "Inventario", "▤", "catalogo.ver", () => new InventarioViewModel(sesion)),
            ("usuarios", "Usuarios", "⚿", "usuarios.gestionar", () => new UsuariosViewModel(sesion)),
            ("finanzas", "Mis finanzas", "◔", null, () => new FinanzasViewModel(sesion)),
        };
        Menu = opciones
            .Where(o => o.permiso is null || sesion.Puede(o.permiso))
            .Select(o => new OpcionMenu(o.clave, o.titulo, o.icono, o.crear))
            .ToList();
        foreach (var opcion in Menu)
        {
            var o = opcion;
            o.Seleccionar = new AsyncRelayCommand(() => IrAAsync(o));
        }
        _ = IrAAsync(Menu[0]);
    }

    public Sesion Sesion { get; }
    public IReadOnlyList<OpcionMenu> Menu { get; }

    public string NombreUsuario => Sesion.Usuario.Nombre;
    public string RolUsuario => string.IsNullOrEmpty(Sesion.Usuario.NombreRol)
        ? Core.Api.Enumeraciones.Etiqueta(Sesion.Usuario.Rol)
        : Sesion.Usuario.NombreRol;
    public string NombreEntorno => Sesion.Entorno.Nombre.ToUpperInvariant();
    public bool EsDesarrollo => Sesion.Entorno.Clave == Entorno.Desarrollo.Clave;

    [ObservableProperty] private ViewModelBase? _seccionActual;

    /// <summary>Lleva a una pantalla por su clave. Devuelve false si el rol no tiene esa pantalla.</summary>
    public bool TienePantalla(string clave) => Menu.Any(o => o.Clave == clave);

    private Task IrAAsync(string clave)
    {
        var opcion = Menu.FirstOrDefault(o => o.Clave == clave);
        return opcion is null ? Task.CompletedTask : IrAAsync(opcion);
    }

    private async Task IrAAsync(OpcionMenu opcion)
    {
        foreach (var o in Menu) o.Activo = o == opcion;
        SeccionActual = opcion.Vista;
        if (opcion.Vista is ISeccion s) await s.ActivarAsync();
    }

    [RelayCommand]
    private void CerrarSesion() => _alSalir();
}
