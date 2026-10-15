// Prueba de humo de Provision.Core contra una API real (úsala en LOCAL, crea datos).
//   dotnet run --project tools/Provision.Core.Pruebas -- <urlApi> <email> <password>
using Provision.Core;
using Provision.Core.Api;

var url = args.ElementAtOrDefault(0) ?? "http://localhost:3000/api/v1";
var email = args.ElementAtOrDefault(1) ?? "admin@provision.local";
var password = args.ElementAtOrDefault(2) ?? "CambiaEsto123";

var fallos = 0;
void Comprobar(bool ok, string que)
{
    Console.WriteLine($"{(ok ? "  ✓" : "  ✗")} {que}");
    if (!ok) fallos++;
}

Console.WriteLine("Dinero");
Comprobar(Dinero.Formatear(15050) == "Bs 150,50", "15050 → Bs 150,50");
Comprobar(Dinero.Formatear(123456789) == "Bs 1.234.567,89", "miles con punto");
Comprobar(Dinero.Formatear(-500) == "-Bs 5,00", "negativos");
foreach (var (texto, esperado) in new[] { ("150", 15000L), ("150,5", 15050L), ("150.50", 15050L), ("1.234,50", 123450L), ("1,234.50", 123450L), ("1.234", 123400L), ("Bs 20", 2000L) })
    Comprobar(Dinero.TryLeer(texto, out var c) && c == esperado, $"leer \"{texto}\" → {esperado}");
Comprobar(!Dinero.TryLeer("abc", out _), "rechaza texto no numérico");

Console.WriteLine($"API {url}");
using var http = new ApiClient(url);
var api = new ProvisionApi(http);

try
{
    await api.LoginAsync(email, "clave-equivocada");
    Comprobar(false, "login con clave equivocada falla");
}
catch (ApiException ex) { Comprobar(ex.Status == 401, $"login con clave equivocada → 401 ({ex.Message})"); }

var sesion = await api.LoginAsync(email, password);
Comprobar(sesion.Usuario.Rol == Rol.Superadmin, $"login como {sesion.Usuario.Nombre} ({sesion.Usuario.Rol})");

var sufijo = DateTime.Now.ToString("HHmmss");
var marca = await api.CrearMarcaAsync("Marca prueba " + sufijo);
var producto = await api.CrearProductoAsync(new NuevoProducto
{
    Sku = "PRB-" + sufijo, Nombre = "Montura de prueba " + sufijo, Tipo = TipoProducto.Montura,
    MarcaId = marca.Id, PrecioVenta = 50000, StockMinimo = 1, StockInicial = new StockInicial(3, 20000),
});
Comprobar(producto.Tipo == TipoProducto.Montura, "crear producto con stock inicial (enum MONTURA)");
var lente = await api.CrearProductoAsync(new NuevoProducto
{
    Sku = "LEN-" + sufijo, Nombre = "Lente de prueba " + sufijo, Tipo = TipoProducto.LenteOftalmico,
    PrecioVenta = 30000, StockInicial = new StockInicial(5, 10000),
});
Comprobar(lente.Tipo == TipoProducto.LenteOftalmico, "enum LENTE_OFTALMICO ida y vuelta");

var lista = await api.ProductosAsync(buscar: sufijo);
Comprobar(lista.Total == 2 && lista.Datos.Any(p => p.Stock == 3), $"buscar productos ({lista.Total}) con stock");

await api.RegistrarEntradaAsync(new EntradaInventario { Documento = "F-" + sufijo, Items = [new(producto.Id, 2, 26000)] });
var tras = (await api.ProductosAsync(buscar: "PRB-" + sufijo)).Datos.Single();
Comprobar(tras.Stock == 5 && tras.CostoPromedio == 22400, $"entrada: stock {tras.Stock}, costo prom. {Dinero.Formatear(tras.CostoPromedio)}");

