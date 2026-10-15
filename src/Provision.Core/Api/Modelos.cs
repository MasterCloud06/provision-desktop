// Modelos que viajan entre la app y la API. Todos los montos están en centavos (long).
using System.Text.Json.Serialization;

namespace Provision.Core.Api;

/* ─────────────────────────── Enumeraciones ─────────────────────────── */

/// <summary>Vendedor = ejecutivo de ventas. Los permisos de cada rol los define el backend.</summary>
public enum Rol { Admin, Encargado, Cajero, Vendedor, Optometrista, Laboratorio }

public enum TipoProducto { Montura, LenteOftalmico, LenteContacto, GafasSol, Accesorio, Liquido, Servicio }

public enum MetodoPago { Efectivo, Qr, Tarjeta, Transferencia }

public enum EstadoVenta { Activa, Anulada }

public enum EstadoPago { Pendiente, Parcial, Pagada }

public enum EstadoTrabajo { Pendiente, EnLaboratorio, Listo, Entregado, Cancelado }

public enum TipoMovimientoCaja { Ingreso, Egreso }

public enum ConceptoCaja { CobroVenta, Devolucion, Gasto, RetiroDueno, Deposito, Otro }

public enum TipoCuentaPersonal { Efectivo, Banco, BilleteraDigital, TarjetaCredito, Ahorro }

public enum TipoTransaccion { Ingreso, Gasto, Transferencia }

public enum TipoCategoria { Ingreso, Gasto }

/* ─────────────────────────── Comunes ─────────────────────────── */

public sealed record Pagina<T>
{
    public List<T> Datos { get; init; } = [];
    public int Total { get; init; }
    [JsonPropertyName("pagina")] public int NumeroPagina { get; init; }
    public int PorPagina { get; init; }
}

/* ─────────────────────────── Acceso ─────────────────────────── */

public sealed record Usuario
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public string Email { get; init; } = "";
    public Rol Rol { get; init; }
    public string NombreRol { get; init; } = "";
    public Guid? SucursalId { get; init; }
    public bool Activo { get; init; } = true;
    public List<string> Permisos { get; init; } = [];

    public bool Puede(string permiso) => Permisos.Contains(permiso);
}

public sealed record RolInfo
{
    public Rol Rol { get; init; }
    public string Nombre { get; init; } = "";
    public List<string> Permisos { get; init; } = [];

    public override string ToString() => Nombre;
}

public sealed record NuevoUsuario
{
    public string Nombre { get; init; } = "";
    public string Email { get; init; } = "";
    public string Password { get; init; } = "";
    public Rol Rol { get; init; }
    public Guid? SucursalId { get; init; }
}

/// <summary>Solo se envían los campos que no son null.</summary>
public sealed record CambiosUsuario
{
    public string? Nombre { get; init; }
    public string? Password { get; init; }
    public Rol? Rol { get; init; }
    public Guid? SucursalId { get; init; }
    public bool? Activo { get; init; }
}

public sealed record RespuestaLogin
{
    public string Token { get; init; } = "";
    public Usuario Usuario { get; init; } = new();
}

public sealed record Sucursal
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public string Codigo { get; init; } = "";
    public bool Activa { get; init; }
}

/* ─────────────────────────── Catálogo e inventario ─────────────────────────── */

public sealed record Marca
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public bool Activa { get; init; }
}

public sealed record Producto
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = "";
    public string? CodigoBarras { get; init; }
    public string Nombre { get; init; } = "";
    public TipoProducto Tipo { get; init; }
    public string? Modelo { get; init; }
    public Guid? MarcaId { get; init; }
    public string? Marca { get; init; }
    public long PrecioVenta { get; init; }
    public long CostoPromedio { get; init; }
    public int StockMinimo { get; init; }
    public bool ControlaStock { get; init; }
    public bool Activo { get; init; }
    public int Stock { get; init; }
    public string? Ubicacion { get; init; }

    public bool BajoStock => ControlaStock && Stock <= StockMinimo;
}

/// <summary>Edición de producto: solo se envían los campos que no son null.</summary>
public sealed record CambiosProducto
{
    public string? Sku { get; init; }
    public string? CodigoBarras { get; init; }
    public string? Nombre { get; init; }
    public TipoProducto? Tipo { get; init; }
    public Guid? MarcaId { get; init; }
    public string? Modelo { get; init; }
    public long? PrecioVenta { get; init; }
    public int? StockMinimo { get; init; }
    public bool? Activo { get; init; }
}

public sealed record ResultadoEliminar
{
    public bool Eliminado { get; init; }
    public bool Desactivado { get; init; }
    public string Mensaje { get; init; } = "";
}

public sealed record StockInicial(int Cantidad, long CostoUnitario);

