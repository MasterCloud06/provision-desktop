using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

public sealed record Indicador(string Titulo, string Valor, string? Detalle = null);

public sealed partial class InicioViewModel(Sesion sesion) : ViewModelBase, ISeccion
{
    public string Saludo
    {
        get
        {
            var h = DateTime.Now.Hour;
            var parte = h < 12 ? "Buenos días" : h < 19 ? "Buenas tardes" : "Buenas noches";
            return $"{parte}, {sesion.Usuario.Nombre.Split(' ')[0]}";
        }
    }

    public string Fecha => DateTime.Now.ToString("dddd d 'de' MMMM", new System.Globalization.CultureInfo("es-BO"));

    [ObservableProperty] private IReadOnlyList<Indicador> _indicadores = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinVentas))]
    private IReadOnlyList<ProductoTop> _topProductos = [];

    public bool SinVentas => TopProductos.Count == 0;
    [ObservableProperty] private IReadOnlyList<Indicador> _laboratorio = [];
    [ObservableProperty] private string _estadoCaja = "";
    [ObservableProperty] private bool _cajaAbierta;

    public Task ActivarAsync() => ActualizarAsync();

    [RelayCommand]
    private Task ActualizarAsync() => IntentarAsync(async () =>
    {
        var d = await sesion.Api.DashboardAsync();
        CajaAbierta = d.CajaAbierta is not null;
        EstadoCaja = CajaAbierta ? "Caja abierta" : "Caja cerrada";
        Indicadores =
        [
            new("Ventas de hoy", Dinero.Formatear(d.Hoy.Monto), $"{d.Hoy.Ventas} ventas"),
            new("Ventas del mes", Dinero.Formatear(d.Ventas.Monto), $"{d.Ventas.Ventas} ventas · ticket {Dinero.Formatear(d.Ventas.TicketPromedio)}"),
            new("Margen bruto del mes", Dinero.Formatear(d.MargenBruto.Utilidad), d.MargenBruto.Porcentaje is int p ? $"{p}% sobre ventas" : "sin ventas aún"),
            new("Por cobrar", Dinero.Formatear(d.CuentasPorCobrar.Monto), $"{d.CuentasPorCobrar.Ventas} ventas con saldo"),
            new("Bajo stock", d.ProductosBajoStock.ToString(), "productos en o bajo el mínimo"),
        ];
        Laboratorio = Enum.GetValues<EstadoTrabajo>()
            .Where(e => e is not EstadoTrabajo.Entregado and not EstadoTrabajo.Cancelado)
            .Select(e => new Indicador(Enumeraciones.Etiqueta(e),
                (d.OrdenesTrabajo.TryGetValue(Enumeraciones.ApiValor(e), out var n) ? n : 0).ToString()))
            .ToList();
        TopProductos = d.TopProductos;
    });
}
