using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Provision.Core.Api;
using Provision.Core.Configuracion;
using Provision.Desktop;
using Provision.Desktop.Servicios;
using Provision.Desktop.ViewModels;
using Provision.Desktop.Views;

internal static class Program
{
    [STAThread] public static void Main(string[] args) => AppBuilder.Configure<VisualApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime(args);
}
public class VisualApp : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var real = Environment.GetEnvironmentVariable("PROVISION_REVIEW_TOKEN") is { Length: > 0 };
            desktop.MainWindow = new Window { Title = real ? "Provisión · revisión ENAI local" : "Provisión · datos sintéticos", Width = 1440, Height = 900, Background = (Avalonia.Media.IBrush)Application.Current!.FindResource("Fondo")! };
            desktop.MainWindow.Opened += async (_, _) =>
            {
                var salida = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, real ? "capturas-enai" : "capturas"));
                Directory.CreateDirectory(salida);
                try
                {
            var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Equipo de pruebas", Rol = Rol.Admin, NombreRol = "Administrador · datos sintéticos", Permisos = ["ventas.crear", "ventas.ver", "ventas.anular", "caja.operar", "clientes.ver", "clientes.gestionar", "catalogo.ver", "catalogo.gestionar", "inventario.ver", "inventario.ajustar", "inventario.bajas", "laboratorio.ver", "reportes.ver", "auditoria.ver", "citas.gestionar", "tienda.gestionar", "usuarios.gestionar"] };
                    var api = real ? new ProvisionApi(new ApiClient("http://localhost:5080/api/optica/provision/v1", new SoloLectura())) : new ProvisionApi(new ApiClient("https://pruebas.invalid", new Simulador(usuario)));
                    if (real) { api.Http.Token = Environment.GetEnvironmentVariable("PROVISION_REVIEW_TOKEN"); usuario = await api.YoAsync(); }
                    Environment.SetEnvironmentVariable("PROVISION_REVIEW_TOKEN", null);
                    var shell = new ShellViewModel(new Sesion(real ? Entorno.Local : new Entorno("pruebas", "Datos sintéticos", "https://pruebas.invalid"), api, usuario), () => desktop.Shutdown());
                    Provision.Desktop.Controles.ImagenRemota.Cliente = api.Http;
                    desktop.MainWindow.Content = new ShellView { DataContext = shell };
                    var resultados = new List<string>();
                    foreach (var (ancho, alto) in new[] { (1440, 900), (1100, 900), (1100, 680) })
                    {
                        desktop.MainWindow.Width = ancho;
                        desktop.MainWindow.Height = alto;
                        foreach (var clave in shell.Menu.Select(x => x.Clave).Where(x => Environment.GetEnvironmentVariable("PROVISION_REVIEW_MODULES") is not { Length: > 0 } modulos || modulos.Split(',').Contains(x)).ToArray())
                        {
                            var comando = (CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)shell.Menu.First(x => x.Clave == clave).Seleccionar!;
                            await comando.ExecuteAsync(null);
                            if (!real && shell.SeccionActual is VenderViewModel venta && venta.Carrito.Count == 0 && venta.Resultados.Count > 0) venta.AgregarCommand.Execute(venta.Resultados.First());
                            if (real && shell.SeccionActual is ClientesViewModel clientes) clientes.Seleccionado = clientes.Clientes.FirstOrDefault();
                            if (real && shell.SeccionActual is UsuariosViewModel usuarios) usuarios.Seleccionado = usuarios.Usuarios.FirstOrDefault();
                            for (var espera = 0; shell.SeccionActual?.Ocupado == true && espera < 100; espera++) await Task.Delay(100);
                            await Task.Delay(650);
                            var visual = (Control)desktop.MainWindow.Content!;
                            if (clave == "vender")
                            {
                                var ayudas = visual.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == "Nombre o código + Enter");
                                if (ayudas.Any(t => t.Opacity < 1)) throw new InvalidOperationException("Las ayudas de entrada deben conservar su contraste sin transparencia.");
                                File.WriteAllLines(Path.Combine(salida, "contraste-ayudas.txt"), ayudas.SelectMany(t => new Visual[] { t }.Concat(t.GetVisualAncestors()).Select(a => $"{a.GetType().Name} {(a as StyledElement)?.Name} opacity={a.Opacity} foreground={(a as TextBlock)?.Foreground}")));
                            }
                            using var bitmap = new RenderTargetBitmap(new PixelSize((int)visual.Bounds.Width, (int)visual.Bounds.Height), new Vector(96, 96));
                            bitmap.Render(visual);
                            bitmap.Save(Path.Combine(salida, $"{clave}-{ancho}x{alto}.png"));
                            if (clave == "inventario" && ancho == 1100 && visual.GetVisualDescendants().OfType<InventarioView>().First().AnchoTarjeta > 320)
                                throw new InvalidOperationException("Inventario debe conservar dos tarjetas a 1100 px.");
                            if (real && clave == "clientes")
                            {
                                var tabs = visual.GetVisualDescendants().OfType<TabControl>().First();
                                for (var indice = 1; indice < 3; indice++)
                                {
                                    tabs.SelectedIndex = indice;
                                    await Task.Delay(200);
                                    bitmap.Render(visual);
                                    bitmap.Save(Path.Combine(salida, $"clientes-pestana{indice}-{ancho}x{alto}.png"));
                                }
                                tabs.SelectedIndex = 0;
                            }
                            resultados.Add($"{clave} {ancho}x{alto}: {shell.Error ?? shell.SeccionActual?.Error ?? "OK"}");
                        }
                    }
                    File.WriteAllLines(Path.Combine(salida, "resultado.txt"), resultados);
                    if (Environment.GetEnvironmentVariable("PROVISION_REVIEW_KEEP_OPEN") != "1") desktop.Shutdown();
                }
                catch (Exception ex) { File.WriteAllText(Path.Combine(salida, "error.txt"), ex.GetType().Name + ": " + ex.Message); desktop.Shutdown(); }
            };
        }
    }
}
internal sealed class Simulador(Usuario usuario) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        var path = r.RequestUri!.AbsolutePath;
        object datos = path switch
        {
            "/auth/me" => usuario,
            "/catalogo/productos" => new { datos = Enumerable.Range(1, 12).Select(i => new Producto { Id = new Guid(i, 0, 0, new byte[8]), Sku = $"OPT-{i:000}", Nombre = i % 2 == 0 ? "Montura rectangular de acetato" : "Montura ligera de titanio", PrecioVenta = 18500 + i * 1000, Stock = i, StockMinimo = 2, ControlaStock = true, Activo = true, VisibleTienda = true, Tipo = TipoProducto.Montura, Marca = "Colección de prueba" }), total = 12, pagina = 1, porPagina = 30 },
            "/catalogo/marcas" => Array.Empty<object>(),
            "/clientes" => new { datos = new[] { new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente sintético", Ci = "TEST-001", Telefono = "00000000" } }, total = 1, pagina = 1, porPagina = 100 },
            "/ventas" => new { datos = new[] { new VentaResumen { Id = Guid.NewGuid(), Numero = "V-000123", Cliente = "María Alejandra Cliente de prueba", Total = 125000, Pagado = 25000, Saldo = 100000, CreadoEn = DateTimeOffset.Now } }, total = 1, montoTotal = 125000 },
            "/ordenes-trabajo" => new[] { new OrdenTrabajoFila { Id = Guid.NewGuid(), Numero = "OT-000123", Cliente = "María Alejandra Cliente de prueba", Telefono = "00000000", VentaNumero = "V-000123", Saldo = 100000 } },
            "/panel" => new PanelRol { Nombre = "Equipo de pruebas", NombreRol = "Administrador", Proposito = "Gestiona la operación diaria de la óptica." },
            "/reportes/dashboard" => new Dashboard(),
            "/auditoria/resumen" => new ResumenAuditoria(),
            "/auditoria/eventos" => new ListaEventos(),
            "/pedidos-web" => new PaginaPedidosWeb(),
            "/citas" => Array.Empty<object>(),
            "/citas/config" => new ConfigCitas(),
            "/finanzas/cuentas" => new Cuentas(),
            "/finanzas/resumen" => new ResumenFinanzas(),
            "/finanzas/transacciones" or "/finanzas/categorias" => Array.Empty<object>(),
            "/caja/actual" => null!,
            "/sucursales" => Array.Empty<object>(),
            "/usuarios" => new[] { usuario },
            "/auth/roles" => Array.Empty<object>(),
            _ when path.Contains("kardex") => Array.Empty<object>(),
            _ => new { }
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(datos, ApiClient.Json), Encoding.UTF8, "application/json") });
    }
}

internal sealed class SoloLectura : DelegatingHandler
{
    public SoloLectura() : base(new HttpClientHandler()) { }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        if (r.Method != HttpMethod.Get) throw new InvalidOperationException("La revisión solo permite consultas GET.");
        return base.SendAsync(r, ct);
    }
}
