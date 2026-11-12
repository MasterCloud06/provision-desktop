using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Provision.Desktop.ViewModels;

namespace Provision.Desktop.Views;

public partial class InventarioView : UserControl
{
    /* Medidas del carrusel: las calcula la vista según el espacio disponible y las leen las tarjetas. */
    public static readonly StyledProperty<double> AnchoTarjetaProperty = AvaloniaProperty.Register<InventarioView, double>(nameof(AnchoTarjeta), 280);
    public static readonly StyledProperty<double> AltoFotoProperty = AvaloniaProperty.Register<InventarioView, double>(nameof(AltoFoto), 220);
    public static readonly StyledProperty<bool> PuedeAtrasProperty = AvaloniaProperty.Register<InventarioView, bool>(nameof(PuedeAtras));
    public static readonly StyledProperty<bool> PuedeAdelanteProperty = AvaloniaProperty.Register<InventarioView, bool>(nameof(PuedeAdelante));
    public static readonly StyledProperty<string> TextoPosicionProperty = AvaloniaProperty.Register<InventarioView, string>(nameof(TextoPosicion), "");

    public double AnchoTarjeta { get => GetValue(AnchoTarjetaProperty); set => SetValue(AnchoTarjetaProperty, value); }
    public double AltoFoto { get => GetValue(AltoFotoProperty); set => SetValue(AltoFotoProperty, value); }
    public bool PuedeAtras { get => GetValue(PuedeAtrasProperty); set => SetValue(PuedeAtrasProperty, value); }
    public bool PuedeAdelante { get => GetValue(PuedeAdelanteProperty); set => SetValue(PuedeAdelanteProperty, value); }
    public string TextoPosicion { get => GetValue(TextoPosicionProperty); set => SetValue(TextoPosicionProperty, value); }

    /// <summary>Las flechas van a media altura de la foto, montadas sobre el borde de la primera y la última tarjeta.</summary>
    public static readonly StyledProperty<Thickness> MargenFlechaIzquierdaProperty = AvaloniaProperty.Register<InventarioView, Thickness>(nameof(MargenFlechaIzquierda));
    public static readonly StyledProperty<Thickness> MargenFlechaDerechaProperty = AvaloniaProperty.Register<InventarioView, Thickness>(nameof(MargenFlechaDerecha));
    public Thickness MargenFlechaIzquierda { get => GetValue(MargenFlechaIzquierdaProperty); set => SetValue(MargenFlechaIzquierdaProperty, value); }
    public Thickness MargenFlechaDerecha { get => GetValue(MargenFlechaDerechaProperty); set => SetValue(MargenFlechaDerechaProperty, value); }

    private const double Separacion = 20;     // igual al Spacing del panel de tarjetas
    private const double AnchoMinimo = 250;
    private const double AltoTextos = 150;    // marca, nombre (2 líneas), modelo/SKU, precio y stock
    private const double UmbralArrastre = 6;

    private int _porVista = 1;
    private int _paginas;

    public InventarioView()
    {
        InitializeComponent();
        // Esc cierra el diálogo que esté arriba; Enter abre la ficha (catálogo) o la edición (lista).
        AddHandler(KeyDownEvent, Tecla, RoutingStrategies.Tunnel);

        Carrusel.SizeChanged += (_, _) => Medir();
        Carrusel.ScrollChanged += (_, _) => ActualizarPosicion();
        Carrusel.AddHandler(PointerPressedEvent, Presionar, RoutingStrategies.Tunnel);
        Carrusel.AddHandler(PointerMovedEvent, Mover, RoutingStrategies.Tunnel);
        Carrusel.AddHandler(PointerReleasedEvent, Soltar, RoutingStrategies.Tunnel);
        Carrusel.AddHandler(PointerCaptureLostEvent, (_, _) => _presionado = false);
        Carrusel.AddHandler(PointerWheelChangedEvent, Rueda, RoutingStrategies.Tunnel);
        _animacion.Tick += Animar;
    }

