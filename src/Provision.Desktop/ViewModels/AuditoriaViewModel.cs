using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Una fila con barra proporcional (0 a 1) para los gráficos simples del dashboard.</summary>
public sealed record FilaBarra(string Titulo, long Valor, long Negados, double Proporcion, string Detalle)
{
    public bool TieneNegados => Negados > 0;
    public double Ancho => Math.Max(2, Proporcion * 220);
}

/// <summary>Barra vertical de actividad por día.</summary>
public sealed record BarraDia(string Etiqueta, long Eventos, long Negados, double Alto)
{
    public string Tooltip => $"{Etiqueta}: {Eventos} eventos" + (Negados > 0 ? $", {Negados} negados" : "");
}

/// <summary>Actividad de un rol, con un botón para filtrar el registro por ese rol.</summary>
public sealed class FilaRol(ActividadRol a, double proporcion, IRelayCommand filtrar)
{
    public string Nombre { get; } = a.Nombre;
    public long Eventos { get; } = a.Eventos;
    public long Denegados { get; } = a.Denegados;
    public bool TieneDenegados => a.Denegados > 0;
    public string Usuarios { get; } = a.Usuarios == 1 ? "1 usuario" : $"{a.Usuarios} usuarios";
    public string Ultimo { get; } = a.Ultimo is DateTimeOffset u ? "último " + u.LocalDateTime.ToString("dd/MM HH:mm") : "sin actividad";
    public double Ancho { get; } = Math.Max(2, proporcion * 260);
    public bool SinActividad => Eventos == 0;
    public IRelayCommand Filtrar { get; } = filtrar;
}

public sealed record FilaEvento(EventoAuditoria E)
{
    public string Hora => E.CreadoLocal.ToString("dd/MM HH:mm:ss");
    public string Usuario => E.Usuario ?? "—";
    public string Rol => E.NombreRol ?? "Sin sesión";
    public string Modulo => Enumeraciones.EtiquetaModulo(E.Modulo);
    public string Descripcion => E.Descripcion;
    public string Resultado => Enumeraciones.Etiqueta(E.Resultado);
    public string Sucursal => E.Sucursal ?? "";
    public bool EsExito => E.Resultado == ResultadoEvento.Exito;
    public bool EsNegado => E.Resultado is ResultadoEvento.Denegado or ResultadoEvento.Fallido;
    public bool EsRechazado => E.Resultado is ResultadoEvento.Rechazado or ResultadoEvento.Error;
}

