namespace Provision.Core.Api;

/// <summary>
/// Operaciones de la API de Provisión agrupadas por módulo. Es lo único que usan las pantallas.
/// </summary>
public sealed class ProvisionApi(ApiClient http)
{
    public ApiClient Http { get; } = http;

    private static string Q(params (string clave, object? valor)[] ps)
    {
        static string Texto(object v) => v switch
        {
            bool b => b ? "true" : "false",
            _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "",
        };
        var partes = ps
            .Where(p => p.valor is not null && Texto(p.valor) != "")
            .Select(p => $"{p.clave}={Uri.EscapeDataString(Texto(p.valor!))}");
        var q = string.Join('&', partes);
        return q.Length == 0 ? "" : "?" + q;
    }

    /* Acceso */

    public async Task<RespuestaLogin> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var r = await Http.PostAsync<RespuestaLogin>("auth/login", new { email, password }, ct);
        Http.Token = r.Token;
        return r;
    }

    public void CerrarSesion() => Http.Token = null;

    public Task<Usuario> YoAsync(CancellationToken ct = default) => Http.GetAsync<Usuario>("auth/me", ct);

    public Task<List<Sucursal>> SucursalesAsync(CancellationToken ct = default) =>
        Http.GetAsync<List<Sucursal>>("sucursales", ct);

    /* Catálogo e inventario */

    public Task<Pagina<Producto>> ProductosAsync(
        string? buscar = null, TipoProducto? tipo = null, bool? bajoStock = null, bool? activo = null,
        int pagina = 1, int porPagina = 100, CancellationToken ct = default) =>
        Http.GetAsync<Pagina<Producto>>("catalogo/productos" + Q(
            ("q", buscar), ("tipo", tipo is null ? null : Enumeraciones.ApiValor(tipo.Value)),
            ("bajoStock", bajoStock), ("activo", activo), ("pagina", pagina), ("porPagina", porPagina)), ct);

    public Task<Producto> CrearProductoAsync(NuevoProducto p, CancellationToken ct = default) =>
        Http.PostAsync<Producto>("catalogo/productos", p, ct);

    public Task<List<Marca>> MarcasAsync(CancellationToken ct = default) =>
        Http.GetAsync<List<Marca>>("catalogo/marcas", ct);

    public Task<Marca> CrearMarcaAsync(string nombre, CancellationToken ct = default) =>
        Http.PostAsync<Marca>("catalogo/marcas", new { nombre }, ct);

    public Task RegistrarEntradaAsync(EntradaInventario e, CancellationToken ct = default) =>
        Http.PostAsync<object>("inventario/entradas", e, ct);

    public Task<List<AlertaStock>> AlertasStockAsync(CancellationToken ct = default) =>
        Http.GetAsync<List<AlertaStock>>("inventario/alertas", ct);

    /* Clientes */

    public async Task<List<Cliente>> BuscarClientesAsync(string? q, CancellationToken ct = default) =>
        (await Http.GetAsync<Pagina<Cliente>>("clientes" + Q(("q", q), ("porPagina", 30)), ct)).Datos;

    public Task<Cliente> CrearClienteAsync(NuevoCliente c, CancellationToken ct = default) =>
        Http.PostAsync<Cliente>("clientes", c, ct);

    /* Caja */

    /// <summary>Resumen de la caja abierta, o null si está cerrada.</summary>
    public Task<ResumenCaja?> CajaActualAsync(CancellationToken ct = default) =>
        Http.GetAsync<ResumenCaja?>("caja/actual", ct);

    public Task<SesionCaja> AbrirCajaAsync(long montoApertura, string? observaciones = null, CancellationToken ct = default) =>
        Http.PostAsync<SesionCaja>("caja/abrir", new { montoApertura, observaciones }, ct);

    public Task<ResumenCaja> CerrarCajaAsync(long efectivoContado, string? observaciones = null, CancellationToken ct = default) =>
        Http.PostAsync<ResumenCaja>("caja/cerrar", new { efectivoContado, observaciones }, ct);

    public Task RegistrarMovimientoCajaAsync(NuevoMovimientoCaja m, CancellationToken ct = default) =>
        Http.PostAsync<object>("caja/movimientos", m, ct);

    public Task<DetalleSesionCaja> DetalleSesionAsync(Guid sesionId, CancellationToken ct = default) =>
        Http.GetAsync<DetalleSesionCaja>($"caja/sesiones/{sesionId}", ct);

    /* Ventas */

    public Task<VentaRegistrada> CrearVentaAsync(NuevaVenta v, CancellationToken ct = default) =>
        Http.PostAsync<VentaRegistrada>("ventas", v, ct);

    public Task<ListaVentas> VentasAsync(DateOnly? desde = null, DateOnly? hasta = null, bool? conSaldo = null,
        CancellationToken ct = default) =>
        Http.GetAsync<ListaVentas>("ventas" + Q(
            ("desde", desde?.ToString("yyyy-MM-dd")), ("hasta", hasta?.ToString("yyyy-MM-dd")),
            ("conSaldo", conSaldo), ("porPagina", 200)), ct);

    public Task RegistrarPagosAsync(Guid ventaId, IEnumerable<Pago> pagos, CancellationToken ct = default) =>
        Http.PostAsync<object>($"ventas/{ventaId}/pagos", new { pagos }, ct);

    /* Reportes */

    public Task<Dashboard> DashboardAsync(CancellationToken ct = default) =>
        Http.GetAsync<Dashboard>("reportes/dashboard", ct);

    /* Finanzas personales */

    public Task<Cuentas> CuentasAsync(CancellationToken ct = default) => Http.GetAsync<Cuentas>("finanzas/cuentas", ct);

    public Task<CuentaPersonal> CrearCuentaAsync(NuevaCuenta c, CancellationToken ct = default) =>
        Http.PostAsync<CuentaPersonal>("finanzas/cuentas", c, ct);

    public Task<List<CategoriaPersonal>> CategoriasAsync(CancellationToken ct = default) =>
        Http.GetAsync<List<CategoriaPersonal>>("finanzas/categorias", ct);

    public Task<List<TransaccionPersonal>> TransaccionesAsync(string mes, CancellationToken ct = default) =>
        Http.GetAsync<List<TransaccionPersonal>>("finanzas/transacciones" + Q(("mes", mes), ("porPagina", 200)), ct);

    public Task<TransaccionPersonal> CrearTransaccionAsync(NuevaTransaccion t, CancellationToken ct = default) =>
        Http.PostAsync<TransaccionPersonal>("finanzas/transacciones", t, ct);

    public Task<ResumenFinanzas> ResumenFinanzasAsync(string mes, CancellationToken ct = default) =>
        Http.GetAsync<ResumenFinanzas>("finanzas/resumen" + Q(("mes", mes)), ct);

    public Task GuardarPresupuestoAsync(Guid categoriaId, string mes, long monto, CancellationToken ct = default) =>
        Http.PutAsync<object>("finanzas/presupuestos", new { categoriaId, mes, monto }, ct);
}
