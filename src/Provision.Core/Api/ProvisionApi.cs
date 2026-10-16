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

    public Task<List<RolInfo>> RolesAsync(CancellationToken ct = default) => Http.GetAsync<List<RolInfo>>("auth/roles", ct);

    /* Usuarios (administrador) */

    public Task<List<Usuario>> UsuariosAsync(CancellationToken ct = default) => Http.GetAsync<List<Usuario>>("usuarios", ct);

    public Task<Usuario> CrearUsuarioAsync(NuevoUsuario u, CancellationToken ct = default) =>
        Http.PostAsync<Usuario>("usuarios", u, ct);

    public Task<Usuario> ActualizarUsuarioAsync(Guid id, CambiosUsuario c, CancellationToken ct = default) =>
        Http.PatchAsync<Usuario>($"usuarios/{id}", c, ct);

    /* Catálogo e inventario */

    public Task<Pagina<Producto>> ProductosAsync(
        string? buscar = null, TipoProducto? tipo = null, bool? bajoStock = null, bool? activo = null,
        int pagina = 1, int porPagina = 100, CancellationToken ct = default) =>
        Http.GetAsync<Pagina<Producto>>("catalogo/productos" + Q(
            ("q", buscar), ("tipo", tipo is null ? null : Enumeraciones.ApiValor(tipo.Value)),
            ("bajoStock", bajoStock), ("activo", activo), ("pagina", pagina), ("porPagina", porPagina)), ct);

    public Task<Producto> CrearProductoAsync(NuevoProducto p, CancellationToken ct = default) =>
        Http.PostAsync<Producto>("catalogo/productos", p, ct);

    public Task<Producto> ActualizarProductoAsync(Guid id, CambiosProducto c, CancellationToken ct = default) =>
        Http.PatchAsync<Producto>($"catalogo/productos/{id}", c, ct);

    /// <summary>Borra el producto, o lo desactiva si ya tiene movimientos o ventas.</summary>
    public Task<ResultadoEliminar> EliminarProductoAsync(Guid id, CancellationToken ct = default) =>
        Http.EnviarDeleteAsync<ResultadoEliminar>($"catalogo/productos/{id}", ct);

    /// <summary>Sube (o reemplaza) la foto del producto. tipo: image/jpeg, image/png, image/webp o image/heic.</summary>
    public Task<Producto> SubirFotoAsync(Guid id, byte[] imagen, string tipo, CancellationToken ct = default) =>
        Http.PutArchivoAsync<Producto>($"catalogo/productos/{id}/foto", imagen, tipo, ct);

    public Task<Producto> QuitarFotoAsync(Guid id, CancellationToken ct = default) =>
        Http.EnviarDeleteAsync<Producto>($"catalogo/productos/{id}/foto", ct);

    /// <summary>Tipo de imagen según la extensión del archivo, o null si no es un formato aceptado.</summary>
    public static string? TipoDeFoto(string nombreArchivo) => Path.GetExtension(nombreArchivo).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".heic" or ".heif" => "image/heic",
        _ => null,
    };

    /// <summary>URL de una versión reducida de la foto (Cloudinary la genera al vuelo).</summary>
    public static string? Miniatura(string? url, int ancho = 600) =>
        url is not null && url.Contains("res.cloudinary.com") && url.Contains("/upload/")
            ? url.Replace("/upload/", $"/upload/c_limit,w_{ancho},f_jpg,q_auto/")
            : url;

    /// <summary>Última versión publicada de cada app (no requiere sesión).</summary>
    public Task<InfoActualizaciones> ActualizacionesAsync(CancellationToken ct = default) =>
        Http.GetAsync<InfoActualizaciones>("actualizaciones", ct);

    /* Agenda de citas */

    private static string Dia(DateOnly d) => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public Task<List<CitaAgenda>> CitasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default) =>
        Http.GetAsync<List<CitaAgenda>>("citas" + Q(("desde", Dia(desde)), ("hasta", Dia(hasta))), ct);

    public Task<ConfigCitas> ConfigCitasAsync(CancellationToken ct = default) => Http.GetAsync<ConfigCitas>("citas/config", ct);

    public Task<List<DiaAgenda>> DisponibilidadCitasAsync(DateOnly desde, int dias = 1, CancellationToken ct = default) =>
        Http.GetAsync<List<DiaAgenda>>("citas/disponibilidad" + Q(("desde", Dia(desde)), ("dias", dias)), ct);

    /// <summary>Cita tomada por el personal (teléfono o mostrador): queda confirmada.</summary>
    public Task<CitaAgenda> CrearCitaAsync(NuevaCita c, CancellationToken ct = default) => Http.PostAsync<CitaAgenda>("citas", c, ct);

    public Task<CitaAgenda> CambiarEstadoCitaAsync(Guid id, EstadoCita estado, string? nota = null, CancellationToken ct = default) =>
        Http.PostAsync<CitaAgenda>($"citas/{id}/estado", new { estado = Enumeraciones.ApiValor(estado), nota }, ct);

    /* Pedidos de la tienda web */

    /// <summary>estado: un valor de la API (PAGADO, LISTO…) o POR_ATENDER; null = todos.</summary>
    public Task<PaginaPedidosWeb> PedidosWebAsync(string? estado = null, string? buscar = null, int porPagina = 100, CancellationToken ct = default) =>
        Http.GetAsync<PaginaPedidosWeb>("pedidos-web" + Q(("estado", estado), ("q", buscar), ("porPagina", porPagina)), ct);

    public Task<PedidoWeb> PedidoWebAsync(Guid id, CancellationToken ct = default) =>
        Http.GetAsync<PedidoWeb>($"pedidos-web/{id}", ct);

    public Task<PedidoWeb> CambiarEstadoPedidoWebAsync(Guid id, CambioEstadoPedidoWeb c, CancellationToken ct = default) =>
        Http.PostAsync<PedidoWeb>($"pedidos-web/{id}/estado", c, ct);

    public Task<PedidoWeb> CancelarPedidoWebAsync(Guid id, string motivo, CancellationToken ct = default) =>
        Http.PostAsync<PedidoWeb>($"pedidos-web/{id}/cancelar", new { motivo }, ct);

    public Task<PedidoWeb> NotasPedidoWebAsync(Guid id, string notas, CancellationToken ct = default) =>
        Http.PostAsync<PedidoWeb>($"pedidos-web/{id}/notas", new { notasInternas = notas }, ct);

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

    public Task<ClienteDetalle> ClienteAsync(Guid id, CancellationToken ct = default) =>
        Http.GetAsync<ClienteDetalle>($"clientes/{id}", ct);

    public Task<Cliente> ActualizarClienteAsync(Guid id, NuevoCliente c, CancellationToken ct = default) =>
        Http.PatchAsync<Cliente>($"clientes/{id}", c, ct);

    public Task<Receta> CrearRecetaAsync(Guid clienteId, NuevaReceta r, CancellationToken ct = default) =>
        Http.PostAsync<Receta>($"clientes/{clienteId}/recetas", r, ct);

    /* Laboratorio */

    public Task<List<OrdenTrabajoFila>> OrdenesTrabajoAsync(EstadoTrabajo? estado = null, bool? atrasadas = null,
        CancellationToken ct = default) =>
        Http.GetAsync<List<OrdenTrabajoFila>>("ordenes-trabajo" + Q(
            ("estado", estado is null ? null : Enumeraciones.ApiValor(estado.Value)), ("atrasadas", atrasadas),
            ("porPagina", 200)), ct);

    public Task<OrdenTrabajoDetalle> OrdenTrabajoAsync(Guid id, CancellationToken ct = default) =>
        Http.GetAsync<OrdenTrabajoDetalle>($"ordenes-trabajo/{id}", ct);

    public Task CambiarEstadoTrabajoAsync(Guid id, EstadoTrabajo estado, string? nota = null, CancellationToken ct = default) =>
        Http.PostAsync<object>($"ordenes-trabajo/{id}/estado", new { estado, nota }, ct);

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

    /* Mi panel */

    /// <summary>Propósito del rol, responsabilidades, tareas del momento e indicadores del usuario.</summary>
    public Task<PanelRol> PanelAsync(CancellationToken ct = default) => Http.GetAsync<PanelRol>("panel", ct);

    /* Auditoría (super administrador) */

    private static string QAuditoria(FiltrosAuditoria f, params (string, object?)[] extra) => Q(
        new (string, object?)[]
        {
            ("desde", f.Desde?.ToString("yyyy-MM-dd")), ("hasta", f.Hasta?.ToString("yyyy-MM-dd")),
            ("rol", f.Rol is null ? null : Enumeraciones.ApiValor(f.Rol.Value)), ("usuarioId", f.UsuarioId),
            ("modulo", f.Modulo), ("resultado", f.Resultado is null ? null : Enumeraciones.ApiValor(f.Resultado.Value)),
            ("q", f.Buscar),
        }.Concat(extra).ToArray());

    public Task<ResumenAuditoria> ResumenAuditoriaAsync(FiltrosAuditoria f, CancellationToken ct = default) =>
        Http.GetAsync<ResumenAuditoria>("auditoria/resumen" + QAuditoria(f), ct);

    public Task<ListaEventos> EventosAsync(FiltrosAuditoria f, int pagina = 1, int porPagina = 100, CancellationToken ct = default) =>
        Http.GetAsync<ListaEventos>("auditoria/eventos" + QAuditoria(f, ("pagina", pagina), ("porPagina", porPagina)), ct);

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
