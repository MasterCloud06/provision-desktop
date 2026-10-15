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

public sealed partial class OpcionMenu(string titulo, string icono, Func<ViewModelBase> crear) : ObservableObject
{
    private ViewModelBase? _vista;

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
        Menu =
        [
            new("Inicio", "◧", () => new InicioViewModel(sesion)),
            new("Vender", "＋", () => new VenderViewModel(sesion)),
            new("Ventas", "≡", () => new VentasViewModel(sesion)),
            new("Caja", "▣", () => new CajaViewModel(sesion)),
            new("Inventario", "▤", () => new InventarioViewModel(sesion)),
            new("Mis finanzas", "◔", () => new FinanzasViewModel(sesion)),
        ];
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
    public string RolUsuario => Sesion.Usuario.Rol.ToString().ToUpperInvariant();
    public string NombreEntorno => Sesion.Entorno.Nombre.ToUpperInvariant();
    public bool EsDesarrollo => Sesion.Entorno.Clave == Entorno.Desarrollo.Clave;

    [ObservableProperty] private ViewModelBase? _seccionActual;

    private async Task IrAAsync(OpcionMenu opcion)
    {
        foreach (var o in Menu) o.Activo = o == opcion;
        SeccionActual = opcion.Vista;
        if (opcion.Vista is ISeccion s) await s.ActivarAsync();
    }

    [RelayCommand]
    private void CerrarSesion() => _alSalir();
}
