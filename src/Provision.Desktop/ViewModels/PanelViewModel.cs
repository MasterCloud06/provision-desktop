using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Una tarea de "qué hacer ahora", con botón para ir a la pantalla donde se resuelve.</summary>
public sealed class TareaItem(TareaPanel t, string? textoBoton, IAsyncRelayCommand? ir)
{
    public string Titulo { get; } = t.Titulo;
    public string Detalle { get; } = t.Detalle;
    public bool TieneCantidad { get; } = t.Cantidad is not null;
    public string Cantidad { get; } = t.Cantidad?.ToString() ?? "";
    public bool EsAlta { get; } = t.Urgencia == Urgencia.Alta;
    public bool EsMedia { get; } = t.Urgencia == Urgencia.Media;
    public bool EsInfo { get; } = t.Urgencia == Urgencia.Info;
    public bool EsOk { get; } = t.Urgencia == Urgencia.Ok;
    public string Etiqueta { get; } = t.Urgencia switch
    {
        Urgencia.Alta => "URGENTE",
        Urgencia.Media => "PENDIENTE",
        Urgencia.Info => "PARA HOY",
        _ => "AL DÍA",
    };
    public bool TieneBoton { get; } = ir is not null;
    public string TextoBoton { get; } = textoBoton ?? "";
    public IAsyncRelayCommand? Ir { get; } = ir;
}

/// <summary>
/// "Mi panel": la primera pantalla de cada usuario. Explica para qué existe su rol,
/// qué se espera de él y qué tiene que hacer ahora, según los datos del momento.
/// </summary>
public sealed partial class PanelViewModel(Sesion sesion, Func<string, Task> irA, Func<string, bool> tienePantalla)
    : ViewModelBase, ISeccion
{
    private static readonly Dictionary<string, string> NombresPantalla = new()
    {
        ["indicadores"] = "Ver indicadores",
        ["auditoria"] = "Ir a Auditoría",
        ["vender"] = "Ir a Vender",
        ["ventas"] = "Ir a Ventas",
        ["caja"] = "Ir a Caja",
        ["laboratorio"] = "Ir a Laboratorio",
        ["clientes"] = "Ir a Clientes",
        ["inventario"] = "Ir a Inventario",
        ["usuarios"] = "Ir a Usuarios",
    };

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

    [ObservableProperty] private string _nombreRol = "";
    [ObservableProperty] private string _proposito = "";
    [ObservableProperty] private IReadOnlyList<string> _responsabilidades = [];
    [ObservableProperty] private IReadOnlyList<TareaItem> _tareas = [];
    [ObservableProperty] private IReadOnlyList<IndicadorPanel> _indicadores = [];
    [ObservableProperty] private string _actualizado = "";

    public Task ActivarAsync() => ActualizarAsync();

    [RelayCommand]
    private Task ActualizarAsync() => IntentarAsync(async () =>
    {
        var p = await sesion.Api.PanelAsync();
        NombreRol = p.NombreRol;
        Proposito = p.Proposito;
        Responsabilidades = p.Responsabilidades;
        Indicadores = p.Indicadores;
        Tareas = p.Tareas.Select(t =>
        {
            // Solo hay botón si este usuario tiene esa pantalla en su menú.
            var destino = t.Pantalla is { } pantalla && tienePantalla(pantalla) ? pantalla : null;
            return new TareaItem(
                t,
                destino is null ? null : NombresPantalla.GetValueOrDefault(destino, "Ir"),
                destino is null ? null : new AsyncRelayCommand(() => irA(destino)));
        }).ToList();
        Actualizado = $"Actualizado a las {DateTime.Now:HH:mm}";
    });
}
