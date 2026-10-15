using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

public sealed record FilaTransaccion(DateOnly Fecha, string Descripcion, string Detalle, string Monto, bool EsIngreso, bool DesdeCaja);

/// <summary>Finanzas personales del usuario: cuentas, movimientos y presupuesto del mes.</summary>
public sealed partial class FinanzasViewModel : ViewModelBase, ISeccion
{
    private static readonly CultureInfo Es = new("es-BO");
    private readonly Sesion _sesion;
    private List<CategoriaPersonal> _categorias = [];

    public FinanzasViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _tipo = Tipos[1];
        _tipoCuenta = TiposCuenta[0];
    }

    /* Mes */

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NombreMes))]
    private DateTime _mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public string NombreMes => Es.TextInfo.ToTitleCase(Mes.ToString("MMMM yyyy", Es));
    private string MesApi => Mes.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    [RelayCommand]
    private Task MesAnteriorAsync() { Mes = Mes.AddMonths(-1); return CargarAsync(); }

    [RelayCommand]
    private Task MesSiguienteAsync() { Mes = Mes.AddMonths(1); return CargarAsync(); }

    /* Datos */

    [ObservableProperty] private ResumenFinanzas? _resumen;
    [ObservableProperty] private IReadOnlyList<CuentaPersonal> _cuentas = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinMovimientos))]
    private IReadOnlyList<FilaTransaccion> _transacciones = [];

    public bool SinMovimientos => Transacciones.Count == 0;
    [ObservableProperty] private IReadOnlyList<LineaPresupuesto> _presupuesto = [];
    [ObservableProperty] private IReadOnlyList<GastoCategoria> _gastosPorCategoria = [];

    public Task ActivarAsync() => CargarAsync();

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        if (_categorias.Count == 0) _categorias = await _sesion.Api.CategoriasAsync();
        var cuentas = await _sesion.Api.CuentasAsync();
        Cuentas = cuentas.Lista.Where(c => !c.Archivada).ToList();
        Resumen = await _sesion.Api.ResumenFinanzasAsync(MesApi);
        Presupuesto = Resumen.Presupuesto;
        GastosPorCategoria = Resumen.PorCategoria.Where(c => c.Tipo == TipoCategoria.Gasto).ToList();
        var nombres = Cuentas.ToDictionary(c => c.Id, c => c.Nombre);
        Transacciones = (await _sesion.Api.TransaccionesAsync(MesApi)).Select(t => new FilaTransaccion(
            t.Fecha,
            string.IsNullOrWhiteSpace(t.Descripcion) ? Enumeraciones.Etiqueta(t.Tipo) : t.Descripcion,
            t.Tipo == TipoTransaccion.Transferencia
                ? $"{nombres.GetValueOrDefault(t.CuentaId, "?")} → {nombres.GetValueOrDefault(t.CuentaDestinoId ?? Guid.Empty, "?")}"
                : $"{t.Categoria} · {nombres.GetValueOrDefault(t.CuentaId, "?")}",
            (t.Tipo == TipoTransaccion.Gasto ? "-" : t.Tipo == TipoTransaccion.Ingreso ? "+" : "") + Dinero.Formatear(t.Monto),
            t.Tipo == TipoTransaccion.Ingreso,
            t.DesdeCaja)).ToList();
        ActualizarCategorias();
        // Las cuentas se recargan con saldos nuevos: se vuelve a elegir la misma por Id.
        CuentaOrigen = Cuentas.FirstOrDefault(c => c.Id == CuentaOrigen?.Id) ?? Cuentas.FirstOrDefault();
        CuentaDestino = Cuentas.FirstOrDefault(c => c.Id == CuentaDestino?.Id);
        CategoriaPresupuesto ??= _categorias.FirstOrDefault(c => c.Tipo == TipoCategoria.Gasto);
    });

    public IReadOnlyList<CategoriaPersonal> CategoriasGasto => _categorias.Where(c => c.Tipo == TipoCategoria.Gasto && !c.Archivada).ToList();

    /* Nuevo movimiento */

    public IReadOnlyList<Opcion<TipoTransaccion>> Tipos { get; } =
        Enum.GetValues<TipoTransaccion>().Select(t => new Opcion<TipoTransaccion>(t, Enumeraciones.Etiqueta(t))).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsTransferencia), nameof(LlevaCategoria))]
    private Opcion<TipoTransaccion> _tipo;

    public bool EsTransferencia => Tipo.Valor == TipoTransaccion.Transferencia;
    public bool LlevaCategoria => !EsTransferencia;

    [ObservableProperty] private IReadOnlyList<CategoriaPersonal> _categoriasDelTipo = [];
    [ObservableProperty] private CategoriaPersonal? _categoria;
    [ObservableProperty] private CuentaPersonal? _cuentaOrigen;
    [ObservableProperty] private CuentaPersonal? _cuentaDestino;
    [ObservableProperty] private string _monto = "";
    [ObservableProperty] private string _descripcion = "";
    [ObservableProperty] private DateTime? _fecha = DateTime.Today;

    partial void OnTipoChanged(Opcion<TipoTransaccion> value) => ActualizarCategorias();

    private void ActualizarCategorias()
    {
        var tipo = Tipo.Valor == TipoTransaccion.Ingreso ? TipoCategoria.Ingreso : TipoCategoria.Gasto;
        CategoriasDelTipo = _categorias.Where(c => c.Tipo == tipo && !c.Archivada).ToList();
        if (Categoria is null || Categoria.Tipo != tipo) Categoria = CategoriasDelTipo.FirstOrDefault();
        OnPropertyChanged(nameof(CategoriasGasto));
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        if (CuentaOrigen is null) { Falla("Primero crea una cuenta (efectivo, banco…)"); return; }
        if (!Dinero.TryLeer(Monto, out var monto) || monto <= 0) { Falla("Monto inválido"); return; }
        if (EsTransferencia && CuentaDestino is null) { Falla("Elige la cuenta destino"); return; }
        if (!EsTransferencia && Categoria is null) { Falla("Elige una categoría"); return; }
        var t = new NuevaTransaccion
        {
            Tipo = Tipo.Valor,
            CuentaId = CuentaOrigen.Id,
            CuentaDestinoId = EsTransferencia ? CuentaDestino!.Id : null,
            CategoriaId = EsTransferencia ? null : Categoria!.Id,
            Monto = monto,
            Fecha = Fecha is DateTime f ? DateOnly.FromDateTime(f) : null,
            Descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim(),
        };
        if (!await IntentarAsync(() => _sesion.Api.CrearTransaccionAsync(t))) return;
        Monto = Descripcion = "";
        await CargarAsync();
        Aviso = $"{Tipo.Texto} de {Dinero.Formatear(monto)} registrado.";
    }

    /* Nueva cuenta */

    public IReadOnlyList<Opcion<TipoCuentaPersonal>> TiposCuenta { get; } =
        Enum.GetValues<TipoCuentaPersonal>().Select(t => new Opcion<TipoCuentaPersonal>(t, Enumeraciones.Etiqueta(t))).ToList();

    [ObservableProperty] private bool _creandoCuenta;
    [ObservableProperty] private string _nombreCuenta = "";
    [ObservableProperty] private Opcion<TipoCuentaPersonal> _tipoCuenta;
    [ObservableProperty] private string _saldoInicialCuenta = "";

    [RelayCommand]
    private void AlternarNuevaCuenta() => CreandoCuenta = !CreandoCuenta;

    [RelayCommand]
    private async Task CrearCuentaAsync()
    {
        if (NombreCuenta.Trim().Length == 0) { Falla("Ponle un nombre a la cuenta"); return; }
        long saldo = 0;
        if (!string.IsNullOrWhiteSpace(SaldoInicialCuenta) && !Dinero.TryLeer(SaldoInicialCuenta, out saldo)) { Falla("Saldo inicial inválido"); return; }
        var nueva = new NuevaCuenta(NombreCuenta.Trim(), TipoCuenta.Valor, saldo);
        if (!await IntentarAsync(() => _sesion.Api.CrearCuentaAsync(nueva))) return;
        NombreCuenta = SaldoInicialCuenta = "";
        CreandoCuenta = false;
        await CargarAsync();
        Aviso = $"Cuenta \"{nueva.Nombre}\" creada.";
    }

    /* Presupuesto */

    [ObservableProperty] private CategoriaPersonal? _categoriaPresupuesto;
    [ObservableProperty] private string _montoPresupuesto = "";

    [RelayCommand]
    private async Task GuardarPresupuestoAsync()
    {
        if (CategoriaPresupuesto is null) return;
        if (!Dinero.TryLeer(MontoPresupuesto, out var monto) || monto < 0) { Falla("Monto de presupuesto inválido"); return; }
        if (!await IntentarAsync(() => _sesion.Api.GuardarPresupuestoAsync(CategoriaPresupuesto.Id, MesApi, monto))) return;
        MontoPresupuesto = "";
        await CargarAsync();
        Aviso = $"Presupuesto de {CategoriaPresupuesto.Nombre} para {NombreMes}: {Dinero.Formatear(monto)}.";
    }
}
