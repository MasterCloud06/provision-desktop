using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Productos con su stock: crear, editar, eliminar/desactivar y recepción de mercadería.</summary>
public sealed partial class InventarioViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public InventarioViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _filtroTipo = TiposFiltro[0];
        _formTipo = Tipos[0];
        _filtroEstado = Estados[0];
    }

    public bool PuedeGestionar => _sesion.Puede("catalogo.gestionar");
    public bool PuedeRecibir => _sesion.Puede("inventario.recibir");

    public IReadOnlyList<Opcion<TipoProducto>> Tipos { get; } =
        Enum.GetValues<TipoProducto>().Select(t => new Opcion<TipoProducto>(t, Enumeraciones.Etiqueta(t))).ToList();

    public IReadOnlyList<Opcion<TipoProducto?>> TiposFiltro { get; } =
        new[] { new Opcion<TipoProducto?>(null, "Todos los tipos") }
            .Concat(Enum.GetValues<TipoProducto>().Select(t => new Opcion<TipoProducto?>(t, Enumeraciones.Etiqueta(t))))
            .ToList();

    public IReadOnlyList<Opcion<bool?>> Estados { get; } =
        [new(true, "Activos"), new(false, "Desactivados"), new(null, "Todos")];

    /* ─────────── Listado ─────────── */

    [ObservableProperty] private string _busqueda = "";
    [ObservableProperty] private Opcion<TipoProducto?> _filtroTipo;
    [ObservableProperty] private Opcion<bool?> _filtroEstado;
    [ObservableProperty] private bool _soloBajoStock;
    [ObservableProperty] private IReadOnlyList<Producto> _productos = [];
    [ObservableProperty] private string _resumen = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion), nameof(PuedeRecibirSeleccion), nameof(SeleccionInactiva))]
    private Producto? _seleccionado;

    public bool HaySeleccion => Seleccionado is not null;
    public bool PuedeRecibirSeleccion => PuedeRecibir && Seleccionado is { ControlaStock: true, Activo: true };
    public bool SeleccionInactiva => Seleccionado is { Activo: false };

    public Task ActivarAsync() => BuscarAsync();

    partial void OnFiltroTipoChanged(Opcion<TipoProducto?> value) => _ = BuscarAsync();
    partial void OnFiltroEstadoChanged(Opcion<bool?> value) => _ = BuscarAsync();
    partial void OnSoloBajoStockChanged(bool value) => _ = BuscarAsync();

    partial void OnSeleccionadoChanged(Producto? value)
    {
        ConfirmandoEliminar = false;
        EntradaCosto = value is { CostoPromedio: > 0 } ? Dinero.ParaEditar(value.CostoPromedio) : "";
    }

    [RelayCommand]
    private Task BuscarAsync() => IntentarAsync(async () =>
    {
        var seleccionadoId = Seleccionado?.Id;
        var r = await _sesion.Api.ProductosAsync(
            buscar: Busqueda, tipo: FiltroTipo.Valor, activo: FiltroEstado.Valor,
            bajoStock: SoloBajoStock ? true : null, porPagina: 200);
        Productos = r.Datos;
        Seleccionado = Productos.FirstOrDefault(p => p.Id == seleccionadoId);
        Resumen = r.Total == r.Datos.Count ? $"{r.Total} productos" : $"Mostrando {r.Datos.Count} de {r.Total} productos";
        if (Marcas.Count == 0) Marcas = await _sesion.Api.MarcasAsync();
    });

    /* ─────────── Formulario: nuevo o editar ─────────── */

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsNuevo), nameof(TituloFormulario))]
    private Guid? _editandoId;

    public bool EsNuevo => EditandoId is null;
    public string TituloFormulario => EsNuevo ? "Nuevo producto" : "Editar producto";

    [ObservableProperty] private bool _mostrandoFormulario;
    [ObservableProperty] private IReadOnlyList<Marca> _marcas = [];
    [ObservableProperty] private string _formSku = "";
    [ObservableProperty] private string _formNombre = "";
    [ObservableProperty] private Opcion<TipoProducto> _formTipo;
    [ObservableProperty] private Marca? _formMarca;
    [ObservableProperty] private string _nombreMarcaNueva = "";
    [ObservableProperty] private string _formModelo = "";
    [ObservableProperty] private string _formCodigoBarras = "";
    [ObservableProperty] private string _formPrecio = "";
    [ObservableProperty] private decimal? _formStockMinimo = 0;
    [ObservableProperty] private decimal? _formStockInicial = 0;
    [ObservableProperty] private string _formCosto = "";

    [RelayCommand]
    private void Nuevo()
    {
        LimpiarFormulario();
        MostrandoFormulario = true;
    }

    [RelayCommand]
    private void Editar()
    {
        if (Seleccionado is not { } p) return;
        EditandoId = p.Id;
        FormSku = p.Sku;
        FormNombre = p.Nombre;
        FormTipo = Tipos.First(t => t.Valor == p.Tipo);
        FormMarca = Marcas.FirstOrDefault(m => m.Id == p.MarcaId);
        FormModelo = p.Modelo ?? "";
        FormCodigoBarras = p.CodigoBarras ?? "";
        FormPrecio = Dinero.ParaEditar(p.PrecioVenta);
        FormStockMinimo = p.StockMinimo;
        MostrandoFormulario = true;
        Error = Aviso = null;
    }

    [RelayCommand]
    private void CancelarFormulario()
    {
        LimpiarFormulario();
        MostrandoFormulario = false;
    }

    private void LimpiarFormulario()
    {
        EditandoId = null;
        FormSku = FormNombre = FormModelo = FormCodigoBarras = FormPrecio = FormCosto = NombreMarcaNueva = "";
        FormTipo = Tipos[0];
        FormMarca = null;
        FormStockMinimo = 0;
        FormStockInicial = 0;
    }

    [RelayCommand]
    private async Task CrearMarcaAsync()
    {
        var nombre = NombreMarcaNueva.Trim();
        if (nombre.Length == 0) return;
        Marca? m = null;
        if (await IntentarAsync(async () => m = await _sesion.Api.CrearMarcaAsync(nombre)))
        {
            Marcas = await _sesion.Api.MarcasAsync();
            FormMarca = Marcas.FirstOrDefault(x => x.Id == m!.Id);
            NombreMarcaNueva = "";
        }
    }

    [RelayCommand]
    private async Task GuardarProductoAsync()
    {
        if (FormSku.Trim().Length == 0 || FormNombre.Trim().Length < 2) { Falla("Completa el código (SKU) y el nombre"); return; }
        if (!Dinero.TryLeer(FormPrecio, out var precio) || precio < 0) { Falla("Precio de venta inválido"); return; }
        var nombre = FormNombre.Trim();
        string mensaje;

        if (EditandoId is Guid id)
        {
            var cambios = new CambiosProducto
            {
                Sku = FormSku.Trim(),
                Nombre = nombre,
                Tipo = FormTipo.Valor,
                MarcaId = FormMarca?.Id,
                Modelo = FormModelo.Trim(),
                CodigoBarras = string.IsNullOrWhiteSpace(FormCodigoBarras) ? null : FormCodigoBarras.Trim(),
                PrecioVenta = precio,
                StockMinimo = (int)(FormStockMinimo ?? 0),
            };
            if (!await IntentarAsync(() => _sesion.Api.ActualizarProductoAsync(id, cambios))) return;
            mensaje = $"\"{nombre}\" actualizado.";
        }
        else
        {
            var esServicio = FormTipo.Valor == TipoProducto.Servicio;
            var stockInicial = (int)(FormStockInicial ?? 0);
            StockInicial? inicial = null;
            if (!esServicio && stockInicial > 0)
            {
                if (!Dinero.TryLeer(FormCosto, out var costo) || costo < 0) { Falla("Indica el costo unitario de compra del stock inicial"); return; }
                inicial = new StockInicial(stockInicial, costo);
            }
            var nuevo = new NuevoProducto
            {
                Sku = FormSku.Trim(),
                Nombre = nombre,
                Tipo = FormTipo.Valor,
                MarcaId = FormMarca?.Id,
                Modelo = string.IsNullOrWhiteSpace(FormModelo) ? null : FormModelo.Trim(),
                CodigoBarras = string.IsNullOrWhiteSpace(FormCodigoBarras) ? null : FormCodigoBarras.Trim(),
                PrecioVenta = precio,
                StockMinimo = (int)(FormStockMinimo ?? 0),
                StockInicial = inicial,
            };
            if (!await IntentarAsync(() => _sesion.Api.CrearProductoAsync(nuevo))) return;
            mensaje = $"Producto \"{nombre}\" creado.";
        }

        LimpiarFormulario();
        MostrandoFormulario = false;
        await BuscarAsync();
        Aviso = mensaje;
    }

    /* ─────────── Eliminar / reactivar ─────────── */

    [ObservableProperty] private bool _confirmandoEliminar;

    [RelayCommand]
    private void PedirEliminar() => ConfirmandoEliminar = true;

    [RelayCommand]
    private void CancelarEliminar() => ConfirmandoEliminar = false;

    [RelayCommand]
    private async Task EliminarAsync()
    {
        if (Seleccionado is not { } p) return;
        ResultadoEliminar? r = null;
        if (!await IntentarAsync(async () => r = await _sesion.Api.EliminarProductoAsync(p.Id))) return;
        ConfirmandoEliminar = false;
        if (r!.Eliminado) Seleccionado = null;
        await BuscarAsync();
        Aviso = r.Mensaje;
    }

    [RelayCommand]
    private async Task ReactivarAsync()
    {
        if (Seleccionado is not { } p) return;
        if (!await IntentarAsync(() => _sesion.Api.ActualizarProductoAsync(p.Id, new CambiosProducto { Activo = true }))) return;
        await BuscarAsync();
        Aviso = $"\"{p.Nombre}\" está activo otra vez.";
    }

    /* ─────────── Entrada de mercadería ─────────── */

    [ObservableProperty] private decimal? _entradaCantidad = 1;
    [ObservableProperty] private string _entradaCosto = "";
    [ObservableProperty] private string _entradaDocumento = "";

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
        Aviso = mensaje;
    }
}
