using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Productos con su stock, alta de productos y recepción de mercadería.</summary>
public sealed partial class InventarioViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public InventarioViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _filtroTipo = TiposFiltro[0];
        _nuevoTipo = Tipos[0];
    }

    public bool EsAdmin => _sesion.EsAdmin;

    public IReadOnlyList<Opcion<TipoProducto>> Tipos { get; } =
        Enum.GetValues<TipoProducto>().Select(t => new Opcion<TipoProducto>(t, Enumeraciones.Etiqueta(t))).ToList();

    public IReadOnlyList<Opcion<TipoProducto?>> TiposFiltro { get; } =
        new[] { new Opcion<TipoProducto?>(null, "Todos los tipos") }
            .Concat(Enum.GetValues<TipoProducto>().Select(t => new Opcion<TipoProducto?>(t, Enumeraciones.Etiqueta(t))))
            .ToList();

    /* Listado */

    [ObservableProperty] private string _busqueda = "";
    [ObservableProperty] private Opcion<TipoProducto?> _filtroTipo;
    [ObservableProperty] private bool _soloBajoStock;
    [ObservableProperty] private IReadOnlyList<Producto> _productos = [];
    [ObservableProperty] private string _resumen = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    private Producto? _seleccionado;

    public bool HaySeleccion => Seleccionado is { ControlaStock: true };

    public Task ActivarAsync() => BuscarAsync();

    partial void OnFiltroTipoChanged(Opcion<TipoProducto?> value) => _ = BuscarAsync();
    partial void OnSoloBajoStockChanged(bool value) => _ = BuscarAsync();

    [RelayCommand]
    private Task BuscarAsync() => IntentarAsync(async () =>
    {
        var r = await _sesion.Api.ProductosAsync(
            buscar: Busqueda, tipo: FiltroTipo.Valor, bajoStock: SoloBajoStock ? true : null, porPagina: 200);
        Productos = r.Datos;
        Resumen = r.Total == r.Datos.Count ? $"{r.Total} productos" : $"Mostrando {r.Datos.Count} de {r.Total} productos";
        if (Marcas.Count == 0) Marcas = await _sesion.Api.MarcasAsync();
    });

    /* Nuevo producto (solo administrador) */

    [ObservableProperty] private bool _mostrandoNuevo;
    [ObservableProperty] private IReadOnlyList<Marca> _marcas = [];
    [ObservableProperty] private string _nuevoSku = "";
    [ObservableProperty] private string _nuevoNombre = "";
    [ObservableProperty] private Opcion<TipoProducto> _nuevoTipo;
    [ObservableProperty] private Marca? _nuevaMarca;
    [ObservableProperty] private string _nombreMarcaNueva = "";
    [ObservableProperty] private string _nuevoModelo = "";
    [ObservableProperty] private string _nuevoCodigoBarras = "";
    [ObservableProperty] private string _nuevoPrecio = "";
    [ObservableProperty] private decimal? _nuevoStockMinimo = 0;
    [ObservableProperty] private decimal? _nuevoStockInicial = 0;
    [ObservableProperty] private string _nuevoCosto = "";

    [RelayCommand]
    private void AlternarNuevo() => MostrandoNuevo = !MostrandoNuevo;

    [RelayCommand]
    private async Task CrearMarcaAsync()
    {
        var nombre = NombreMarcaNueva.Trim();
        if (nombre.Length == 0) return;
        Marca? m = null;
        if (await IntentarAsync(async () => m = await _sesion.Api.CrearMarcaAsync(nombre)))
        {
            Marcas = await _sesion.Api.MarcasAsync();
            NuevaMarca = Marcas.FirstOrDefault(x => x.Id == m!.Id);
            NombreMarcaNueva = "";
        }
    }

    [RelayCommand]
    private async Task GuardarProductoAsync()
    {
        if (NuevoSku.Trim().Length == 0 || NuevoNombre.Trim().Length < 2) { Falla("Completa el código (SKU) y el nombre"); return; }
        if (!Dinero.TryLeer(NuevoPrecio, out var precio) || precio < 0) { Falla("Precio de venta inválido"); return; }
        var esServicio = NuevoTipo.Valor == TipoProducto.Servicio;
        var stockInicial = (int)(NuevoStockInicial ?? 0);
        StockInicial? inicial = null;
        if (!esServicio && stockInicial > 0)
        {
            if (!Dinero.TryLeer(NuevoCosto, out var costo) || costo < 0) { Falla("Indica el costo unitario de compra del stock inicial"); return; }
            inicial = new StockInicial(stockInicial, costo);
        }
        var producto = new NuevoProducto
        {
            Sku = NuevoSku.Trim(),
            Nombre = NuevoNombre.Trim(),
            Tipo = NuevoTipo.Valor,
            MarcaId = NuevaMarca?.Id,
            Modelo = string.IsNullOrWhiteSpace(NuevoModelo) ? null : NuevoModelo.Trim(),
            CodigoBarras = string.IsNullOrWhiteSpace(NuevoCodigoBarras) ? null : NuevoCodigoBarras.Trim(),
            PrecioVenta = precio,
            StockMinimo = (int)(NuevoStockMinimo ?? 0),
            StockInicial = inicial,
        };
        if (!await IntentarAsync(() => _sesion.Api.CrearProductoAsync(producto))) return;
        var mensaje = $"Producto \"{producto.Nombre}\" creado.";
        NuevoSku = NuevoNombre = NuevoModelo = NuevoCodigoBarras = NuevoPrecio = NuevoCosto = "";
        NuevoStockInicial = 0;
        NuevoStockMinimo = 0;
        MostrandoNuevo = false;
        await BuscarAsync();
        Aviso = mensaje;
    }

    /* Entrada de mercadería para el producto seleccionado */

    [ObservableProperty] private decimal? _entradaCantidad = 1;
    [ObservableProperty] private string _entradaCosto = "";
    [ObservableProperty] private string _entradaDocumento = "";

    partial void OnSeleccionadoChanged(Producto? value) =>
        EntradaCosto = value is { CostoPromedio: > 0 } ? Dinero.ParaEditar(value.CostoPromedio) : "";

    [RelayCommand]
    private async Task RegistrarEntradaAsync()
    {
        if (Seleccionado is not { } p) return;
        var cantidad = (int)(EntradaCantidad ?? 0);
        if (cantidad <= 0) { Falla("La cantidad debe ser mayor a cero"); return; }
        if (!Dinero.TryLeer(EntradaCosto, out var costo) || costo < 0) { Falla("Costo unitario inválido"); return; }
        var entrada = new EntradaInventario
        {
            Documento = string.IsNullOrWhiteSpace(EntradaDocumento) ? null : EntradaDocumento.Trim(),
            Items = [new LineaEntrada(p.Id, cantidad, costo)],
        };
        if (!await IntentarAsync(() => _sesion.Api.RegistrarEntradaAsync(entrada))) return;
        var mensaje = $"Ingresaron {cantidad} unidades de \"{p.Nombre}\".";
        EntradaCantidad = 1;
        EntradaDocumento = "";
        await BuscarAsync();
        Seleccionado = Productos.FirstOrDefault(x => x.Id == p.Id);
        Aviso = mensaje;
    }
}
