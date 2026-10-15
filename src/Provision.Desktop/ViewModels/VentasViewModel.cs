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
    [ObservableProperty] private IReadOnlyList<VentaResumen> _ventas = [];
    [ObservableProperty] private string _resumen = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeAbonar))]
    private VentaResumen? _seleccionada;

    [ObservableProperty] private Opcion<MetodoPago> _metodo;
    [ObservableProperty] private string _montoAbono = "";
    [ObservableProperty] private string _referencia = "";

    public bool PuedeAbonar => Seleccionada is { Saldo: > 0, Estado: EstadoVenta.Activa };

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
        if (Seleccionada is not { } venta) return;
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
