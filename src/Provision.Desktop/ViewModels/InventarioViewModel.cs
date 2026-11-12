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
        _filtroDisponibilidad = Disponibilidades[0];
        _filtroTienda = FiltrosTienda[0];
        _formGenero = Generos[0];
    }

    public bool PuedeGestionar => _sesion.Puede("catalogo.gestionar");
    public bool PuedeRecibir => _sesion.Puede("inventario.recibir");

    public IReadOnlyList<Opcion<TipoProducto>> Tipos { get; } =
        Enum.GetValues<TipoProducto>().Select(t => new Opcion<TipoProducto>(t, Enumeraciones.Etiqueta(t))).ToList();

    public IReadOnlyList<Opcion<TipoProducto?>> TiposFiltro { get; } =
        new[] { new Opcion<TipoProducto?>(null, "Todas") }
            .Concat(Enum.GetValues<TipoProducto>().Select(t => new Opcion<TipoProducto?>(t, Enumeraciones.Etiqueta(t))))
            .ToList();

    public IReadOnlyList<Opcion<bool?>> Estados { get; } =
        [new(true, "Activos"), new(false, "Desactivados"), new(null, "Todos")];

    public IReadOnlyList<Opcion<string?>> Generos { get; } =
        [new(null, "Sin especificar"), new("UNISEX", "Unisex"), new("MUJER", "Mujer"), new("HOMBRE", "Hombre"), new("NINO", "Niños")];

    /// <summary>Las fotos de un celular pesan 3 a 6 MB; el servidor acepta hasta 12.</summary>
    private const int MaxFotoBytes = 12 * 1024 * 1024;

    /* ─────────── Listado ─────────── */

    /// <summary>
    /// El catálogo trae todo el inventario de la sucursal (de a 100) para que el carrusel y los filtros de marca,
    /// disponibilidad y tienda trabajen sobre el conjunto completo.
    /// </summary>
    private const int PorPagina = 100;

    public IReadOnlyList<Opcion<string>> Disponibilidades { get; } =
        [new("", "Todas"), new("CON_STOCK", "Disponibles"), new("BAJO", "Stock bajo"), new("AGOTADO", "Agotados")];

    public IReadOnlyList<Opcion<bool?>> FiltrosTienda { get; } =
        [new(null, "Todos"), new(true, "Visibles"), new(false, "Ocultos")];

    [ObservableProperty] private string _busqueda = "";
    [ObservableProperty] private Opcion<TipoProducto?> _filtroTipo;
    [ObservableProperty] private Opcion<bool?> _filtroEstado;
    [ObservableProperty] private bool _soloBajoStock;
    [ObservableProperty] private Opcion<Guid?> _filtroMarca = SinFiltroMarca;
    [ObservableProperty] private Opcion<string> _filtroDisponibilidad = null!;
    [ObservableProperty] private Opcion<bool?> _filtroTienda = null!;
    [ObservableProperty] private IReadOnlyList<Opcion<Guid?>> _opcionesMarca = [SinFiltroMarca];
    [ObservableProperty] private IReadOnlyList<Producto> _productos = [];
    [ObservableProperty] private IReadOnlyList<FilaProducto> _filas = [];
    [ObservableProperty] private FilaProducto? _filaSeleccionada;
    [ObservableProperty] private string _resumen = "";
    [ObservableProperty] private bool _cargado;

    private static readonly Opcion<Guid?> SinFiltroMarca = new(null, "Todas las marcas");
    /// <summary>Opción para los productos sin marca (no existe como marca en el servidor).</summary>
    private static readonly Guid SinMarca = Guid.Empty;

    /// <summary>Catálogo (carrusel) o lista. La lista es la tabla densa para revisar muchos productos.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VistaLista))]
    private bool _vistaCatalogo = true;
    public bool VistaLista => !VistaCatalogo;

    [RelayCommand] private void VerCatalogo() => VistaCatalogo = true;
    [RelayCommand] private void VerLista() => VistaCatalogo = false;

    /// <summary>Cambia cada vez que los filtros cambian el conjunto: la vista vuelve el carrusel al principio.</summary>
    [ObservableProperty] private int _versionFiltro;

    /* Indicadores: se cuentan sobre los productos cargados y filtrados, nunca se estiman. */
    [ObservableProperty] private int _totalProductos;
    [ObservableProperty] private int _disponibles;
    [ObservableProperty] private int _conStockBajo;
    [ObservableProperty] private int _agotados;
    [ObservableProperty] private string _notaIndicadores = "";

    public bool SinResultados => Cargado && Filas.Count == 0;
    public bool HayFiltros => Busqueda.Trim().Length > 0 || FiltroTipo.Valor is not null || FiltroEstado != Estados[0] || SoloBajoStock
                              || FiltroMarca.Valor is not null || FiltroDisponibilidad.Valor != "" || FiltroTienda.Valor is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion), nameof(PuedeRecibirSeleccion), nameof(SeleccionInactiva), nameof(SeleccionOculta),
        nameof(PuedeEliminarSeleccion), nameof(PuedeReactivarSeleccion), nameof(PuedeEditarSeleccion), nameof(Ficha))]
    private Producto? _seleccionado;

    /// <summary>Lo que muestra la ficha: el producto seleccionado con sus textos ya armados.</summary>
    public FilaProducto? Ficha => Seleccionado is { } p ? new FilaProducto(p) : null;

    public bool HaySeleccion => Seleccionado is not null;
    public bool PuedeRecibirSeleccion => PuedeRecibir && Seleccionado is { ControlaStock: true, Activo: true };
    public bool SeleccionInactiva => Seleccionado is { Activo: false };
    public bool SeleccionOculta => Seleccionado is { VisibleTienda: false };
    public bool PuedeEditarSeleccion => PuedeGestionar && HaySeleccion;
    public bool PuedeEliminarSeleccion => PuedeGestionar && Seleccionado is { Activo: true };
    public bool PuedeReactivarSeleccion => PuedeGestionar && SeleccionInactiva;

    partial void OnFilaSeleccionadaChanged(FilaProducto? value)
    {
        if (value is not null || !FichaAbierta) Seleccionado = value?.Producto;
    }
    partial void OnFilasChanged(IReadOnlyList<FilaProducto> value) => OnPropertyChanged(nameof(SinResultados));
    partial void OnCargadoChanged(bool value) => OnPropertyChanged(nameof(SinResultados));

    /// <summary>Foto del producto seleccionado (miniatura descargada de ENAI).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneFotoSeleccionado))]
    private Bitmap? _fotoSeleccionado;

    public bool TieneFotoSeleccionado => FotoSeleccionado is not null;

    public Task ActivarAsync() => BuscarAsync();

    private bool _silenciarFiltros;
    private CancellationTokenSource? _esperaBusqueda;

    /* Tipo, estado, stock bajo y búsqueda van al servidor; marca, disponibilidad y tienda filtran lo ya cargado. */
    partial void OnFiltroTipoChanged(Opcion<TipoProducto?> value) => FiltroCambio();
    partial void OnFiltroEstadoChanged(Opcion<bool?> value) => FiltroCambio();
    partial void OnSoloBajoStockChanged(bool value) => FiltroCambio();
    partial void OnFiltroMarcaChanged(Opcion<Guid?> value) => FiltroLocalCambio();
    partial void OnFiltroDisponibilidadChanged(Opcion<string> value) => FiltroLocalCambio();
    partial void OnFiltroTiendaChanged(Opcion<bool?> value) => FiltroLocalCambio();

    /// <summary>Buscar mientras se escribe, con una pausa corta para no consultar en cada tecla.</summary>
    partial void OnBusquedaChanged(string value)
    {
        OnPropertyChanged(nameof(HayFiltros));
        if (_silenciarFiltros) return;
        _esperaBusqueda?.Cancel();
        var espera = _esperaBusqueda = new CancellationTokenSource();
        _ = Task.Delay(350, espera.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) { VersionFiltro++; _ = BuscarAsync(); }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void FiltroCambio()
    {
        OnPropertyChanged(nameof(HayFiltros));
        if (_silenciarFiltros) return;
        VersionFiltro++;
        _ = BuscarAsync();
    }

    private void FiltroLocalCambio()
    {
        OnPropertyChanged(nameof(HayFiltros));
        if (_silenciarFiltros) return;
        VersionFiltro++;
        AplicarFiltrosLocales();
    }

    [RelayCommand]
    private Task LimpiarFiltrosAsync()
    {
        _silenciarFiltros = true;
        _esperaBusqueda?.Cancel();
        Busqueda = "";
        FiltroTipo = TiposFiltro[0];
        FiltroEstado = Estados[0];
        SoloBajoStock = false;
        FiltroMarca = OpcionesMarca[0];
        FiltroDisponibilidad = Disponibilidades[0];
        FiltroTienda = FiltrosTienda[0];
        _silenciarFiltros = false;
        OnPropertyChanged(nameof(HayFiltros));
        VersionFiltro++;
        return BuscarAsync();
    }

    /// <summary>Los indicadores del encabezado también filtran: un clic en "Agotados" muestra los agotados.</summary>
    [RelayCommand]
    private void FiltrarPorIndicador(string? clave) =>
        FiltroDisponibilidad = Disponibilidades.FirstOrDefault(d => d.Valor == (clave ?? "")) ?? Disponibilidades[0];

    partial void OnSeleccionadoChanged(Producto? value)
    {
        ConfirmandoEliminar = false;
        if (value is null) FichaAbierta = false;
        EntradaCosto = value is { CostoPromedio: > 0 } ? Dinero.ParaEditar(value.CostoPromedio) : "";
        _ = CargarFotoSeleccionadoAsync(value);
    }

    private string? _fotoCargada;

    private async Task CargarFotoSeleccionadoAsync(Producto? p)
    {
        var url = ProvisionApi.Miniatura(p?.ImagenUrl, 900);
        if (url == _fotoCargada) return;
        _fotoCargada = url;
        FotoSeleccionado?.Dispose();
        FotoSeleccionado = null;
        var bitmap = await DescargarAsync(url);
        if (url == _fotoCargada) FotoSeleccionado = bitmap;
        else bitmap?.Dispose();
    }

    /// <summary>Baja una imagen para mostrarla; si no se puede, simplemente no hay vista previa.</summary>
    private async Task<Bitmap?> DescargarAsync(string? url)
    {
        if (url is null) return null;
        try
        {
            var bytes = await _sesion.Api.Http.DescargarFotoAsync(url);
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private int _versionBusqueda;

    [RelayCommand]
    private Task BuscarAsync()
    {
        var version = ++_versionBusqueda;
        var buscar = Busqueda.Trim();
        var tipo = FiltroTipo.Valor;
        var activo = FiltroEstado.Valor;
        bool? bajoStock = SoloBajoStock ? true : null;
        return IntentarAsync(async () =>
    {
        var cargados = new List<Producto>();
        var total = 0;
        for (var pagina = 1; ; pagina++)
        {
            var r = await _sesion.Api.ProductosAsync(
                buscar: buscar, tipo: tipo, activo: activo,
                bajoStock: bajoStock, pagina: pagina, porPagina: PorPagina);
            if (version != _versionBusqueda) return;
            total = r.Total;
            cargados.AddRange(r.Datos);
            if (r.Datos.Count == 0 || cargados.Count >= total) break;
        }
        Productos = cargados;
        _totalServidor = total;
        if (Marcas.Count == 0)
        {
            Marcas = await _sesion.Api.MarcasAsync();
            OpcionesMarca = [SinFiltroMarca, .. Marcas.Select(m => new Opcion<Guid?>(m.Id, m.Nombre)), new(SinMarca, "Sin marca")];
        }
        if (version != _versionBusqueda) return;
        AplicarFiltrosLocales();
        Cargado = true;
    });
    }

    private int _totalServidor;

    private void AplicarFiltrosLocales()
    {
        var seleccionadoId = Seleccionado?.Id;
        IEnumerable<FilaProducto> filas = Productos.Select(p => new FilaProducto(p));
        if (FiltroMarca.Valor is { } marca)
            filas = filas.Where(f => marca == SinMarca ? f.Producto.MarcaId is null : f.Producto.MarcaId == marca);
        filas = FiltroDisponibilidad.Valor switch
        {
            "CON_STOCK" => filas.Where(f => f.EsDisponible),
            "BAJO" => filas.Where(f => f.EsBajo),
            "AGOTADO" => filas.Where(f => f.EsAgotado),
            _ => filas,
        };
        if (FiltroTienda.Valor is { } visible) filas = filas.Where(f => f.Producto.VisibleTienda == visible);
        Filas = filas.ToList();
        FilaSeleccionada = Filas.FirstOrDefault(f => f.Producto.Id == seleccionadoId);
        // Con la ficha abierta se conserva el producto aunque un filtro lo saque del catálogo (p. ej. al ocultarlo de la tienda).
        if (FilaSeleccionada is null) Seleccionado = FichaAbierta ? Productos.FirstOrDefault(p => p.Id == seleccionadoId) : null;

        // Los indicadores se cuentan sobre lo que trajo el servidor (antes de marca, disponibilidad y tienda),
        // para que "Agotados" no se vuelva 0 justo al filtrar por agotados.
        var todos = Productos.Select(p => new FilaProducto(p)).ToList();
        TotalProductos = _totalServidor;
        Disponibles = todos.Count(f => f.EsDisponible);
        ConStockBajo = todos.Count(f => f.EsBajo);
        Agotados = todos.Count(f => f.EsAgotado);
        Resumen = Filas.Count == Productos.Count
            ? (Filas.Count == 1 ? "1 producto" : $"{Filas.Count} productos")
            : $"{Filas.Count} de {Productos.Count} productos";
        NotaIndicadores = _totalServidor > Productos.Count
            ? $"Se cargaron los primeros {Productos.Count} de {_totalServidor} productos; usa la búsqueda o los filtros para acotar."
            : FiltroEstado.Valor switch { true => "Productos activos de esta sucursal.", false => "Productos desactivados.", _ => "Productos activos y desactivados." };
    }

    /* ─────────── Ficha del producto ─────────── */

    [ObservableProperty] private bool _fichaAbierta;
    [ObservableProperty] private bool _visorAbierto;

    /// <summary>Abre la ficha de un producto del catálogo (clic en su tarjeta).</summary>
    public void AbrirFicha(FilaProducto fila)
    {
        FilaSeleccionada = fila;
        Error = Aviso = null;
        FichaAbierta = true;
    }

    [RelayCommand]
    private void CerrarFicha()
    {
        FichaAbierta = false;
        VisorAbierto = false;
    }

    /// <summary>Foto a pantalla completa: se muestra la de la ficha y se reemplaza por una de 1800 px al llegar.</summary>
    [ObservableProperty] private Bitmap? _fotoAmpliada;

    [RelayCommand]
    private async Task AbrirVisorAsync()
    {
        if (!TieneFotoSeleccionado || Seleccionado is not { } p) return;
        FotoAmpliada = FotoSeleccionado;
        VisorAbierto = true;
        if (await DescargarAsync(ProvisionApi.Miniatura(p.ImagenUrl, 1800)) is { } grande && VisorAbierto && Seleccionado?.Id == p.Id)
            FotoAmpliada = grande;
    }

    [RelayCommand]
    private void CerrarVisor() => VisorAbierto = false;

    /// <summary>Publicar u ocultar en la tienda web sin abrir la edición completa.</summary>
    [RelayCommand]
    private async Task CambiarVisibilidadTiendaAsync()
    {
        if (!PuedeGestionar || Seleccionado is not { } p) return;
        var visible = !p.VisibleTienda;
        if (!await IntentarAsync(() => _sesion.Api.ActualizarProductoAsync(p.Id, new CambiosProducto { VisibleTienda = visible }))) return;
        await BuscarAsync();
        Aviso = visible ? $"\"{p.Nombre}\" ahora se muestra en la tienda web." : $"\"{p.Nombre}\" ya no se muestra en la tienda web.";
    }

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

    /* Solo lectura al editar: el stock y el costo promedio cambian con entradas y ventas, no a mano. */
    [ObservableProperty] private string _formStockActual = "";
    [ObservableProperty] private string _formCostoPromedio = "";
    [ObservableProperty] private bool _formControlaStock;
    public bool FormEsServicio => FormTipo.Valor == TipoProducto.Servicio;

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
        OnPropertyChanged(nameof(FormEsServicio));
    }

    /* Foto del formulario: la que ya tiene el producto o una nueva elegida del disco */
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTieneFoto))]
    private Bitmap? _formFoto;

    [ObservableProperty] private string _formFotoTexto = "Sin foto";
    private byte[]? _fotoNueva;
    private string? _fotoNuevaTipo;
    private string? _fotoNuevaNombre;
    private bool _quitarFoto;
    private bool _teniaFoto;

    public bool FormTieneFoto => FormFoto is not null || _fotoNueva is not null || (_teniaFoto && !_quitarFoto);

    /// <summary>La vista llama esto después de que el usuario elige un archivo.</summary>
    public void UsarFoto(byte[] contenido, string nombreArchivo)
    {
        var tipo = ProvisionApi.TipoDeFoto(nombreArchivo);
        if (tipo is null) { Falla("La foto debe ser JPG, PNG, WEBP"); return; }
        if (contenido.Length > MaxFotoBytes) { Falla("La foto pesa más de 12 MB: elige una más liviana"); return; }
        _fotoNueva = contenido;
        _fotoNuevaTipo = tipo;
        _fotoNuevaNombre = nombreArchivo;
        _quitarFoto = false;
        try { FormFoto = new Bitmap(new MemoryStream(contenido)); }
        catch { FormFoto = null; }
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
        AbrirFormulario();
    }

    /* Cierre accidental: se recuerda cómo estaba el formulario al abrirlo y se pregunta antes de descartar. */
    private string _huellaInicial = "";
    [ObservableProperty] private bool _confirmandoDescartar;

    private string Huella() => string.Join("\u001f",
        FormSku, FormNombre, FormTipo.Valor, FormMarca?.Id, FormModelo, FormCodigoBarras, FormPrecio, FormStockMinimo,
        FormStockInicial, FormCosto, FormDescripcion, FormVisibleTienda, FormGenero.Valor, FormMaterial, FormForma, FormColor,
        FormCalibre, FormPuente, FormVarilla, FormIndice, FormTratamiento, _fotoNueva?.Length, _quitarFoto);

    private void AbrirFormulario()
    {
        _huellaInicial = Huella();
        ConfirmandoDescartar = false;
        Error = Aviso = null;
        MostrandoFormulario = true;
    }

    public bool FormularioModificado => MostrandoFormulario && Huella() != _huellaInicial;

    [RelayCommand]
    private void DescartarCambios()
    {
        ConfirmandoDescartar = false;
        LimpiarFormulario();
        MostrandoFormulario = false;
        Error = null;
    }

    [RelayCommand]
    private void SeguirEditando() => ConfirmandoDescartar = false;

    [RelayCommand]
    private void Editar()
    {
        if (Seleccionado is not { } p) return;
        EditandoId = p.Id;
        FormStockActual = p.ControlaStock ? $"{p.Stock} unidades" : "No lleva control de stock";
        FormCostoPromedio = p.CostoPromedio > 0 ? Dinero.Formatear(p.CostoPromedio) : "Sin compras registradas";
        FormControlaStock = p.ControlaStock;
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
        AbrirFormulario();
        if (_teniaFoto && FormFoto is null) _ = CargarFotoFormularioAsync(p);
    }

    /// <summary>Si se abre la edición antes de que termine de bajar la miniatura, se completa al llegar.</summary>
    private async Task CargarFotoFormularioAsync(Producto p)
    {
        var bitmap = await DescargarAsync(ProvisionApi.Miniatura(p.ImagenUrl));
        if (MostrandoFormulario && EditandoId == p.Id && _fotoNueva is null && !_quitarFoto) FormFoto = bitmap;
    }

    /// <summary>Cancelar, la X o Esc: si hay cambios sin guardar, se pide confirmación.</summary>
    [RelayCommand]
    private void CancelarFormulario()
    {
        if (FormularioModificado) { ConfirmandoDescartar = true; return; }
        DescartarCambios();
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
            if (!await IntentarAsync(() => _sesion.Api.SubirFotoAsync(productoId, bytes, tipo, nombre: _fotoNuevaNombre), mensajeEspera: "Subiendo la foto…"))
            {
                // El mensaje va después de recargar: BuscarAsync limpia Error al empezar y lo borraba.
                var fallo = $"Los datos se guardaron, pero la foto no se pudo subir: {Error}";
                await BuscarAsync();
                Error = fallo;
                return;
            }
            mensaje += " Foto subida.";
        }
        else if (_quitarFoto)
        {
            if (!await IntentarAsync(() => _sesion.Api.QuitarFotoAsync(productoId)))
            {
                Error = $"Los datos se guardaron, pero la foto no se pudo quitar: {Error}";
                return;
            }
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

    [ObservableProperty] private bool _entradaAbierta;

    [RelayCommand]
    private void AbrirEntrada()
    {
        if (!PuedeRecibirSeleccion) return;
        EntradaCantidad = 1;
        EntradaDocumento = "";
        Error = Aviso = null;
        EntradaAbierta = true;
    }

    [RelayCommand]
    private void CerrarEntrada()
    {
        EntradaAbierta = false;
        Error = null;
    }

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
        EntradaAbierta = false;
        await BuscarAsync();
        Aviso = mensaje;
    }
    public bool PuedeVerMovimientos => _sesion.Puede("inventario.ver");
    public bool PuedeAjustar => _sesion.Puede("inventario.ajustar");
    public bool PuedeBaja => _sesion.Puede("inventario.bajas");
    [ObservableProperty] private bool _movimientosAbiertos;
    [ObservableProperty] private IReadOnlyList<MovimientoInventario> _movimientos = [];
    [ObservableProperty] private string _cantidadMovimiento = "";
    [ObservableProperty] private string _motivoMovimiento = "";
    [ObservableProperty] private string? _operacionPendiente;
    private Guid? _productoMovimiento;

    [RelayCommand]
    private async Task AbrirMovimientosAsync()
    {
        if (!PuedeVerMovimientos || Seleccionado is not { } p) return;
        _productoMovimiento = p.Id;
        CantidadMovimiento = "";
        MotivoMovimiento = "";
        OperacionPendiente = null;
        Movimientos = [];
        MovimientosAbiertos = true;
        await IntentarAsync(async () => Movimientos = await _sesion.Api.KardexAsync(p.Id));
    }
    [RelayCommand] private void CerrarMovimientos() => MovimientosAbiertos = false;
    [RelayCommand] private void PrepararMovimiento(string tipo)
    {
        if (Ocupado || (tipo == "conteo" ? !PuedeAjustar : !PuedeBaja)) return;
        if (!int.TryParse(CantidadMovimiento, out var cantidad) || cantidad < (tipo == "conteo" ? 0 : 1))
        { Falla("Escribe una cantidad entera válida."); return; }
        if (MotivoMovimiento.Trim().Length is < 3 or > 200) { Falla("Escribe un motivo de 3 a 200 caracteres."); return; }
        OperacionPendiente = tipo;
    }
    [RelayCommand] private void CancelarMovimiento() => OperacionPendiente = null;
    [RelayCommand]
    private async Task ConfirmarMovimientoAsync()
    {
        var tipo = OperacionPendiente;
        if (Ocupado || tipo is null || _productoMovimiento is not { } id) return;
        if (tipo == "conteo" ? !PuedeAjustar : !PuedeBaja) return;
        if (!int.TryParse(CantidadMovimiento, out var cantidad) || cantidad < (tipo == "conteo" ? 0 : 1)) return;
        var motivo = MotivoMovimiento.Trim();
        if (motivo.Length is < 3 or > 200) return;
        var ok = await IntentarAsync(() => tipo == "conteo"
            ? _sesion.Api.RegistrarConteoAsync(id, cantidad, motivo)
            : _sesion.Api.RegistrarBajaAsync(id, cantidad, motivo));
        if (!ok) return;
        OperacionPendiente = null;
        await IntentarAsync(async () => Movimientos = await _sesion.Api.KardexAsync(id));
        await BuscarAsync();
        Aviso = "Movimiento registrado. Existencias actualizadas por ENAI.";
    }
}