var caja = await api.CajaActualAsync();
if (caja is not null) await api.CerrarCajaAsync(caja.EfectivoEsperado);
Comprobar(await api.CajaActualAsync() is null, "caja cerrada devuelve null");
await api.AbrirCajaAsync(10000);
Comprobar((await api.CajaActualAsync())?.EfectivoEsperado == 10000, "abrir caja con Bs 100");

var cliente = await api.CrearClienteAsync(new NuevoCliente { Nombre = "Cliente prueba " + sufijo, Ci = sufijo });
Comprobar((await api.BuscarClientesAsync(sufijo)).Count == 1, "crear y buscar cliente");

var venta = await api.CrearVentaAsync(new NuevaVenta
{
    ClienteId = cliente.Id,
    Items = [new() { ProductoId = producto.Id, Cantidad = 1 }, new() { ProductoId = lente.Id, Cantidad = 1 }],
    Descuento = 5000,
    Pagos = [new() { Metodo = MetodoPago.Efectivo, Monto = 30000 }, new() { Metodo = MetodoPago.Qr, Monto = 10000, Referencia = "QR1" }],
    OrdenTrabajo = new DatosOrdenTrabajo { FechaPrometida = DateOnly.FromDateTime(DateTime.Today.AddDays(3)) },
});
Comprobar(venta.Total == 75000 && venta.Saldo == 35000 && venta.EstadoPago == EstadoPago.Parcial,
    $"venta {venta.Numero}: total {Dinero.Formatear(venta.Total)}, saldo {Dinero.Formatear(venta.Saldo)}");
Comprobar(venta.OrdenTrabajo?.Estado == EstadoTrabajo.Pendiente, "orden de trabajo creada (DateOnly enviado)");

try
{
    await api.CrearVentaAsync(new NuevaVenta { Items = [] });
    Comprobar(false, "venta vacía rechazada");
}
catch (ApiException ex) { Comprobar(ex.Codigo == "VALIDACION" && ex.MensajeCompleto.Contains("items"), "error de validación legible: " + ex.MensajeCompleto.Replace(Environment.NewLine, " ")); }

await api.RegistrarPagosAsync(venta.Id, [new Pago { Metodo = MetodoPago.Tarjeta, Monto = 35000 }]);
var conSaldo = await api.VentasAsync(conSaldo: true);
Comprobar(conSaldo.Datos.All(v => v.Id != venta.Id), "abono deja la venta sin saldo");

var cuentas = await api.CuentasAsync();
var billetera = cuentas.Lista.FirstOrDefault(c => c.Nombre == "Billetera prueba")
    ?? await api.CrearCuentaAsync(new NuevaCuenta("Billetera prueba", TipoCuentaPersonal.Efectivo, 0));
await api.RegistrarMovimientoCajaAsync(new NuevoMovimientoCaja
{
    Concepto = ConceptoCaja.RetiroDueno, Monto = 5000, Descripcion = "Retiro de prueba", CuentaPersonalId = billetera.Id,
});
var resumen = await api.CajaActualAsync();
Comprobar(resumen!.EfectivoEsperado == 10000 + 30000 - 5000, $"efectivo esperado {Dinero.Formatear(resumen.EfectivoEsperado)}");
Comprobar(resumen.PorMetodo["QR"].Ingresos == 10000, "totales por método (QR)");

var detalle = await api.DetalleSesionAsync(resumen.Sesion.Id);
Comprobar(detalle.Movimientos.Any(m => m.Concepto == ConceptoCaja.RetiroDueno), "movimientos de caja con concepto RETIRO_DUENO");

