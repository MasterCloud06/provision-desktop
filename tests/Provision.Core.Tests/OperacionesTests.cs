using System.Net;
using System.Text;
using System.Text.Json;
using Provision.Core.Api;

namespace Provision.Core.Tests;
public class OperacionesTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => responder(request);
    }
    [Fact]
    public async Task Conteo_baja_y_anulacion_respetan_rutas_y_cuerpos_de_enai()
    {
        var id = Guid.NewGuid();
        var rutas = new List<string>();
        using var http = new ApiClient("https://tests.invalid/api/optica/provision/v1", new Handler(async r =>
        {
            rutas.Add(r.RequestUri!.AbsolutePath);
            Assert.Equal(HttpMethod.Post, r.Method);
            using var cuerpo = JsonDocument.Parse(await r.Content!.ReadAsStringAsync());
            Assert.Equal("Motivo de prueba", cuerpo.RootElement.GetProperty("motivo").GetString());
            if (r.RequestUri.AbsolutePath.Contains("inventario"))
                Assert.Equal(id, cuerpo.RootElement.GetProperty("items")[0].GetProperty("productoId").GetGuid());
            if (r.RequestUri.AbsolutePath.EndsWith("ajustes"))
                Assert.Equal(0, cuerpo.RootElement.GetProperty("items")[0].GetProperty("cantidadContada").GetInt32());
            return new(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
        }));
        var api = new ProvisionApi(http);
        await api.RegistrarConteoAsync(id, 0, "Motivo de prueba");
        await api.RegistrarBajaAsync(id, 2, "Motivo de prueba");
        await api.AnularVentaAsync(id, "Motivo de prueba");
        Assert.Equal(3, rutas.Count);
        Assert.EndsWith("inventario/ajustes", rutas[0]);
        Assert.EndsWith("inventario/salidas", rutas[1]);
        Assert.EndsWith($"ventas/{id}/anular", rutas[2]);
    }

    [Fact]
    public async Task Ventas_recoge_paginas_y_conserva_total_y_monto()
    {
        var paginas = new List<int>();
        using var http = new ApiClient("https://tests.invalid", new Handler(r =>
        {
            var pagina = paginas.Count + 1;
            paginas.Add(pagina);
            Assert.Contains($"pagina={pagina}", r.RequestUri!.Query);
            var json = JsonSerializer.Serialize(new { datos = new[] { new { id = Guid.NewGuid() } }, total = 3, montoTotal = 60000, pagina, porPagina = 1 });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }));
        var resultado = await new ProvisionApi(http).VentasAsync();
        Assert.Equal(3, resultado.Datos.Count);
        Assert.Equal(3, resultado.Total);
        Assert.Equal(60000, resultado.MontoTotal);
    }
}
