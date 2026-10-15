using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

public sealed record TotalMetodo(string Metodo, long Ingresos, long Egresos, long Neto);

/// <summary>Apertura, movimientos y cierre con arqueo de la caja de la sucursal.</summary>
public sealed partial class CajaViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public CajaViewModel(Sesion sesion)
    {
        _sesion = sesion;
        Conceptos = sesion.Puede("caja.retiro_dueno")
            ? [Gasto, Deposito, Retiro]
            : [Gasto, Deposito];
        _concepto = Conceptos[0];
    }

    private static readonly Opcion<ConceptoCaja> Gasto = new(ConceptoCaja.Gasto, "Gasto (sale dinero)");
    private static readonly Opcion<ConceptoCaja> Deposito = new(ConceptoCaja.Deposito, "Depósito / sencillo (entra dinero)");
    private static readonly Opcion<ConceptoCaja> Retiro = new(ConceptoCaja.RetiroDueno, "Retiro del dueño");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Abierta), nameof(Cerrada))]
    private ResumenCaja? _resumen;

    public bool Abierta => Resumen is not null;
    public bool Cerrada => Resumen is null;

    [ObservableProperty] private IReadOnlyList<TotalMetodo> _totales = [];
    [ObservableProperty] private IReadOnlyList<MovimientoCaja> _movimientos = [];

    /* Apertura */
    [ObservableProperty] private string _montoApertura = "";

    /* Movimiento manual */
    public IReadOnlyList<Opcion<ConceptoCaja>> Conceptos { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsRetiro))]
    private Opcion<ConceptoCaja> _concepto;

    public bool EsRetiro => Concepto.Valor == ConceptoCaja.RetiroDueno;

    [ObservableProperty] private string _montoMovimiento = "";
    [ObservableProperty] private string _descripcionMovimiento = "";
    [ObservableProperty] private IReadOnlyList<CuentaPersonal> _cuentasPersonales = [];
    [ObservableProperty] private CuentaPersonal? _cuentaDestino;

    /* Cierre */
    [ObservableProperty] private string _efectivoContado = "";
    [ObservableProperty] private string _resultadoCierre = "";

    public Task ActivarAsync() => CargarAsync();

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        Resumen = await _sesion.Api.CajaActualAsync();
        if (Resumen is null)
        {
            Totales = [];
            Movimientos = [];
            return;
        }
        Totales = Enum.GetValues<MetodoPago>()
            .Select(m => (m, t: Resumen.PorMetodo.GetValueOrDefault(Enumeraciones.ApiValor(m)) ?? new TotalesMetodo()))
            .Select(x => new TotalMetodo(Enumeraciones.Etiqueta(x.m), x.t.Ingresos, x.t.Egresos, x.t.Neto))
            .ToList();
        Movimientos = (await _sesion.Api.DetalleSesionAsync(Resumen.Sesion.Id)).Movimientos;
        if (_sesion.Puede("caja.retiro_dueno") && CuentasPersonales.Count == 0)
            CuentasPersonales = (await _sesion.Api.CuentasAsync()).Lista.Where(c => !c.Archivada).ToList();
    });

    [RelayCommand]
    private async Task AbrirAsync()
    {
        if (!Dinero.TryLeer(MontoApertura, out var monto) || monto < 0) { Falla("Indica el fondo inicial (puede ser 0)"); return; }
        if (await IntentarAsync(() => _sesion.Api.AbrirCajaAsync(monto)))
        {
            MontoApertura = "";
            ResultadoCierre = "";
            await CargarAsync();
            Aviso = $"Caja abierta con {Dinero.Formatear(monto)}.";
        }
    }

    [RelayCommand]
    private async Task RegistrarMovimientoAsync()
    {
        if (!Dinero.TryLeer(MontoMovimiento, out var monto) || monto <= 0) { Falla("Monto inválido"); return; }
        if (DescripcionMovimiento.Trim().Length < 3) { Falla("Describe el movimiento (ej. \"Pasajes del mensajero\")"); return; }
        var movimiento = new NuevoMovimientoCaja
        {
            Concepto = Concepto.Valor,
            Monto = monto,
            Descripcion = DescripcionMovimiento.Trim(),
            CuentaPersonalId = EsRetiro ? CuentaDestino?.Id : null,
        };
        if (await IntentarAsync(() => _sesion.Api.RegistrarMovimientoCajaAsync(movimiento)))
        {
            var texto = $"{Concepto.Texto}: {Dinero.Formatear(monto)} registrado.";
            if (EsRetiro && CuentaDestino is not null) texto += $" También entró a tu cuenta \"{CuentaDestino.Nombre}\".";
            MontoMovimiento = DescripcionMovimiento = "";
            await CargarAsync();
            Aviso = texto;
        }
    }

    [RelayCommand]
    private async Task CerrarAsync()
    {
        if (!Dinero.TryLeer(EfectivoContado, out var contado) || contado < 0) { Falla("Escribe el efectivo que contaste"); return; }
        ResumenCaja? r = null;
        if (!await IntentarAsync(async () => r = await _sesion.Api.CerrarCajaAsync(contado))) return;
        var dif = r!.Sesion.Diferencia ?? 0;
        ResultadoCierre = dif switch
        {
            0 => $"Caja cerrada. El efectivo cuadra exacto: {Dinero.Formatear(contado)}.",
            < 0 => $"Caja cerrada con FALTANTE de {Dinero.Formatear(-dif)} (esperado {Dinero.Formatear(r.EfectivoEsperado)}, contado {Dinero.Formatear(contado)}).",
            _ => $"Caja cerrada con SOBRANTE de {Dinero.Formatear(dif)} (esperado {Dinero.Formatear(r.EfectivoEsperado)}, contado {Dinero.Formatear(contado)}).",
        };
        EfectivoContado = "";
        await CargarAsync();
    }
}