var mes = DateTime.Today.ToString("yyyy-MM");
var categorias = await api.CategoriasAsync();
var comida = categorias.First(c => c.Tipo == TipoCategoria.Gasto && c.Nombre == "Alimentación");
await api.CrearTransaccionAsync(new NuevaTransaccion
{
    Tipo = TipoTransaccion.Gasto, CuentaId = billetera.Id, CategoriaId = comida.Id, Monto = 1500, Descripcion = "Almuerzo",
});
await api.GuardarPresupuestoAsync(comida.Id, mes, 100000);
var fin = await api.ResumenFinanzasAsync(mes);
Comprobar(fin.IngresosDesdeNegocio >= 5000 && fin.Presupuesto.Any(p => p.Categoria == "Alimentación"), $"finanzas: ingresos {Dinero.Formatear(fin.Ingresos)}, gastos {Dinero.Formatear(fin.Gastos)}");
Comprobar((await api.TransaccionesAsync(mes)).Any(t => t.DesdeCaja), "transacciones con fecha DateOnly");
Comprobar((await api.CuentasAsync()).Lista.Any(c => c.Id == billetera.Id), "lista de cuentas con saldo");

var dash = await api.DashboardAsync();
Comprobar(dash.Ventas.Monto >= 75000 && dash.TopProductos.Count > 0, $"dashboard: {dash.Ventas.Ventas} ventas, {Dinero.Formatear(dash.Ventas.Monto)}");

Console.WriteLine("Roles, productos, clientes y laboratorio");
Comprobar(sesion.Usuario.Puede("auditoria.ver") && sesion.Usuario.NombreRol == "Super administrador", "login trae nombre de rol y permisos");
var roles = await api.RolesAsync();
Comprobar(roles.Count == 7 && roles.All(r => r.Asignable) && roles.Any(r => r.Rol == Rol.Cajero && r.Nombre == "Cajero"), "lista de roles (enum CAJERO, todos asignables por el super admin)");
var cajeroEmail = $"cajero{sufijo}@prueba.bo";
var cajero = await api.CrearUsuarioAsync(new NuevoUsuario
{
    Nombre = "Cajero prueba", Email = cajeroEmail, Password = "clave12345", Rol = Rol.Cajero, SucursalId = sesion.Usuario.SucursalId,
});
Comprobar(cajero.Rol == Rol.Cajero, "crear usuario cajero");
using (var httpCajero = new ApiClient(url))
{
    var apiCajero = new ProvisionApi(httpCajero);
    var sc = await apiCajero.LoginAsync(cajeroEmail, "clave12345");
    Comprobar(sc.Usuario.Puede("caja.operar") && !sc.Usuario.Puede("ventas.anular"), "permisos del cajero");
    try { await apiCajero.DashboardAsync(); Comprobar(false, "cajero sin acceso a reportes"); }
    catch (ApiException ex) { Comprobar(ex.Status == 403, "cajero sin acceso a reportes (403)"); }
}
var cambiado = await api.ActualizarUsuarioAsync(cajero.Id, new CambiosUsuario { Rol = Rol.Vendedor, Activo = false });
Comprobar(cambiado.Rol == Rol.Vendedor && !cambiado.Activo, "cambiar rol y desactivar (solo campos enviados)");
Comprobar((await api.UsuariosAsync()).Any(x => x.Id == cajero.Id && x.NombreRol == "Ejecutivo de ventas"), "lista de usuarios con nombre de rol");

var editado = await api.ActualizarProductoAsync(producto.Id, new CambiosProducto { PrecioVenta = 55000 });
Comprobar(editado.PrecioVenta == 55000 && editado.Nombre == producto.Nombre, "editar solo el precio de un producto");
var temporal = await api.CrearProductoAsync(new NuevoProducto { Sku = "TMP-" + sufijo, Nombre = "Temporal " + sufijo, Tipo = TipoProducto.Accesorio, PrecioVenta = 100 });
Comprobar((await api.EliminarProductoAsync(temporal.Id)).Eliminado, "eliminar producto sin historial");
var desactivado = await api.EliminarProductoAsync(producto.Id);
Comprobar(!desactivado.Eliminado && desactivado.Desactivado, "producto con historial se desactiva: " + desactivado.Mensaje);

