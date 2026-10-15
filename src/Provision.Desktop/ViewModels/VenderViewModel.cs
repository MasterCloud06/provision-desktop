using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Una línea del carrito.</summary>
public sealed partial class LineaCarrito : ObservableObject
{
    private readonly VenderViewModel _venta;

    public LineaCarrito(VenderViewModel venta, Producto producto)
    {
        _venta = venta;
        Producto = producto;
    }

    public Producto Producto { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Total))]
    private int _cantidad = 1;

    public long Total => Producto.PrecioVenta * Cantidad;

    [RelayCommand]
    private void Sumar()
    {
        if (Producto.ControlaStock && Cantidad >= Producto.Stock)
        {
            _venta.MostrarError($"Solo hay {Producto.Stock} unidades de \"{Producto.Nombre}\"");
            return;
        }
        Cantidad++;
    }

    [RelayCommand]
    private void Restar()
    {
        if (Cantidad > 1) Cantidad--;
        else _venta.Quitar(this);
    }

    [RelayCommand]
    private void Quitar() => _venta.Quitar(this);

    partial void OnCantidadChanged(int value) => _venta.Recalcular();
}

/// <summary>Un pago ya agregado a la venta.</summary>
public sealed partial class PagoAgregado(VenderViewModel venta, MetodoPago metodo, long monto, string? referencia) : ObservableObject
{
    public MetodoPago Metodo { get; } = metodo;
    public long Monto { get; } = monto;
    public string? Referencia { get; } = referencia;
    public string Texto => Enumeraciones.Etiqueta(Metodo) + (string.IsNullOrEmpty(Referencia) ? "" : $" · {Referencia}");

    [RelayCommand]
    private void Quitar() => venta.QuitarPago(this);
}

