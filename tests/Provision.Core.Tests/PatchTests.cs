using System.Text.Json;
using Provision.Core.Api;

namespace Provision.Core.Tests;

public class PatchTests
{
    [Fact]
    public void Campo_omitido_no_borra_datos_y_null_explicito_si_se_envia()
    {
        var cambio = new CambiosProducto { MarcaId = null, CodigoBarras = null, PrecioVenta = 0, VisibleTienda = false };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(cambio, ApiClient.Json));
        var root = json.RootElement;
        Assert.False(root.TryGetProperty("nombre", out _));
        Assert.False(root.TryGetProperty("modelo", out _));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("marcaId").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("codigoBarras").ValueKind);
        Assert.Equal(0, root.GetProperty("precioVenta").GetInt64());
        Assert.False(root.GetProperty("visibleTienda").GetBoolean());
    }

    [Fact]
    public void Actualizar_cliente_no_envia_campos_fuera_del_formulario()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new CambiosCliente { Nombre = "Ana", Ci = null, Telefono = "123" }, ApiClient.Json));
        Assert.Equal(3, json.RootElement.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("ci").ValueKind);
    }

    [Fact]
    public void Enum_y_guid_conservan_el_contrato_enai()
    {
        var id = Guid.NewGuid();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new CambiosProducto { Tipo = TipoProducto.LenteOftalmico, MarcaId = id }, ApiClient.Json));
        Assert.Equal("LENTE_OFTALMICO", json.RootElement.GetProperty("tipo").GetString());
        Assert.Equal(id, json.RootElement.GetProperty("marcaId").GetGuid());
    }
}