public sealed record NuevoProducto
{
    public string Sku { get; init; } = "";
    public string? CodigoBarras { get; init; }
    public string Nombre { get; init; } = "";
    public TipoProducto Tipo { get; init; }
    public Guid? MarcaId { get; init; }
    public string? Modelo { get; init; }
    public long PrecioVenta { get; init; }
    public int StockMinimo { get; init; }
    public StockInicial? StockInicial { get; init; }
}

public sealed record LineaEntrada(Guid ProductoId, int Cantidad, long CostoUnitario);

public sealed record EntradaInventario
{
    public string? Documento { get; init; }
    public Guid? ProveedorId { get; init; }
    public List<LineaEntrada> Items { get; init; } = [];
}

public sealed record AlertaStock
{
    public Guid Id { get; init; }
    public string Sku { get; init; } = "";
    public string Nombre { get; init; } = "";
    public TipoProducto Tipo { get; init; }
    public int Stock { get; init; }
    public int StockMinimo { get; init; }
    public int Faltante { get; init; }
}

/* ─────────────────────────── Clientes ─────────────────────────── */

public sealed record Cliente
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public string? Ci { get; init; }
    public string? Nit { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }

    public string Descripcion => string.IsNullOrEmpty(Ci) ? Nombre : $"{Nombre} · CI {Ci}";
}

public sealed record Receta
{
    public Guid Id { get; init; }
    public DateOnly Fecha { get; init; }
    public string? Profesional { get; init; }
    public double? OdEsfera { get; init; }
    public double? OdCilindro { get; init; }
    public int? OdEje { get; init; }
    public double? OdAdicion { get; init; }
    public double? OiEsfera { get; init; }
    public double? OiCilindro { get; init; }
    public int? OiEje { get; init; }
    public double? OiAdicion { get; init; }
    public double? DnpOd { get; init; }
    public double? DnpOi { get; init; }
    public string? UsoRecomendado { get; init; }
    public string? Observaciones { get; init; }

    private static string Ojo(double? esf, double? cil, int? eje, double? add)
    {
        static string D(double? v) => v is null ? "—" : (v > 0 ? "+" : "") + v.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        var t = $"Esf {D(esf)}";
        if (cil is not null && cil != 0) t += $"  Cil {D(cil)}  Eje {eje}°";
        if (add is not null && add != 0) t += $"  Add {D(add)}";
        return t;
    }

    public string TextoOd => Ojo(OdEsfera, OdCilindro, OdEje, OdAdicion);
    public string TextoOi => Ojo(OiEsfera, OiCilindro, OiEje, OiAdicion);
    public string Resumen => $"{Fecha:dd/MM/yyyy} · {Profesional ?? "sin profesional"}";
}

public sealed record NuevaReceta
{
    public DateOnly Fecha { get; init; }
    public string? Profesional { get; init; }
    public double? OdEsfera { get; init; }
    public double? OdCilindro { get; init; }
    public int? OdEje { get; init; }
    public double? OdAdicion { get; init; }
    public double? OiEsfera { get; init; }
    public double? OiCilindro { get; init; }
    public int? OiEje { get; init; }
    public double? OiAdicion { get; init; }
    public double? DnpOd { get; init; }
    public double? DnpOi { get; init; }
    public string? UsoRecomendado { get; init; }
    public string? Observaciones { get; init; }
}

public sealed record CompraCliente
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = "";
    public long Total { get; init; }
    public long Saldo { get; init; }
    public EstadoVenta Estado { get; init; }
    public EstadoPago EstadoPago { get; init; }
    public DateTimeOffset CreadoEn { get; init; }
    public DateTime CreadoLocal => CreadoEn.LocalDateTime;
}

public sealed record ClienteDetalle
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public string? Ci { get; init; }
    public string? Nit { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
    public string? Notas { get; init; }
    public List<Receta> Recetas { get; init; } = [];
    public List<CompraCliente> Compras { get; init; } = [];
    public long SaldoPendiente { get; init; }
}

public sealed record NuevoCliente
{
    public string Nombre { get; init; } = "";
    public string? Ci { get; init; }
    public string? Telefono { get; init; }
}

/* ─────────────────────────── Caja ─────────────────────────── */

public sealed record SesionCaja
{
    public Guid Id { get; init; }
    public Guid SucursalId { get; init; }
    public string Estado { get; init; } = "";
    public DateTimeOffset AbiertaEn { get; init; }
    public DateTime AbiertaLocal => AbiertaEn.LocalDateTime;
    public long MontoApertura { get; init; }
    public DateTimeOffset? CerradaEn { get; init; }
    public long? EfectivoEsperado { get; init; }
    public long? EfectivoContado { get; init; }
    public long? Diferencia { get; init; }
}

public sealed record TotalesMetodo
{
    public long Ingresos { get; init; }
    public long Egresos { get; init; }
    public long Neto { get; init; }
}