/// <summary>
/// Dashboard de eventos (exclusivo del super administrador): qué hizo cada rol y cada usuario,
/// qué se negó o rechazó, y el registro detallado con filtros.
/// </summary>
public sealed partial class AuditoriaViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public AuditoriaViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _periodo = Periodos[1];
        _filtroRol = RolesFiltro[0];
        _filtroResultado = Resultados[0];
        _filtroModulo = Modulos[0];
    }

    /* ─────────── Filtros ─────────── */

    public IReadOnlyList<Opcion<int>> Periodos { get; } =
        [new(0, "Hoy"), new(6, "Últimos 7 días"), new(29, "Últimos 30 días"), new(89, "Últimos 90 días")];

    public IReadOnlyList<Opcion<Rol?>> RolesFiltro { get; } =
        new[] { new Opcion<Rol?>(null, "Todos los roles") }
            .Concat(Enum.GetValues<Rol>().Select(r => new Opcion<Rol?>(r, Enumeraciones.Etiqueta(r))))
            .ToList();

    public IReadOnlyList<Opcion<ResultadoEvento?>> Resultados { get; } =
        new[] { new Opcion<ResultadoEvento?>(null, "Todos los resultados") }
            .Concat(Enum.GetValues<ResultadoEvento>().Select(r => new Opcion<ResultadoEvento?>(r, Enumeraciones.Etiqueta(r))))
            .ToList();

    public IReadOnlyList<Opcion<string?>> Modulos { get; } =
        new[] { new Opcion<string?>(null, "Todos los módulos") }
            .Concat(new[] { "acceso", "ventas", "caja", "inventario", "catalogo", "clientes", "laboratorio", "usuarios", "sucursales" }
                .Select(m => new Opcion<string?>(m, Enumeraciones.EtiquetaModulo(m))))
            .ToList();

    [ObservableProperty] private Opcion<int> _periodo;
    [ObservableProperty] private Opcion<Rol?> _filtroRol;
    [ObservableProperty] private Opcion<ResultadoEvento?> _filtroResultado;
    [ObservableProperty] private Opcion<string?> _filtroModulo;
    [ObservableProperty] private string _busqueda = "";

    private bool _cargando;

    partial void OnPeriodoChanged(Opcion<int> value) => Recargar();
    partial void OnFiltroRolChanged(Opcion<Rol?> value) => Recargar();
    partial void OnFiltroResultadoChanged(Opcion<ResultadoEvento?> value) => Recargar();
    partial void OnFiltroModuloChanged(Opcion<string?> value) => Recargar();

    private void Recargar()
    {
        if (!_cargando) _ = CargarAsync();
    }

    [RelayCommand]
    private void LimpiarFiltros()
    {
        _cargando = true;
        FiltroRol = RolesFiltro[0];
        FiltroResultado = Resultados[0];
        FiltroModulo = Modulos[0];
        Busqueda = "";
        _cargando = false;
        _ = CargarAsync();
    }

    private FiltrosAuditoria Filtros()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        return new FiltrosAuditoria
        {
            Desde = hoy.AddDays(-Periodo.Valor),
            Hasta = hoy,
            Rol = FiltroRol.Valor,
            Resultado = FiltroResultado.Valor,
            Modulo = FiltroModulo.Valor,
            Buscar = string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda.Trim(),
        };
    }

    /* ─────────── Datos ─────────── */

    [ObservableProperty] private long _totalEventos;
    [ObservableProperty] private long _usuariosActivos;
    [ObservableProperty] private long _negados;
    [ObservableProperty] private long _rechazados;
    [ObservableProperty] private string _tasaExito = "";
    [ObservableProperty] private IReadOnlyList<FilaRol> _porRol = [];
    [ObservableProperty] private IReadOnlyList<FilaBarra> _porModulo = [];
    [ObservableProperty] private IReadOnlyList<FilaBarra> _porUsuario = [];
    [ObservableProperty] private IReadOnlyList<BarraDia> _porDia = [];
    [ObservableProperty] private IReadOnlyList<AlertaAuditoria> _alertas = [];
    [ObservableProperty] private IReadOnlyList<FilaEvento> _eventos = [];
    [ObservableProperty] private string _resumenEventos = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinAlertas))]
    private bool _hayAlertas;

    public bool SinAlertas => !HayAlertas;

    public Task ActivarAsync() => CargarAsync();

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        var f = Filtros();
        // El resumen por rol se calcula sin el filtro de rol, para comparar siempre todos los roles.
        var resumenTask = _sesion.Api.ResumenAuditoriaAsync(f with { Rol = null });
        var eventosTask = _sesion.Api.EventosAsync(f, porPagina: 200);
        var r = await resumenTask;
        var lista = await eventosTask;

        TotalEventos = r.Totales.Eventos;
        UsuariosActivos = r.Totales.Usuarios;
        Negados = r.Totales.Denegados + r.Totales.Fallidos;
        Rechazados = r.Totales.Rechazados + r.Totales.Errores;
        TasaExito = r.Totales.Eventos > 0 ? $"{Math.Round(100.0 * r.Totales.Exitos / r.Totales.Eventos)}% con éxito" : "sin actividad";

        var maxRol = Math.Max(1, r.PorRol.Select(x => x.Eventos).DefaultIfEmpty(0).Max());
        PorRol = r.PorRol
            .Select(a => new FilaRol(a, (double)a.Eventos / maxRol, new RelayCommand(() => FiltroRol = RolesFiltro.First(o => o.Valor == a.Rol))))
            .ToList();

        var maxMod = Math.Max(1, r.PorModulo.Select(x => x.Eventos).DefaultIfEmpty(0).Max());
        PorModulo = r.PorModulo
            .Select(m => new FilaBarra(Enumeraciones.EtiquetaModulo(m.Modulo), m.Eventos, m.Denegados, (double)m.Eventos / maxMod, ""))
            .ToList();

        var maxUsu = Math.Max(1, r.PorUsuario.Select(x => x.Eventos).DefaultIfEmpty(0).Max());
        PorUsuario = r.PorUsuario
            .Select(u => new FilaBarra(u.Usuario ?? "—", u.Eventos, u.Denegados, (double)u.Eventos / maxUsu, u.NombreRol ?? ""))
            .ToList();

        var maxDia = Math.Max(1, r.PorDia.Select(x => x.Eventos).DefaultIfEmpty(0).Max());
        PorDia = r.PorDia
            .Select(d => new BarraDia(
                DateOnly.TryParse(d.Dia, out var dia) ? dia.ToString("dd/MM") : d.Dia,
                d.Eventos, d.Negados, Math.Max(3, 110.0 * d.Eventos / maxDia)))
            .ToList();

        Alertas = r.Alertas;
        HayAlertas = r.Alertas.Count > 0;

        Eventos = lista.Datos.Select(e => new FilaEvento(e)).ToList();
        ResumenEventos = lista.Total > lista.Datos.Count
            ? $"Mostrando los {lista.Datos.Count} más recientes de {lista.Total}"
            : $"{lista.Total} eventos";
    });
}
