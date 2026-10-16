using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Un paso que el pedido puede dar ahora (un botón por cada uno).</summary>
public sealed record PasoPedido(EstadoPedidoWeb Estado, string Texto, IAsyncRelayCommand Comando);

/// <summary>
/// Pedidos de la tienda web: prepararlos, avisar al cliente, entregarlos (cobrando en caja si paga al retirar)
/// o cancelarlos. Al entregar, el pedido queda registrado como venta.
/// </summary>
public sealed partial class PedidosWebViewModel : ViewModelBase, ISeccion
{
    private readonly Sesion _sesion;

    public PedidosWebViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _filtro = Filtros[0];
        _metodo = Metodos[0];
    }

    public IReadOnlyList<Opcion<string?>> Filtros { get; } =
    [
        new("POR_ATENDER", "Por atender"),
        new("PENDIENTE_PAGO", "Esperando pago"),
        new("ENTREGADO", "Entregados"),
        new("CANCELADO", "Cancelados"),
        new(null, "Todos"),
    ];

    public IReadOnlyList<Opcion<MetodoPago>> Metodos { get; } =
        Enum.GetValues<MetodoPago>().Select(m => new Opcion<MetodoPago>(m, Enumeraciones.Etiqueta(m))).ToList();

    public bool OperaCaja => _sesion.Puede("caja.operar");

    [ObservableProperty] private Opcion<string?> _filtro;
    [ObservableProperty] private string _busqueda = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinPedidos))]
    private IReadOnlyList<PedidoWebResumen> _pedidos = [];

    public bool SinPedidos => Pedidos.Count == 0;
    [ObservableProperty] private string _resumen = "";

    [ObservableProperty] private PedidoWebResumen? _seleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayDetalle), nameof(DireccionCompleta), nameof(PuedeCancelar), nameof(EntregaTexto),
        nameof(PagoTexto), nameof(MuestraCobro), nameof(CobroSinPermiso), nameof(PuedeAvisar), nameof(EstadoTexto), nameof(TieneEnvio))]
    private PedidoWeb? _detalle;

    [ObservableProperty] private IReadOnlyList<PasoPedido> _pasos = [];
    [ObservableProperty] private string _notasInternas = "";
    [ObservableProperty] private bool _confirmandoCancelar;
    [ObservableProperty] private string _motivoCancelacion = "";

    /* Cobro al entregar (pago al retirar) */
    [ObservableProperty] private Opcion<MetodoPago> _metodo;
    [ObservableProperty] private string _referencia = "";

    public bool HayDetalle => Detalle is not null;
    public string EstadoTexto => Detalle is null ? "" : Enumeraciones.Etiqueta(Detalle.Estado);
    public bool TieneEnvio => Detalle is { CostoEnvio: > 0 };
    public string EntregaTexto => Detalle is null ? "" : Detalle.Entrega == EntregaPedidoWeb.Retiro ? "Retira en la óptica" : "Envío a domicilio";
    public string PagoTexto => Detalle switch
    {
        null => "",
        { MetodoPago: MetodoPagoWeb.Stripe, PagadoEn: not null } => "Pagado con tarjeta en línea",
        { MetodoPago: MetodoPagoWeb.Stripe } => "Tarjeta en línea: el cliente todavía no pagó",
        { PagadoEn: not null } => "Cobrado en la óptica",
        _ => "Paga al retirar",
    };
    public string? DireccionCompleta => Detalle?.Direccion is { } d ? d + (Detalle.Ciudad is { } c ? $", {c}" : "") : null;
    public bool PuedeCancelar => Detalle is { Estado: not (EstadoPedidoWeb.Entregado or EstadoPedidoWeb.Cancelado) };
    /// <summary>Se muestra el cobro cuando el siguiente paso es entregar un pedido que se paga al retirar.</summary>
    public bool MuestraCobro => Detalle is { CobrarAlEntregar: true } d && d.Siguientes.Contains(EstadoPedidoWeb.Entregado) && OperaCaja;
    public bool CobroSinPermiso => Detalle is { CobrarAlEntregar: true } d && d.Siguientes.Contains(EstadoPedidoWeb.Entregado) && !OperaCaja;
    public bool PuedeAvisar => Detalle is { Estado: EstadoPedidoWeb.Listo or EstadoPedidoWeb.Enviado or EstadoPedidoWeb.Preparando or EstadoPedidoWeb.Pagado };

    public Task ActivarAsync() => CargarAsync();

    partial void OnFiltroChanged(Opcion<string?> value) => _ = CargarAsync();

    partial void OnSeleccionadoChanged(PedidoWebResumen? value)
    {
        ConfirmandoCancelar = false;
        MotivoCancelacion = "";
        Referencia = "";
        if (value is null) { Detalle = null; Pasos = []; return; }
        _ = CargarDetalleAsync(value.Id);
    }

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        var id = Seleccionado?.Id;
        var r = await _sesion.Api.PedidosWebAsync(Filtro.Valor, string.IsNullOrWhiteSpace(Busqueda) ? null : Busqueda.Trim());
        Pedidos = r.Datos;
        int C(string k) => r.Conteos.TryGetValue(k, out var n) ? n : 0;
        Resumen = $"{C("POR_ATENDER")} por atender · {C("PENDIENTE_PAGO")} esperando pago · {C("ENTREGADO")} entregados";
        Seleccionado = Pedidos.FirstOrDefault(p => p.Id == id);
        if (Seleccionado is not null) await CargarDetalleAsync(Seleccionado.Id);
    });

    private async Task CargarDetalleAsync(Guid id)
    {
        PedidoWeb? d = null;
        if (!await IntentarAsync(async () => d = await _sesion.Api.PedidoWebAsync(id))) return;
        if (Seleccionado?.Id != id) return;
        Detalle = d;
        NotasInternas = d!.NotasInternas ?? "";
        Pasos = d.Siguientes.Select(e => new PasoPedido(e, TextoPaso(e, d), new AsyncRelayCommand(() => AvanzarAsync(e)))).ToList();
    }

    private static string TextoPaso(EstadoPedidoWeb e, PedidoWeb p) => e switch
    {
        EstadoPedidoWeb.Preparando => "Empezar a preparar",
        EstadoPedidoWeb.Listo => "Marcar listo para retirar",
        EstadoPedidoWeb.Enviado => "Marcar como enviado",
        EstadoPedidoWeb.Entregado when p.CobrarAlEntregar => $"Cobrar {Dinero.Formatear(p.Total)} y entregar",
        EstadoPedidoWeb.Entregado => "Marcar como entregado",
        _ => Enumeraciones.Etiqueta(e),
    };

    private async Task AvanzarAsync(EstadoPedidoWeb estado)
    {
        if (Detalle is not { } p) return;
        List<Pago>? pagos = null;
        if (estado == EstadoPedidoWeb.Entregado && p.CobrarAlEntregar)
        {
            if (!OperaCaja) { Falla("Este pedido se paga al retirar: lo entrega quien opera la caja."); return; }
            pagos = [new Pago { Metodo = Metodo.Valor, Monto = p.Total, Referencia = string.IsNullOrWhiteSpace(Referencia) ? null : Referencia.Trim() }];
        }
        var ok = await IntentarAsync(() => _sesion.Api.CambiarEstadoPedidoWebAsync(p.Id, new CambioEstadoPedidoWeb { Estado = estado, Pagos = pagos }));
        if (!ok) return;
        var mensaje = estado switch
        {
            EstadoPedidoWeb.Entregado when pagos is not null => $"{p.Numero} cobrado ({Dinero.Formatear(p.Total)}) y entregado. Quedó registrado como venta.",
            EstadoPedidoWeb.Entregado => $"{p.Numero} entregado. Quedó registrado como venta.",
            EstadoPedidoWeb.Listo => $"{p.Numero} listo para retirar. Avísale al cliente.",
            _ => $"{p.Numero}: {Enumeraciones.Etiqueta(estado)}.",
        };
        await CargarAsync();
        Aviso = mensaje;
    }

    [RelayCommand]
    private async Task GuardarNotasAsync()
    {
        if (Detalle is not { } p) return;
        if (await IntentarAsync(() => _sesion.Api.NotasPedidoWebAsync(p.Id, NotasInternas.Trim()))) Aviso = "Notas guardadas.";
    }

    [RelayCommand]
    private void PedirCancelar() => ConfirmandoCancelar = true;

    [RelayCommand]
    private void NoCancelar() => ConfirmandoCancelar = false;

    [RelayCommand]
    private async Task CancelarPedidoAsync()
    {
        if (Detalle is not { } p) return;
        if (MotivoCancelacion.Trim().Length < 3) { Falla("Escribe el motivo de la cancelación"); return; }
        if (!await IntentarAsync(() => _sesion.Api.CancelarPedidoWebAsync(p.Id, MotivoCancelacion.Trim()))) return;
        var reembolso = p is { MetodoPago: MetodoPagoWeb.Stripe, PagadoEn: not null } ? " Se devolvió el pago a la tarjeta del cliente." : "";
        ConfirmandoCancelar = false;
        await CargarAsync();
        Aviso = $"{p.Numero} cancelado. El stock volvió al inventario.{reembolso}";
    }

    /// <summary>Abre WhatsApp con un mensaje listo para avisar al cliente.</summary>
    [RelayCommand]
    private void AvisarWhatsApp()
    {
        if (Detalle is not { } p) return;
        var numero = new string(p.Telefono.Where(char.IsDigit).ToArray());
        if (numero.Length == 8) numero = "591" + numero; // celular boliviano sin código de país
        if (numero.Length < 8) { Falla("El teléfono del pedido no parece un número de WhatsApp"); return; }
        var nombre = p.NombreContacto.Split(' ')[0];
        var texto = p.Estado switch
        {
            EstadoPedidoWeb.Listo => $"Hola {nombre}, tu pedido {p.Numero} ya está listo para retirar en la óptica." +
                                     (p.CobrarAlEntregar ? $" El total a pagar es {Dinero.Formatear(p.Total)}." : "") + " ¡Te esperamos!",
            EstadoPedidoWeb.Enviado => $"Hola {nombre}, tu pedido {p.Numero} ya va en camino a tu domicilio.",
            _ => $"Hola {nombre}, te escribimos de la óptica por tu pedido {p.Numero}.",
        };
        try
        {
            Process.Start(new ProcessStartInfo($"https://wa.me/{numero}?text={Uri.EscapeDataString(texto)}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Falla($"No se pudo abrir WhatsApp: {ex.Message}");
        }
    }
}