public sealed record ResumenCaja
{
    public SesionCaja Sesion { get; init; } = new();
    public Dictionary<string, TotalesMetodo> PorMetodo { get; init; } = [];
    public Dictionary<string, long> PorConcepto { get; init; } = [];
    public long TotalIngresos { get; init; }
    public long TotalEgresos { get; init; }
    public long EfectivoEsperado { get; init; }
}

public sealed record MovimientoCaja
{
    public Guid Id { get; init; }
    public TipoMovimientoCaja Tipo { get; init; }
    public ConceptoCaja Concepto { get; init; }
    public MetodoPago Metodo { get; init; }
    public long Monto { get; init; }
    public string? Descripcion { get; init; }
    public string? VentaNumero { get; init; }
    public string? Usuario { get; init; }
    public DateTimeOffset CreadoEn { get; init; }
    public DateTime CreadoLocal => CreadoEn.LocalDateTime;
}

public sealed record DetalleSesionCaja
{
    public SesionCaja Sesion { get; init; } = new();
    public Dictionary<string, TotalesMetodo> PorMetodo { get; init; } = [];
    public long EfectivoEsperado { get; init; }
    public List<MovimientoCaja> Movimientos { get; init; } = [];
}

public sealed record NuevoMovimientoCaja
{
    public ConceptoCaja Concepto { get; init; }
    public TipoMovimientoCaja? Tipo { get; init; }
    public MetodoPago? Metodo { get; init; }
    public long Monto { get; init; }
    public string Descripcion { get; init; } = "";
    public Guid? CuentaPersonalId { get; init; }
}

/* ─────────────────────────── Ventas ─────────────────────────── */

public sealed record LineaVenta
{
    public Guid ProductoId { get; init; }
    public int Cantidad { get; init; }
    public long? Descuento { get; init; }
}

public sealed record Pago
{
    public MetodoPago Metodo { get; init; }
    public long Monto { get; init; }
    public string? Referencia { get; init; }
}

public sealed record DatosOrdenTrabajo
{
    public DateOnly? FechaPrometida { get; init; }
    public string? Instrucciones { get; init; }
}

public sealed record NuevaVenta
{
    public Guid? ClienteId { get; init; }
    public Guid? RecetaId { get; init; }
    public List<LineaVenta> Items { get; init; } = [];
    public long? Descuento { get; init; }
    public List<Pago> Pagos { get; init; } = [];
    public string? Notas { get; init; }
    public DatosOrdenTrabajo? OrdenTrabajo { get; init; }
}

public sealed record OrdenTrabajoResumen
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = "";
    public EstadoTrabajo Estado { get; init; }
}

public sealed record VentaRegistrada
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = "";
    public long Subtotal { get; init; }
    public long Descuento { get; init; }
    public long Total { get; init; }
    public long Pagado { get; init; }
    public long Saldo { get; init; }
    public EstadoPago EstadoPago { get; init; }
    public OrdenTrabajoResumen? OrdenTrabajo { get; init; }
}

public sealed record VentaResumen
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = "";
    public string? Cliente { get; init; }
    public string? Vendedor { get; init; }
    public EstadoVenta Estado { get; init; }
    public EstadoPago EstadoPago { get; init; }
    public long Total { get; init; }
    public long Pagado { get; init; }
    public long Saldo { get; init; }
    public DateTimeOffset CreadoEn { get; init; }
    public DateTime CreadoLocal => CreadoEn.LocalDateTime;
}

public sealed record ListaVentas
{
    public List<VentaResumen> Datos { get; init; } = [];
    public int Total { get; init; }
    public long MontoTotal { get; init; }
}

/* ─────────────────────────── Laboratorio ─────────────────────────── */

public sealed record OrdenTrabajoFila
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = "";
    public EstadoTrabajo Estado { get; init; }
    public DateOnly? FechaPrometida { get; init; }
    public string? LaboratorioExterno { get; init; }
    public Guid VentaId { get; init; }
    public string VentaNumero { get; init; } = "";
    public long Saldo { get; init; }
    public string? Cliente { get; init; }
    public string? Telefono { get; init; }
    public DateTimeOffset CreadoEn { get; init; }

    public bool Atrasada => FechaPrometida is DateOnly f && f < DateOnly.FromDateTime(DateTime.Today)
                            && Estado is EstadoTrabajo.Pendiente or EstadoTrabajo.EnLaboratorio;
    public string EstadoTexto => Enumeraciones.Etiqueta(Estado);
}

public sealed record ItemVenta
{
    public Guid ProductoId { get; init; }
    public string Descripcion { get; init; } = "";
    public int Cantidad { get; init; }
}

