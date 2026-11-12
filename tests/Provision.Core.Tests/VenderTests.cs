using System.Net;
using System.Text;
using Provision.Core.Api;
using Provision.Core.Configuracion;
using Provision.Desktop.Servicios;
using Provision.Desktop.ViewModels;

namespace Provision.Core.Tests;

public class VenderTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => responder(request);
    }
    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    private static VenderViewModel Crear(Func<HttpRequestMessage, Task<HttpResponseMessage>>? responder = null)
    {
        var api = new ProvisionApi(new ApiClient("https://tests.invalid", new Handler(responder ?? (_ => Task.FromResult(Json("{}"))))));
        return new(new Sesion(Entorno.Local, api, new Usuario { Permisos = ["ventas.crear", "caja.operar"] }));
    }
    private static Producto Producto() => new() { Id = Guid.NewGuid(), Nombre = "Montura de prueba", PrecioVenta = 10000, Stock = 5, ControlaStock = true, Sku = "OPT-1" };

    [Fact]
    public void Recalcular_conserva_el_abono_escrito_y_rechaza_sobrepago()
    {
        var vm = Crear();
        vm.AgregarCommand.Execute(Producto());
        vm.MontoPagoTexto = "25";
        vm.Carrito[0].SumarCommand.Execute(null);
        Assert.Equal("25", vm.MontoPagoTexto);
        vm.AgregarPagoCommand.Execute(null);
        Assert.Equal(2500, vm.Pagado);
        vm.Cliente = new Cliente { Id = Guid.NewGuid() };
        vm.DescuentoTexto = "190";
        Assert.False(vm.CobrarCommand.CanExecute(null));
    }

    [Fact]
    public void Nueva_venta_exige_confirmar_si_hay_productos()
    {
        var vm = Crear();
        vm.AgregarCommand.Execute(Producto());
        vm.NuevaVentaCommand.Execute(null);
        Assert.True(vm.ConfirmandoNuevaVenta);
        Assert.Single(vm.Carrito);
        vm.ConfirmarNuevaVentaCommand.Execute(null);
        Assert.Empty(vm.Carrito);
    }

    [Fact]
    public async Task Carrito_no_cambia_mientras_el_servidor_registra_la_venta()
    {
        var respuesta = new TaskCompletionSource<HttpResponseMessage>();
        var vm = Crear(_ => respuesta.Task);
        vm.AgregarCommand.Execute(Producto());
        vm.AgregarPagoCommand.Execute(null);
        var tarea = vm.CobrarCommand.ExecuteAsync(null);
        vm.Carrito[0].SumarCommand.Execute(null);
        vm.Carrito[0].QuitarCommand.Execute(null);
        vm.AgregarCommand.Execute(Producto());
        Assert.Single(vm.Carrito);
        Assert.Equal(1, vm.Carrito[0].Cantidad);
        respuesta.SetResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("{}") });
        await tarea;
        Assert.Single(vm.Carrito);
        Assert.NotNull(vm.Error);
    }

    [Fact]
    public async Task Codigo_exacto_agrega_producto_y_paginacion_alcanza_el_resto()
    {
        var rutas = new List<string>();
        var id = Guid.NewGuid();
        var vm = Crear(r => { rutas.Add(r.RequestUri!.Query); return Task.FromResult(Json($$"""{"datos":[{"id":"{{id}}","sku":"OPT-1","nombre":"Montura","precioVenta":10000,"stock":5,"controlaStock":true}],"total":31,"pagina":1,"porPagina":30}""")); });
        vm.Busqueda = "OPT-1";
        await vm.EscanearCommand.ExecuteAsync(null);
        Assert.Single(vm.Carrito);
        Assert.Equal("", vm.Busqueda);
        Assert.True(vm.HaySiguiente);
        await vm.SiguienteCommand.ExecuteAsync(null);
        Assert.Contains("pagina=2", rutas.Last());
    }
}