var receta = await api.CrearRecetaAsync(cliente.Id, new NuevaReceta
{
    Fecha = DateOnly.FromDateTime(DateTime.Today), Profesional = "Lic. Prueba", OdEsfera = -1.25, OdCilindro = -0.5, OdEje = 90, OiEsfera = -1,
});
var ficha = await api.ClienteAsync(cliente.Id);
Comprobar(ficha.Recetas.Count == 1 && ficha.Compras.Count == 1 && ficha.Recetas[0].TextoOd.Contains("Eje 90"), $"ficha del cliente: receta OD {ficha.Recetas[0].TextoOd}");

var ordenes = await api.OrdenesTrabajoAsync();
var orden = ordenes.Single(o => o.Id == venta.OrdenTrabajo!.Id);
Comprobar(orden.Estado == EstadoTrabajo.Pendiente && orden.VentaNumero == venta.Numero, "tablero de laboratorio");
await api.CambiarEstadoTrabajoAsync(orden.Id, EstadoTrabajo.EnLaboratorio, "Enviado");
await api.CambiarEstadoTrabajoAsync(orden.Id, EstadoTrabajo.Listo);
await api.CambiarEstadoTrabajoAsync(orden.Id, EstadoTrabajo.Entregado);
var detalleOt = await api.OrdenTrabajoAsync(orden.Id);
Comprobar(detalleOt.Estado == EstadoTrabajo.Entregado && detalleOt.Historial.Count == 4 && detalleOt.Items.Count == 2,
    "orden: EN_LABORATORIO → LISTO → ENTREGADO con historial");

Console.WriteLine("Mi panel");
var miPanel = await api.PanelAsync();
Comprobar(miPanel.Rol == Rol.Superadmin && miPanel.Proposito.Length > 40 && miPanel.Responsabilidades.Count >= 3,
    $"panel del super administrador: {miPanel.Tareas.Count} tareas, {miPanel.Indicadores.Count} indicadores");
Comprobar(miPanel.Tareas.All(t => Enum.IsDefined(t.Urgencia)) && miPanel.Tareas.Any(t => t.Pantalla is not null || t.Urgencia == Urgencia.Ok),
    "tareas con urgencia (enum) y pantalla destino");
Comprobar((await api.RolesAsync()).All(r => r.Proposito.Length > 20), "cada rol trae su propósito");

Console.WriteLine("Auditoría");
var hoy = DateOnly.FromDateTime(DateTime.Today);
var aud = await api.ResumenAuditoriaAsync(new FiltrosAuditoria { Desde = hoy.AddDays(-6), Hasta = hoy });
Comprobar(aud.PorRol.Count == 7 && aud.PorRol.Any(r => r.Rol == Rol.Superadmin && r.Eventos > 0), $"resumen: {aud.Totales.Eventos} eventos, {aud.Totales.Usuarios} usuarios");
Comprobar(aud.PorModulo.Any(m => m.Modulo == "ventas") && aud.PorDia.Count >= 1, "actividad por módulo y por día");
var evCajero = await api.EventosAsync(new FiltrosAuditoria { Rol = Rol.Cajero });
Comprobar(evCajero.Datos.All(e => e.Rol == Rol.Cajero), $"filtro por rol cajero ({evCajero.Total} eventos)");
var denegados = await api.EventosAsync(new FiltrosAuditoria { Resultado = ResultadoEvento.Denegado });
Comprobar(denegados.Datos.Any(e => e.Descripcion.Contains("reportes") || e.Resultado == ResultadoEvento.Denegado) || denegados.Total == 0, $"filtro por resultado DENEGADO ({denegados.Total})");
var ventasEv = await api.EventosAsync(new FiltrosAuditoria { Modulo = "ventas", Buscar = venta.Numero });
Comprobar(ventasEv.Datos.Any(e => e.Descripcion.StartsWith("Registró la venta " + venta.Numero)), "búsqueda de la venta en el registro");

var cierre = await api.CerrarCajaAsync(resumen.EfectivoEsperado - 100);
Comprobar(cierre.Sesion.Diferencia == -100, "cierre de caja con faltante de Bs 1");

Console.WriteLine(fallos == 0 ? "\nTodo bien." : $"\n{fallos} comprobaciones fallaron.");
return fallos == 0 ? 0 : 1;