public sealed record HistorialTrabajo
{
    public EstadoTrabajo? EstadoAnterior { get; init; }
    public EstadoTrabajo EstadoNuevo { get; init; }
    public string? Nota { get; init; }
    public string? Usuario { get; init; }
    public DateTimeOffset CreadoEn { get; init; }
    public DateTime CreadoLocal => CreadoEn.LocalDateTime;
    public string Texto => Enumeraciones.Etiqueta(EstadoNuevo);
}

public sealed record OrdenTrabajoDetalle
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = "";
    public EstadoTrabajo Estado { get; init; }
    public DateOnly? FechaPrometida { get; init; }
    public string? Instrucciones { get; init; }
    public string? LaboratorioExterno { get; init; }
    public Receta? Receta { get; init; }
    public List<ItemVenta> Items { get; init; } = [];
    public List<HistorialTrabajo> Historial { get; init; } = [];
}

/* ─────────────────────────── Reportes ─────────────────────────── */

public sealed record ConteoMonto
{
    public long Ventas { get; init; }
    public long Monto { get; init; }
}

public sealed record VentasPeriodo
{
    public long Ventas { get; init; }
    public long Monto { get; init; }
    public long Cobrado { get; init; }
    public long Descuentos { get; init; }
    public long TicketPromedio { get; init; }
}

public sealed record MargenBruto
{
    public long Ingreso { get; init; }
    public long Costo { get; init; }
    public long Utilidad { get; init; }
    public int? Porcentaje { get; init; }
}

public sealed record ProductoTop
{
    public Guid ProductoId { get; init; }
    public string Producto { get; init; } = "";
    public long Unidades { get; init; }
    public long Monto { get; init; }
}

public sealed record Dashboard
{
    public ConteoMonto Hoy { get; init; } = new();
    public VentasPeriodo Ventas { get; init; } = new();
    public MargenBruto MargenBruto { get; init; } = new();
    public ConteoMonto CuentasPorCobrar { get; init; } = new();
    public Dictionary<string, long> OrdenesTrabajo { get; init; } = [];
    public long ProductosBajoStock { get; init; }
    public List<ProductoTop> TopProductos { get; init; } = [];
    public object? CajaAbierta { get; init; }
}

/* ─────────────────────────── Finanzas personales ─────────────────────────── */

public sealed record CuentaPersonal
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public TipoCuentaPersonal Tipo { get; init; }
    public bool Archivada { get; init; }
    public long SaldoInicial { get; init; }
    public long Saldo { get; init; }
}

public sealed record Cuentas
{
    [JsonPropertyName("cuentas")] public List<CuentaPersonal> Lista { get; init; } = [];
    public long Patrimonio { get; init; }
}

public sealed record NuevaCuenta(string Nombre, TipoCuentaPersonal Tipo, long SaldoInicial);

public sealed record CategoriaPersonal
{
    public Guid Id { get; init; }
    public string Nombre { get; init; } = "";
    public TipoCategoria Tipo { get; init; }
    public bool Archivada { get; init; }
}

public sealed record TransaccionPersonal
{
    public Guid Id { get; init; }
    public TipoTransaccion Tipo { get; init; }
    public long Monto { get; init; }
    public DateOnly Fecha { get; init; }
    public string? Descripcion { get; init; }
    public Guid CuentaId { get; init; }
    public Guid? CuentaDestinoId { get; init; }
    public string? Categoria { get; init; }
    public bool DesdeCaja { get; init; }
}

public sealed record NuevaTransaccion
{
    public TipoTransaccion Tipo { get; init; }
    public Guid CuentaId { get; init; }
    public Guid? CuentaDestinoId { get; init; }
    public Guid? CategoriaId { get; init; }
    public long Monto { get; init; }
    public DateOnly? Fecha { get; init; }
    public string? Descripcion { get; init; }
}

public sealed record GastoCategoria
{
    public Guid CategoriaId { get; init; }
    public string Categoria { get; init; } = "";
    public TipoCategoria Tipo { get; init; }
    public long Total { get; init; }
}

public sealed record LineaPresupuesto
{
    public Guid CategoriaId { get; init; }
    public string Categoria { get; init; } = "";
    public long Monto { get; init; }
    public long Gastado { get; init; }
    public long Disponible { get; init; }
    public int? Porcentaje { get; init; }
    public bool Excedido { get; init; }
}

public sealed record ResumenFinanzas
{
    public string Mes { get; init; } = "";
    public long Ingresos { get; init; }
    public long Gastos { get; init; }
    public long Ahorro { get; init; }
    public int? TasaAhorro { get; init; }
    public long IngresosDesdeNegocio { get; init; }
    public List<GastoCategoria> PorCategoria { get; init; } = [];
    public List<LineaPresupuesto> Presupuesto { get; init; } = [];
    public long Patrimonio { get; init; }
}
