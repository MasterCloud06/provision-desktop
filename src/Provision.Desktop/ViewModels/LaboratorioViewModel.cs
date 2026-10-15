using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Un botón para mover la orden al siguiente estado.</summary>
public sealed class AccionTrabajo(string texto, bool principal, IAsyncRelayCommand comando)
{
    public string Texto { get; } = texto;
    public bool Principal { get; } = principal;
    public IAsyncRelayCommand Comando { get; } = comando;
}

/// <summary>Tablero de órdenes de laboratorio: del pedido a la entrega de los lentes.</summary>
public sealed partial class LaboratorioViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public LaboratorioViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _filtro = Filtros[0];
    }

    public IReadOnlyList<Opcion<string>> Filtros { get; } =
    [
        new("activas", "En curso (pendiente, laboratorio, listo)"),
        new("PENDIENTE", "Pendientes"),
        new("EN_LABORATORIO", "En laboratorio"),
        new("LISTO", "Listas para entregar"),
        new("atrasadas", "Atrasadas"),
        new("ENTREGADO", "Entregadas"),
    ];

    [ObservableProperty] private Opcion<string> _filtro;
    [ObservableProperty] private IReadOnlyList<OrdenTrabajoFila> _ordenes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HaySeleccion))]
    private OrdenTrabajoFila? _seleccionada;

    public bool HaySeleccion => Seleccionada is not null;

    [ObservableProperty] private OrdenTrabajoDetalle? _detalle;
    [ObservableProperty] private IReadOnlyList<AccionTrabajo> _acciones = [];
    [ObservableProperty] private string _nota = "";

    public Task ActivarAsync() => CargarAsync();

    partial void OnFiltroChanged(Opcion<string> value) => _ = CargarAsync();

    partial void OnSeleccionadaChanged(OrdenTrabajoFila? value)
    {
        Detalle = null;
        Acciones = [];
        if (value is not null) _ = CargarDetalleAsync(value.Id);
    }

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        var seleccionadaId = Seleccionada?.Id;
        List<OrdenTrabajoFila> lista = Filtro.Valor switch
        {
            "activas" => (await _sesion.Api.OrdenesTrabajoAsync())
                .Where(o => o.Estado is EstadoTrabajo.Pendiente or EstadoTrabajo.EnLaboratorio or EstadoTrabajo.Listo).ToList(),
            "atrasadas" => await _sesion.Api.OrdenesTrabajoAsync(atrasadas: true),
            var e => await _sesion.Api.OrdenesTrabajoAsync(Enum.Parse<EstadoTrabajo>(e.Replace("_", ""), ignoreCase: true)),
        };
        Ordenes = lista;
        Seleccionada = Ordenes.FirstOrDefault(o => o.Id == seleccionadaId);
    });

    private Task CargarDetalleAsync(Guid id) => IntentarAsync(async () =>
    {
        Detalle = await _sesion.Api.OrdenTrabajoAsync(id);
        Acciones = AccionesPara(Detalle.Estado);
    });

    /// <summary>Solo los pasos que permite el flujo y que el rol puede hacer.</summary>
    private List<AccionTrabajo> AccionesPara(EstadoTrabajo estado)
    {
        var lista = new List<AccionTrabajo>();
        void Agregar(string texto, EstadoTrabajo destino, string permiso, bool principal = false)
        {
            if (_sesion.Puede(permiso))
                lista.Add(new AccionTrabajo(texto, principal, new AsyncRelayCommand(() => MoverAsync(destino))));
        }
        switch (estado)
        {
            case EstadoTrabajo.Pendiente:
                Agregar("Enviar a laboratorio", EstadoTrabajo.EnLaboratorio, "laboratorio.gestionar", true);
                Agregar("Marcar como listo", EstadoTrabajo.Listo, "laboratorio.gestionar");
                Agregar("Cancelar orden", EstadoTrabajo.Cancelado, "laboratorio.cancelar");
                break;
            case EstadoTrabajo.EnLaboratorio:
                Agregar("Lentes listos", EstadoTrabajo.Listo, "laboratorio.gestionar", true);
                Agregar("Volver a pendiente", EstadoTrabajo.Pendiente, "laboratorio.gestionar");
                Agregar("Cancelar orden", EstadoTrabajo.Cancelado, "laboratorio.cancelar");
                break;
            case EstadoTrabajo.Listo:
                Agregar("Entregar al cliente", EstadoTrabajo.Entregado, "laboratorio.entregar", true);
                Agregar("Devolver a laboratorio", EstadoTrabajo.EnLaboratorio, "laboratorio.gestionar");
                break;
        }
        return lista;
    }

    private async Task MoverAsync(EstadoTrabajo destino)
    {
        if (Seleccionada is not { } orden) return;
        var nota = string.IsNullOrWhiteSpace(Nota) ? null : Nota.Trim();
        if (!await IntentarAsync(() => _sesion.Api.CambiarEstadoTrabajoAsync(orden.Id, destino, nota))) return;
        Nota = "";
        var mensaje = $"{orden.Numero}: {Enumeraciones.Etiqueta(destino)}.";
        await CargarAsync();
        if (Seleccionada is null) Detalle = null;
        else await CargarDetalleAsync(Seleccionada.Id);
        Aviso = mensaje;
    }
}