/// <summary>Punto de venta: buscar productos, armar el carrito, cliente, pagos y cobrar.</summary>
public sealed partial class VenderViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public VenderViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _metodoSeleccionado = Metodos[0];
    }

    /* Búsqueda de productos */

    [ObservableProperty] private string _busqueda = "";
    [ObservableProperty] private IReadOnlyList<Producto> _resultados = [];

    public Task ActivarAsync() => BuscarAsync();

    [RelayCommand]
    private Task BuscarAsync() => IntentarAsync(async () =>
    {
        var r = await _sesion.Api.ProductosAsync(buscar: Busqueda, activo: true, porPagina: 50);
        Resultados = r.Datos;
    });

    [RelayCommand]
    private void Agregar(Producto producto)
    {
        Error = null;
        var existente = Carrito.FirstOrDefault(l => l.Producto.Id == producto.Id);
        if (existente is not null)
        {
            existente.SumarCommand.Execute(null);
            return;
        }
        if (producto.ControlaStock && producto.Stock <= 0)
        {
            MostrarError($"\"{producto.Nombre}\" no tiene stock en esta sucursal");
            return;
        }
        Carrito.Add(new LineaCarrito(this, producto));
        Recalcular();
    }

    /* Carrito */

    public ObservableCollection<LineaCarrito> Carrito { get; } = [];

    [ObservableProperty] private string _descuentoTexto = "";
    [ObservableProperty] private long _subtotal;
    [ObservableProperty] private long _descuento;
    [ObservableProperty] private long _total;
    [ObservableProperty] private long _pagado;
    [ObservableProperty] private long _saldo;
    [ObservableProperty] private bool _llevaLentes;

    partial void OnDescuentoTextoChanged(string value) => Recalcular();

    internal void Quitar(LineaCarrito linea)
    {
        Carrito.Remove(linea);
        Recalcular();
    }

    internal void MostrarError(string mensaje) => Falla(mensaje);

    internal void Recalcular()
    {
        Subtotal = Carrito.Sum(l => l.Total);
        Descuento = Dinero.TryLeer(DescuentoTexto, out var d) ? Math.Clamp(d, 0, Subtotal) : 0;
        Total = Subtotal - Descuento;
        Pagado = Pagos.Sum(p => p.Monto);
        Saldo = Total - Pagado;
        LlevaLentes = Carrito.Any(l => l.Producto.Tipo == TipoProducto.LenteOftalmico);
        if (string.IsNullOrWhiteSpace(MontoPagoTexto) || Saldo > 0) MontoPagoTexto = Saldo > 0 ? Dinero.ParaEditar(Saldo) : "";
        CobrarCommand.NotifyCanExecuteChanged();
    }

    /* Cliente */

    [ObservableProperty] private string _busquedaCliente = "";
    [ObservableProperty] private IReadOnlyList<Cliente> _clientesEncontrados = [];
    [ObservableProperty] private Cliente? _cliente;
    [ObservableProperty] private bool _creandoCliente;
    [ObservableProperty] private string _nuevoClienteNombre = "";
    [ObservableProperty] private string _nuevoClienteCi = "";
    [ObservableProperty] private string _nuevoClienteTelefono = "";

    [RelayCommand]
    private Task BuscarClienteAsync() => IntentarAsync(async () =>
    {
        ClientesEncontrados = await _sesion.Api.BuscarClientesAsync(BusquedaCliente);
        if (ClientesEncontrados.Count == 0) Aviso = "No se encontró ningún cliente. Puedes registrarlo.";
    });

    [RelayCommand]
    private void QuitarCliente() => Cliente = null;

    [RelayCommand]
    private void AlternarNuevoCliente()
    {
        CreandoCliente = !CreandoCliente;
        if (CreandoCliente && string.IsNullOrEmpty(NuevoClienteNombre)) NuevoClienteNombre = BusquedaCliente;
    }

    [RelayCommand]
    private async Task GuardarClienteAsync()
    {
        if (NuevoClienteNombre.Trim().Length < 2) { Falla("Escribe el nombre del cliente"); return; }
        Cliente? creado = null;
        var ok = await IntentarAsync(async () => creado = await _sesion.Api.CrearClienteAsync(new NuevoCliente
        {
            Nombre = NuevoClienteNombre.Trim(),
            Ci = string.IsNullOrWhiteSpace(NuevoClienteCi) ? null : NuevoClienteCi.Trim(),
            Telefono = string.IsNullOrWhiteSpace(NuevoClienteTelefono) ? null : NuevoClienteTelefono.Trim(),
        }));
        if (!ok) return;
        Cliente = creado;
        CreandoCliente = false;
        NuevoClienteNombre = NuevoClienteCi = NuevoClienteTelefono = "";
    }

    partial void OnClienteChanged(Cliente? value) => CobrarCommand.NotifyCanExecuteChanged();

    /* Pagos */

    public IReadOnlyList<Opcion<MetodoPago>> Metodos { get; } =
        Enum.GetValues<MetodoPago>().Select(m => new Opcion<MetodoPago>(m, Enumeraciones.Etiqueta(m))).ToList();

    public ObservableCollection<PagoAgregado> Pagos { get; } = [];

    [ObservableProperty] private Opcion<MetodoPago> _metodoSeleccionado;
    [ObservableProperty] private string _montoPagoTexto = "";
    [ObservableProperty] private string _referenciaPago = "";

    [RelayCommand]
    private void AgregarPago()
    {
        if (!Dinero.TryLeer(MontoPagoTexto, out var monto) || monto <= 0) { Falla("Monto de pago inválido"); return; }
        if (monto > Saldo)
        {
            // En efectivo el cliente suele entregar de más: se cobra el saldo y se informa el cambio.
            if (MetodoSeleccionado.Valor == MetodoPago.Efectivo && Saldo > 0)
            {
                Aviso = $"Cambio a devolver: {Dinero.Formatear(monto - Saldo)}";
                monto = Saldo;
            }
            else { Falla("El pago supera lo que falta cobrar"); return; }
        }
        else Error = null;
        Pagos.Add(new PagoAgregado(this, MetodoSeleccionado.Valor, monto,
            string.IsNullOrWhiteSpace(ReferenciaPago) ? null : ReferenciaPago.Trim()));
        ReferenciaPago = "";
        MontoPagoTexto = "";
        Recalcular();
    }

    internal void QuitarPago(PagoAgregado pago)
    {
        Pagos.Remove(pago);
        Recalcular();
    }

    /* Orden de trabajo (lentes) */

    [ObservableProperty] private DateTime? _fechaPrometida = DateTime.Today.AddDays(3);
    [ObservableProperty] private string _instrucciones = "";

    /* Cobrar */

    [ObservableProperty] private VentaRegistrada? _ultimaVenta;

    private bool PuedeCobrar() => Carrito.Count > 0 && (Saldo == 0 || Cliente is not null);

    [RelayCommand(CanExecute = nameof(PuedeCobrar))]
    private async Task CobrarAsync()
    {
        var venta = new NuevaVenta
        {
            ClienteId = Cliente?.Id,
            Items = Carrito.Select(l => new LineaVenta { ProductoId = l.Producto.Id, Cantidad = l.Cantidad }).ToList(),
            Descuento = Descuento > 0 ? Descuento : null,
            Pagos = Pagos.Select(p => new Pago { Metodo = p.Metodo, Monto = p.Monto, Referencia = p.Referencia }).ToList(),
            OrdenTrabajo = LlevaLentes
                ? new DatosOrdenTrabajo
                {
                    FechaPrometida = FechaPrometida is DateTime f ? DateOnly.FromDateTime(f) : null,
                    Instrucciones = string.IsNullOrWhiteSpace(Instrucciones) ? null : Instrucciones.Trim(),
                }
                : null,
        };
        VentaRegistrada? r = null;
        var ok = await IntentarAsync(async () => r = await _sesion.Api.CrearVentaAsync(venta));
        if (!ok || r is null) return;

        UltimaVenta = r;
        var resumen = $"Venta {r.Numero} registrada por {Dinero.Formatear(r.Total)}.";
        if (r.Saldo > 0) resumen += $" Saldo pendiente: {Dinero.Formatear(r.Saldo)}.";
        if (r.OrdenTrabajo is not null) resumen += $" Orden de laboratorio {r.OrdenTrabajo.Numero}.";
        Limpiar();
        Aviso = resumen;
        await BuscarAsync();
        Aviso = resumen;
    }

    [RelayCommand]
    private void Limpiar()
    {
        Carrito.Clear();
        Pagos.Clear();
        Cliente = null;
        ClientesEncontrados = [];
        BusquedaCliente = "";
        DescuentoTexto = "";
        Instrucciones = "";
        FechaPrometida = DateTime.Today.AddDays(3);
        Error = Aviso = null;
        MontoPagoTexto = "";
        Recalcular();
    }
}
