using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Historial de ventas y cobro de saldos pendientes.</summary>
public sealed partial class VentasViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public VentasViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _filtro = Filtros[0];
        _metodo = Metodos[0];
    }

    public IReadOnlyList<Opcion<string>> Filtros { get; } =
    [
        new("hoy", "Hoy"),
        new("semana", "Últimos 7 días"),
        new("mes", "Este mes"),
        new("saldo", "Con saldo pendiente"),
    ];

    public IReadOnlyList<Opcion<MetodoPago>> Metodos { get; } =
        Enum.GetValues<MetodoPago>().Select(m => new Opcion<MetodoPago>(m, Enumeraciones.Etiqueta(m))).ToList();

    [ObservableProperty] private Opcion<string> _filtro;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(VentasVisibles))] private IReadOnlyList<VentaResumen> _ventas = [];
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(VentasVisibles))] private string _busqueda = "";
    public IReadOnlyList<VentaResumen> VentasVisibles => Ventas.Where(v => string.IsNullOrWhiteSpace(Busqueda) || v.Numero.Contains(Busqueda, StringComparison.OrdinalIgnoreCase) || (v.Cliente?.Contains(Busqueda, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
    [ObservableProperty] private string _resumen = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAbonar), nameof(PuedeAnular))]
    private VentaResumen? _seleccionada;

    [ObservableProperty] private Opcion<MetodoPago> _metodo;
    [ObservableProperty] private string _montoAbono = "";
    [ObservableProperty] private string _referencia = "";

    /// <summary>Solo quien opera caja cobra saldos.</summary>
    public bool OperaCaja => _sesion.Puede("caja.operar");

    public bool PuedeAbonar => OperaCaja && Seleccionada is { Saldo: > 0, Estado: EstadoVenta.Activa };

    public bool PuedeAnular => _sesion.Puede("ventas.anular") && Seleccionada is { Estado: EstadoVenta.Activa };
    [ObservableProperty] private bool _confirmandoAnulacion;
    [ObservableProperty] private string _motivoAnulacion = "";
    private Guid? _ventaPorAnular;

    [RelayCommand]
    private void PrepararAnulacion()
    {
        if (!PuedeAnular || Ocupado) return;
        _ventaPorAnular = Seleccionada!.Id;
        MotivoAnulacion = "";
        ConfirmandoAnulacion = true;
    }
    [RelayCommand] private void CancelarAnulacion() => ConfirmandoAnulacion = false;
    [RelayCommand]
    private async Task AnularAsync()
    {
        if (!_sesion.Puede("ventas.anular") || _ventaPorAnular is not { } id || Ocupado) return;
        if (MotivoAnulacion.Trim().Length is < 5 or > 300) { Falla("Escribe un motivo de 5 a 300 caracteres."); return; }
        if (!await IntentarAsync(() => _sesion.Api.AnularVentaAsync(id, MotivoAnulacion.Trim()))) return;
        ConfirmandoAnulacion = false;
        Seleccionada = null;
        await CargarAsync();
        Aviso = "Venta anulada. ENAI actualizó las existencias y los reembolsos de caja.";
    }

    public Task ActivarAsync() => CargarAsync();

    partial void OnFiltroChanged(Opcion<string> value) => _ = CargarAsync();

    partial void OnSeleccionadaChanged(VentaResumen? value) =>
        MontoAbono = value is { Saldo: > 0 } ? Dinero.ParaEditar(value.Saldo) : "";

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var r = Filtro.Valor switch
        {
            "hoy" => await _sesion.Api.VentasAsync(desde: hoy, hasta: hoy),
            "semana" => await _sesion.Api.VentasAsync(desde: hoy.AddDays(-6), hasta: hoy),
            "mes" => await _sesion.Api.VentasAsync(desde: new DateOnly(hoy.Year, hoy.Month, 1), hasta: hoy),
            _ => await _sesion.Api.VentasAsync(conSaldo: true),
        };
        Ventas = r.Datos;
        var pendiente = r.Datos.Where(v => v.Estado == EstadoVenta.Activa).Sum(v => v.Saldo);
        Resumen = $"{r.Total} ventas · {Dinero.Formatear(r.MontoTotal)}" +
                  (pendiente > 0 ? $" · por cobrar {Dinero.Formatear(pendiente)}" : "");
    });

    [RelayCommand]
    private async Task AbonarAsync()
    {
        if (!PuedeAbonar || Ocupado || Seleccionada is not { } venta) return;
        if (!Dinero.TryLeer(MontoAbono, out var monto) || monto <= 0) { Falla("Monto inválido"); return; }
        if (monto > venta.Saldo) { Falla($"El saldo de esta venta es {Dinero.Formatear(venta.Saldo)}"); return; }
        var ok = await IntentarAsync(() => _sesion.Api.RegistrarPagosAsync(venta.Id,
            [new Pago { Metodo = Metodo.Valor, Monto = monto, Referencia = string.IsNullOrWhiteSpace(Referencia) ? null : Referencia.Trim() }]));
        if (!ok) return;
        var mensaje = $"Abono de {Dinero.Formatear(monto)} registrado en {venta.Numero}.";
        Referencia = "";
        await CargarAsync();
        Aviso = mensaje;
    }
}