    private InventarioViewModel? Vm => DataContext as InventarioViewModel;
    private InventarioViewModel? _vmSuscrito;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vmSuscrito is not null) _vmSuscrito.PropertyChanged -= CambioEnVm;
        _vmSuscrito = Vm;
        if (_vmSuscrito is not null) _vmSuscrito.PropertyChanged += CambioEnVm;
    }

    private void CambioEnVm(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Filtros nuevos: el carrusel vuelve al principio. Un refresco tras editar conserva la posición.
            case nameof(InventarioViewModel.VersionFiltro):
                Detener();
                Carrusel.Offset = default;
                break;
            case nameof(InventarioViewModel.Filas):
            case nameof(InventarioViewModel.VistaCatalogo):
                Dispatcher.UIThread.Post(Medir, DispatcherPriority.Background);
                break;
        }
    }

    /* ─────────── Medidas y posición ─────────── */

    /// <summary>Cuántas tarjetas entran a la vez y de qué tamaño: siempre tarjetas enteras, nunca cortadas al borde.</summary>
    private void Medir()
    {
        var ancho = Carrusel.Bounds.Width;
        var alto = Carrusel.Bounds.Height;
        if (ancho <= 0 || alto <= 0) return;
        // La densidad depende del ancho; aumentar la altura no debe ocultar productos.
        var altoFotoPosible = alto - AltoTextos - 26;
        _porVista = Math.Max(1, (int)((ancho + Separacion) / (AnchoMinimo + Separacion)));
        AnchoTarjeta = Math.Floor((ancho - Separacion * (_porVista - 1)) / _porVista);
        AltoFoto = Math.Floor(Math.Clamp(Math.Min(altoFotoPosible, AnchoTarjeta * 0.7), 130, 260));
        var arriba = 4 + AltoFoto / 2 - 22; // 4 = margen superior del panel de tarjetas; 22 = medio botón
        MargenFlechaIzquierda = new Thickness(-14, arriba, 0, 0);
        MargenFlechaDerecha = new Thickness(0, arriba, -14, 0);
        Dispatcher.UIThread.Post(ActualizarPosicion, DispatcherPriority.Background);
    }

    private double Paso => AnchoTarjeta + Separacion;
    private double Maximo => Math.Max(0, Carrusel.Extent.Width - Carrusel.Viewport.Width);
    private int Cantidad => Vm?.Filas.Count ?? 0;
    private int IndiceActual => (int)Math.Round(Carrusel.Offset.X / Paso);

    private void ActualizarPosicion()
    {
        var x = Carrusel.Offset.X;
        PuedeAtras = x > 1;
        PuedeAdelante = x < Maximo - 1;
        var n = Cantidad;
        var desde = Math.Min(IndiceActual + 1, n);
        var hasta = Math.Min(IndiceActual + _porVista, n);
        TextoPosicion = n == 0 ? "" : $"{desde}–{hasta} de {n}";

        var paginas = n <= _porVista ? 0 : (int)Math.Ceiling(n / (double)_porVista);
        if (paginas > 15) paginas = 0; // con muchas páginas los puntos no ayudan: queda el texto "x–y de n"
        if (paginas != _paginas)
        {
            _paginas = paginas;
            Puntos.Children.Clear();
            for (var i = 0; i < paginas; i++)
            {
                var pagina = i;
                var punto = new Button { Classes = { "punto" }, Content = new Border(), Focusable = false };
                ToolTip.SetTip(punto, $"Página {i + 1}");
                AutomationProperties.SetName(punto, $"Página {i + 1}");
                punto.Click += (_, _) => IrA(pagina * _porVista);
                Puntos.Children.Add(punto);
            }
        }
        var activa = x >= Maximo - 1 ? _paginas - 1 : IndiceActual / _porVista;
        for (var i = 0; i < Puntos.Children.Count; i++)
            Puntos.Children[i].Classes.Set("activo", i == activa);
    }

    /* ─────────── Desplazamiento suave ─────────── */

    private readonly DispatcherTimer _animacion = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _reloj = new();
    private double _desde, _hasta;
    private const double Duracion = 380;

    private void IrA(int indice)
    {
        indice = Math.Clamp(indice, 0, Math.Max(0, Cantidad - 1));
        _desde = Carrusel.Offset.X;
        _hasta = Math.Min(indice * Paso, Maximo);
        if (Math.Abs(_hasta - _desde) < 0.5) return;
        _reloj.Restart();
        _animacion.Start();
    }

    private void Animar(object? sender, EventArgs e)
    {
        var t = Math.Min(1, _reloj.Elapsed.TotalMilliseconds / Duracion);
        var suave = 1 - Math.Pow(1 - t, 3); // desacelera al llegar
        Carrusel.Offset = new Vector(_desde + (_hasta - _desde) * suave, 0);
        if (t >= 1) _animacion.Stop();
    }

    private void Detener() => _animacion.Stop();

    private void Atras_Click(object? sender, RoutedEventArgs e) => IrA(IndiceActual - _porVista);
    private void Adelante_Click(object? sender, RoutedEventArgs e) => IrA(IndiceActual + _porVista);

    private void Rueda(object? sender, PointerWheelEventArgs e)
    {
        var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
        if (delta == 0) return;
        var base_ = _animacion.IsEnabled ? (int)Math.Round(_hasta / Paso) : IndiceActual;
        IrA(base_ + (delta < 0 ? 1 : -1));
        e.Handled = true;
    }

    /* ─────────── Arrastre y clic en tarjetas ─────────── */

    private bool _presionado, _arrastrando;
    private Point _inicio;
    private double _offsetInicio;
    private FilaProducto? _tarjeta;

    private void Presionar(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Carrusel).Properties.IsLeftButtonPressed) return;
        Detener();
        _presionado = true;
        _arrastrando = false;
        _inicio = e.GetPosition(Carrusel);
        _offsetInicio = Carrusel.Offset.X;
        _tarjeta = e.Source is Control c ? TarjetaDe(c) : null;
    }

    private static FilaProducto? TarjetaDe(Control c)
    {
        for (Control? x = c; x is not null; x = x.GetVisualParent() as Control)
            if (x is Border { DataContext: FilaProducto f } borde && borde.Classes.Contains("producto")) return f;
        return null;
    }

    private void Mover(object? sender, PointerEventArgs e)
    {
        if (!_presionado) return;
        var dx = e.GetPosition(Carrusel).X - _inicio.X;
        if (!_arrastrando && Math.Abs(dx) < UmbralArrastre) return;
        if (!_arrastrando)
        {
            _arrastrando = true;
            e.Pointer.Capture(Carrusel);
            Carrusel.Cursor = new Cursor(StandardCursorType.SizeWestEast);
        }
        Carrusel.Offset = new Vector(Math.Clamp(_offsetInicio - dx, 0, Maximo), 0);
        e.Handled = true;
    }

    private void Soltar(object? sender, PointerReleasedEventArgs e)
    {
        if (!_presionado) return;
        _presionado = false;
        if (_arrastrando)
        {
            _arrastrando = false;
            e.Pointer.Capture(null);
            Carrusel.Cursor = Cursor.Default;
            // Al soltar, se acomoda a la tarjeta más cercana para que ninguna quede cortada.
            IrA(IndiceActual);
            e.Handled = true;
        }
        else if (_tarjeta is { } fila && Vm is { } vm)
        {
            vm.AbrirFicha(fila);
            e.Handled = true;
        }
        _tarjeta = null;
    }

    /* ─────────── Teclado ─────────── */

    private void Tecla(object? sender, KeyEventArgs e)
    {
        if (Vm is not { } vm) return;
        if (e.Key == Key.Escape)
        {
            if (vm.VisorAbierto) vm.CerrarVisorCommand.Execute(null);
            else if (vm.ConfirmandoDescartar) vm.SeguirEditandoCommand.Execute(null);
            else if (vm.MostrandoFormulario) vm.CancelarFormularioCommand.Execute(null);
            else if (vm.EntradaAbierta) vm.CerrarEntradaCommand.Execute(null);
            else if (vm.ConfirmandoEliminar) vm.CancelarEliminarCommand.Execute(null);
            else if (vm.FichaAbierta) vm.CerrarFichaCommand.Execute(null);
            else return;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.Source is Control c && TarjetaDe(c) is { } fila && !vm.FichaAbierta)
        {
            vm.AbrirFicha(fila);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && e.Source is ListBoxItem && vm.PuedeEditarSeleccion)
        {
            vm.EditarCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right && vm.VistaCatalogo && e.Source is Control t && TarjetaDe(t) is not null)
        {
            IrA(IndiceActual + (e.Key == Key.Right ? 1 : -1));
            e.Handled = true;
        }
    }

    private void Visor_PointerPressed(object? sender, PointerPressedEventArgs e) => Vm?.CerrarVisorCommand.Execute(null);

    /* ─────────── Lista ─────────── */

    private void Tabla_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Solo sobre una fila (no sobre la barra de desplazamiento ni el vacío).
        if (Vm is { PuedeEditarSeleccion: true } vm && e.Source is Control c && c.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not null)
            vm.EditarCommand.Execute(null);
    }

    /// <summary>El botón ⋮ de la fila selecciona ese producto y abre el mismo menú que el clic derecho.</summary>
    private void Opciones_Click(object? sender, RoutedEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control { DataContext: FilaProducto fila } boton) return;
        vm.FilaSeleccionada = fila;
        if (Tabla.ContextMenu is not { } menu) return;
        // El menú pertenece a la tabla: se abre sobre ella, pero anclado bajo el botón; al cerrarse vuelve al puntero.
        menu.PlacementTarget = boton;
        menu.Placement = PlacementMode.BottomEdgeAlignedRight;
        menu.Closed += VolverAlPuntero;
        menu.Open(Tabla);
    }

    private void VolverAlPuntero(object? sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu) return;
        menu.Closed -= VolverAlPuntero;
        menu.PlacementTarget = null;
        menu.Placement = PlacementMode.Pointer;
    }

    /// <summary>Sin producto seleccionado o sin permisos, el menú quedaría vacío: no se abre.</summary>
    private void MenuFila_Opening(object? sender, CancelEventArgs e)
    {
        if (Vm is not { HaySeleccion: true } vm || !(vm.PuedeGestionar || vm.PuedeRecibirSeleccion)) e.Cancel = true;
    }

    /// <summary>Abre el selector de archivos y pasa la foto elegida al formulario.</summary>
    private async void ElegirFoto_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InventarioViewModel vm || TopLevel.GetTopLevel(this) is not { } ventana) return;
        var archivos = await ventana.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Elegir foto del producto",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Fotos") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp"] },
            ],
        });
        if (archivos.Count == 0) return;
        try
        {
            await using var origen = await archivos[0].OpenReadAsync();
            using var memoria = new MemoryStream();
            await origen.CopyToAsync(memoria);
            vm.UsarFoto(memoria.ToArray(), archivos[0].Name);
        }
        catch (Exception ex)
        {
            vm.Error = $"No se pudo leer la foto: {ex.Message}";
        }
    }
}
