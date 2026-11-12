using System.Net;
using System.Text;
using Provision.Core.Api;
using Provision.Core.Configuracion;
using Provision.Desktop.Servicios;
using Provision.Desktop.ViewModels;

namespace Provision.Core.Tests;

public class RedisenioTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => responder(r);
    }
    private static HttpResponseMessage Json(string texto) => new(HttpStatusCode.OK) { Content = new StringContent(texto, Encoding.UTF8, "application/json") };
    private static Sesion CrearSesion(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) => new(Entorno.Local,
        new ProvisionApi(new ApiClient("https://tests.invalid", new Handler(responder))), new Usuario());

    [Fact]
    public void Serie_excluye_anuladas_agrupa_en_Bolivia_y_rellena_dias_sin_ventas()
    {
        var fin = new DateOnly(2026, 11, 11);
        var datos = new[] {
            new VentaResumen { Total = 12550, CreadoEn = DateTimeOffset.Parse("2026-11-11T02:30:00Z") },
            new VentaResumen { Total = 450, CreadoEn = DateTimeOffset.Parse("2026-11-10T15:00:00Z") },
            new VentaResumen { Total = 99999, Estado = EstadoVenta.Anulada, CreadoEn = DateTimeOffset.Parse("2026-11-10T15:00:00Z") },
            new VentaResumen { Total = 800, CreadoEn = DateTimeOffset.Parse("2026-11-12T15:00:00Z") }
        };
        var serie = SeriesVisuales.VentasDiarias(datos, fin);
        Assert.Equal(7, serie.Count);
        Assert.Equal("05/11", serie[0].Etiqueta);
        Assert.Equal(130d, serie[5].Valor);
        Assert.Equal(0d, serie[6].Valor);
        Assert.Equal(130d, serie.Sum(x => x.Valor));
    }

    [Fact]
    public void Busqueda_filtra_numero_y_cliente_sin_mutar_el_listado()
    {
        var vm = new VentasViewModel(CrearSesion(_ => throw new InvalidOperationException("No debe consultar"))) {
            Ventas = [new VentaResumen { Numero = "V-100", Cliente = "Ana Vargas" }, new VentaResumen { Numero = "V-200", Cliente = null }]
        };
        vm.Busqueda = "vargas";
        Assert.Equal("V-100", Assert.Single(vm.VentasVisibles).Numero);
        vm.Busqueda = "v-200";
        Assert.Equal("V-200", Assert.Single(vm.VentasVisibles).Numero);
        vm.Busqueda = " ";
        Assert.Equal(2, vm.VentasVisibles.Count);
        Assert.Equal(2, vm.Ventas.Count);
    }

    [Fact]
    public async Task Calendario_ignora_la_respuesta_del_dia_anterior()
    {
        var pendiente = new TaskCompletionSource<HttpResponseMessage>();
        var llamadas = 0;
        var vm = new AgendaViewModel(CrearSesion(r => {
            Assert.Equal(HttpMethod.Get, r.Method);
            if (r.RequestUri!.AbsolutePath.EndsWith("/config")) return Task.FromResult(Json("{}"));
            return ++llamadas == 1 ? pendiente.Task : Task.FromResult(Json("[]"));
        }));
        var primera = vm.ActivarAsync();
        vm.FechaCalendario = vm.FechaCalendario!.Value.AddDays(1);
        Assert.Equal(DateOnly.FromDateTime(vm.FechaCalendario.Value), vm.Dia);
        pendiente.SetResult(Json("[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"nombre\":\"Cita antigua\"}]"));
        await primera;
        Assert.Empty(vm.Citas);
        Assert.Null(vm.Error);
    }

    [Fact]
    public async Task Panel_sin_permisos_no_consulta_ventas_ni_agenda()
    {
        var rutas = new List<string>();
        var vm = new PanelViewModel(CrearSesion(r => {
            rutas.Add(r.RequestUri!.AbsolutePath);
            return Task.FromResult(Json("{}"));
        }), _ => Task.CompletedTask, _ => false);
        await vm.ActivarAsync();
        Assert.Equal(new[] { "/panel" }, rutas);
        Assert.False(vm.VerVentas);
        Assert.False(vm.VerAgenda);
        Assert.Empty(vm.Evolucion);
    }
    [Fact]
    public async Task Shell_muestra_sucursal_real_y_solo_modulos_permitidos()
    {
        Avalonia.Skia.SkiaPlatform.Initialize();
        var id = Guid.NewGuid();
        var usuario = new Usuario { Nombre = "Cuenta de prueba", SucursalId = id, Permisos = [] };
        var api = new ProvisionApi(new ApiClient("https://tests.invalid", new Handler(r => {
            Assert.Equal(HttpMethod.Get, r.Method);
            object datos = r.RequestUri!.AbsolutePath switch {
                "/auth/me" => usuario,
                "/sucursales" => new[] { new Sucursal { Id = id, Nombre = "Casa matriz" } },
                _ => new PanelRol()
            };
            return Task.FromResult(Json(System.Text.Json.JsonSerializer.Serialize(datos, ApiClient.Json)));
        })));
        var shell = new ShellViewModel(new Sesion(Entorno.Local, api, usuario), () => { });
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)shell.Menu[0].Seleccionar!).ExecuteAsync(null);
        Assert.Equal("Casa matriz", shell.SucursalActual);
        Assert.Equal(new[] { "panel", "finanzas" }, shell.Menu.Select(m => m.Clave));
        Assert.False(shell.PuedeVender);
        Assert.Equal("Mi panel", shell.TituloSeccion);
    }
}
