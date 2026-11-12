using System.Net;
using System.Text;
using Provision.Core.Api;
using Provision.Core.Configuracion;
using Provision.Desktop.Servicios;
using Provision.Desktop.ViewModels;

namespace Provision.Core.Tests;

public class RevisionTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => responder(r);
    }
    private static HttpResponseMessage Json(string texto) => new(HttpStatusCode.OK) { Content = new StringContent(texto, Encoding.UTF8, "application/json") };
    private static Sesion Sesion(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) => new(Entorno.Local,
        new ProvisionApi(new ApiClient("https://tests.invalid", new Handler(responder))), new Usuario { Permisos = ["catalogo.ver", "tienda.gestionar"] });

    [Fact]
    public async Task Pedido_pagado_con_tarjeta_no_se_cancela_sin_permiso_de_reembolso()
    {
        var llamadas = 0;
        var vm = new PedidosWebViewModel(Sesion(_ => { llamadas++; return Task.FromResult(Json("{}")); }))
        {
            Detalle = new PedidoWeb { Id = Guid.NewGuid(), Estado = EstadoPedidoWeb.Pagado, MetodoPago = MetodoPagoWeb.Stripe, PagadoEn = DateTimeOffset.UtcNow },
            MotivoCancelacion = "Cancelación de prueba"
        };
        vm.PedirCancelarCommand.Execute(null);
        await vm.CancelarPedidoCommand.ExecuteAsync(null);
        Assert.False(vm.ConfirmandoCancelar);
        Assert.Equal(0, llamadas);
    }

    [Fact]
    public async Task Inventario_ignora_una_respuesta_anterior_que_llega_despues()
    {
        var anterior = new TaskCompletionSource<HttpResponseMessage>();
        var llamadas = 0;
        var vm = new InventarioViewModel(Sesion(r =>
        {
            if (r.RequestUri!.AbsolutePath.EndsWith("marcas")) return Task.FromResult(Json("[]"));
            return ++llamadas == 1 ? anterior.Task : Task.FromResult(Json("{\"datos\":[{\"nombre\":\"Nuevo\",\"activo\":true}],\"total\":1}"));
        }));
        var primera = vm.ActivarAsync();
        await vm.ActivarAsync();
        anterior.SetResult(Json("{\"datos\":[{\"nombre\":\"Viejo\",\"activo\":true}],\"total\":1}"));
        await primera;
        Assert.Equal("Nuevo", Assert.Single(vm.Productos).Nombre);
        Assert.False(vm.Ocupado);
    }
}
