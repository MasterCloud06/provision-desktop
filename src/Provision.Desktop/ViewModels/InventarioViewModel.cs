using Avalonia.Media.Imaging;
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
        _formGenero = Generos[0];
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

    public IReadOnlyList<Opcion<string?>> Generos { get; } =
        [new(null, "Sin especificar"), new("UNISEX", "Unisex"), new("MUJER", "Mujer"), new("HOMBRE", "Hombre"), new("NINO", "Niños")];

    /// <summary>Las fotos de un celular pesan 3 a 6 MB; el servidor acepta hasta 12.</summary>
    private const int MaxFotoBytes = 12 * 1024 * 1024;
    private static readonly HttpClient Descargas = new() { Timeout = TimeSpan.FromSeconds(30) };

    /* ─────────── Listado ─────────── */

    [ObservableProperty] private string _busqueda = "";
    [ObservableProperty] private Opcion<TipoProducto?> _filtroTipo;
    [ObservableProperty] private Opcion<bool?> _filtroEstado;
    [ObservableProperty] private bool _soloBajoStock;
    [ObservableProperty] private IReadOnlyList<Producto> _productos = [];
    [ObservableProperty] private string _resumen = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion), nameof(PuedeRecibirSeleccion), nameof(SeleccionInactiva), nameof(SeleccionOculta))]
    private Producto? _seleccionado;

    public bool HaySeleccion => Seleccionado is not null;
    public bool PuedeRecibirSeleccion => PuedeRecibir && Seleccionado is { ControlaStock: true, Activo: true };
    public bool SeleccionInactiva => Seleccionado is { Activo: false };
    public bool SeleccionOculta => Seleccionado is { VisibleTienda: false };

    /// <summary>Foto del producto seleccionado (miniatura descargada de Cloudinary).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneFotoSeleccionado))]
    private Bitmap? _fotoSeleccionado;

    public bool TieneFotoSeleccionado => FotoSeleccionado is not null;

    public Task ActivarAsync() => BuscarAsync();

    partial void OnFiltroTipoChanged(Opcion<TipoProducto?> value) => _ = BuscarAsync();
    partial void OnFiltroEstadoChanged(Opcion<bool?> value) => _ = BuscarAsync();
    partial void OnSoloBajoStockChanged(bool value) => _ = BuscarAsync();

    partial void OnSeleccionadoChanged(Producto? value)
    {
        ConfirmandoEliminar = false;
        EntradaCosto = value is { CostoPromedio: > 0 } ? Dinero.ParaEditar(value.CostoPromedio) : "";
        _ = CargarFotoSeleccionadoAsync(value);
    }

    private string? _fotoCargada;

    private async Task CargarFotoSeleccionadoAsync(Producto? p)
    {
        var url = ProvisionApi.Miniatura(p?.ImagenUrl);
        if (url == _fotoCargada) return;
        _fotoCargada = url;
        FotoSeleccionado = null;
        var bitmap = await DescargarAsync(url);
        if (url == _fotoCargada) FotoSeleccionado = bitmap;
    }

    /// <summary>Baja una imagen para mostrarla; si no se puede, simplemente no hay vista previa.</summary>
    private static async Task<Bitmap?> DescargarAsync(string? url)
    {
        if (url is null) return null;
        try
        {
            var bytes = await Descargas.GetByteArrayAsync(url);
            return new Bitmap(new MemoryStream(bytes));
        }
        catch
        {
            return null;
        }
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
    [ObservableProperty] private string _formDescripcion = "";
    [ObservableProperty] private bool _formVisibleTienda = true;

    /* Características */
    [ObservableProperty] private Opcion<string?> _formGenero = null!;
    [ObservableProperty] private string _formMaterial = "";
    [ObservableProperty] private string _formForma = "";
    [ObservableProperty] private string _formColor = "";
    [ObservableProperty] private decimal? _formCalibre;
    [ObservableProperty] private decimal? _formPuente;
    [ObservableProperty] private decimal? _formVarilla;
    [ObservableProperty] private decimal? _formIndice;
    [ObservableProperty] private string _formTratamiento = "";

    /// <summary>Monturas y gafas de sol llevan medidas y "para quién".</summary>
    public bool FormEsMontura => FormTipo.Valor is TipoProducto.Montura or TipoProducto.GafasSol;
    public bool FormEsLente => FormTipo.Valor is TipoProducto.LenteOftalmico;
    public bool FormLlevaMaterial => FormTipo.Valor is TipoProducto.Montura or TipoProducto.GafasSol or TipoProducto.Accesorio or TipoProducto.LenteOftalmico;

    partial void OnFormTipoChanged(Opcion<TipoProducto> value)
    {
        OnPropertyChanged(nameof(FormEsMontura));
        OnPropertyChanged(nameof(FormEsLente));
        OnPropertyChanged(nameof(FormLlevaMaterial));
    }

    /* Foto del formulario: la que ya tiene el producto o una nueva elegida del disco */
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTieneFoto))]
    private Bitmap? _formFoto;

    [ObservableProperty] private string _formFotoTexto = "Sin foto";
    private byte[]? _fotoNueva;
    private string? _fotoNuevaTipo;
    private bool _quitarFoto;
    private bool _teniaFoto;

    public bool FormTieneFoto => FormFoto is not null || _fotoNueva is not null || (_teniaFoto && !_quitarFoto);

    /// <summary>La vista llama esto después de que el usuario elige un archivo.</summary>
    public void UsarFoto(byte[] contenido, string nombreArchivo)
    {
        var tipo = ProvisionApi.TipoDeFoto(nombreArchivo);
        if (tipo is null) { Falla("La foto debe ser JPG, PNG, WEBP o HEIC"); return; }
        if (contenido.Length > MaxFotoBytes) { Falla("La foto pesa más de 12 MB: elige una más liviana"); return; }
        _fotoNueva = contenido;
        _fotoNuevaTipo = tipo;
        _quitarFoto = false;
        try { FormFoto = new Bitmap(new MemoryStream(contenido)); }
        catch { FormFoto = null; } // HEIC: Windows no siempre la puede mostrar, pero se sube igual
        FormFotoTexto = $"Nueva: {nombreArchivo} ({contenido.Length / 1024} KB). Se sube al guardar.";
        OnPropertyChanged(nameof(FormTieneFoto));
        Error = null;
    }

    [RelayCommand]
    private void QuitarFoto()
    {
        _fotoNueva = null;
        _fotoNuevaTipo = null;
        _quitarFoto = _teniaFoto;
        FormFoto = null;
        FormFotoTexto = _teniaFoto ? "La foto se quitará al guardar." : "Sin foto";
        OnPropertyChanged(nameof(FormTieneFoto));
    }

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
        FormDescripcion = p.Descripcion ?? "";
        FormVisibleTienda = p.VisibleTienda;
        var a = p.Atributos;
        FormGenero = Generos.FirstOrDefault(g => g.Valor == a.Genero) ?? Generos[0];
        FormMaterial = a.Material ?? "";
        FormForma = a.Forma ?? "";
        FormColor = a.Color ?? "";
        FormCalibre = a.Calibre;
        FormPuente = a.Puente;
        FormVarilla = a.Varilla;
        FormIndice = a.Indice;
        FormTratamiento = a.Tratamiento ?? "";
        _fotoNueva = null;
        _fotoNuevaTipo = null;
        _quitarFoto = false;
        _teniaFoto = p.ImagenUrl is not null;
        FormFoto = FotoSeleccionado;
        FormFotoTexto = _teniaFoto ? "Foto actual" : "Sin foto";
        OnPropertyChanged(nameof(FormTieneFoto));
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
        FormDescripcion = FormMaterial = FormForma = FormColor = FormTratamiento = "";
        FormVisibleTienda = true;
        FormGenero = Generos[0];
        FormCalibre = FormPuente = FormVarilla = FormIndice = null;
        _fotoNueva = null;
        _fotoNuevaTipo = null;
        _quitarFoto = false;
        _teniaFoto = false;
        FormFoto = null;
        FormFotoTexto = "Sin foto";
        OnPropertyChanged(nameof(FormTieneFoto));
    }

    private static string? Texto(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Solo se guardan las características que corresponden al tipo de producto.</summary>
    private AtributosProducto ArmarAtributos() => new()
    {
        Genero = FormEsMontura ? FormGenero.Valor : null,
        Material = FormLlevaMaterial ? Texto(FormMaterial) : null,
        Forma = FormEsMontura ? Texto(FormForma) : null,
        Color = Texto(FormColor),
        Calibre = FormEsMontura && FormCalibre > 0 ? FormCalibre : null,
        Puente = FormEsMontura && FormPuente > 0 ? FormPuente : null,
        Varilla = FormEsMontura && FormVarilla > 0 ? FormVarilla : null,
        Indice = FormEsLente && FormIndice > 0 ? FormIndice : null,
        Tratamiento = FormEsLente ? Texto(FormTratamiento) : null,
    };

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
        if (FormEsLente && FormIndice is { } ind && ind > 0 && (ind < 1.4m || ind > 2m)) { Falla("El índice del lente va entre 1.40 y 2.00 (por ejemplo 1.56)"); return; }
        var nombre = FormNombre.Trim();
        var atributos = ArmarAtributos();
        string mensaje;
        Guid productoId;

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
                Descripcion = FormDescripcion.Trim(),
                Atributos = atributos,
                VisibleTienda = FormVisibleTienda,
            };
            if (!await IntentarAsync(() => _sesion.Api.ActualizarProductoAsync(id, cambios))) return;
            mensaje = $"\"{nombre}\" actualizado.";
            productoId = id;
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
                Descripcion = Texto(FormDescripcion),
                Atributos = atributos,
                VisibleTienda = FormVisibleTienda,
            };
            Producto? creado = null;
            if (!await IntentarAsync(async () => creado = await _sesion.Api.CrearProductoAsync(nuevo))) return;
            mensaje = $"Producto \"{nombre}\" creado.";
            productoId = creado!.Id;
            // Desde aquí el producto existe: si la foto falla, se reintenta editándolo.
            EditandoId = productoId;
        }

        // La foto va aparte, después de guardar los datos.
        if (_fotoNueva is not null)
        {
            var bytes = _fotoNueva;
            var tipo = _fotoNuevaTipo!;
            if (!await IntentarAsync(() => _sesion.Api.SubirFotoAsync(productoId, bytes, tipo), mensajeEspera: "Subiendo la foto…"))
            {
                Error = $"Los datos se guardaron, pero la foto no: {Error}";
                await BuscarAsync();
                return;
            }
            mensaje += " Foto subida.";
        }
        else if (_quitarFoto)
        {
            if (!await IntentarAsync(() => _sesion.Api.QuitarFotoAsync(productoId))) return;
            mensaje += " Foto quitada.";
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
