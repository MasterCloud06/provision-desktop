using Provision.Core.Api;

namespace Provision.Desktop.ViewModels;

/// <summary>Un dato de la ficha: rótulo y valor ya formateado.</summary>
public sealed record Dato(string Etiqueta, string Valor);

/// <summary>
/// Un producto tal como lo muestran el catálogo, la lista y la ficha de Inventario: un solo estado por producto,
/// con color solo para lo que necesita atención (agotado, stock bajo).
/// </summary>
public sealed class FilaProducto(Producto producto)
{
    public Producto Producto { get; } = producto;

    public bool EsInactivo => !Producto.Activo;
    public bool EsAgotado => Producto is { Activo: true, ControlaStock: true, Stock: <= 0 };
    public bool EsBajo => Producto is { Activo: true, ControlaStock: true } && Producto.Stock > 0 && Producto.Stock <= Producto.StockMinimo;
    public bool EsDisponible => Producto.Activo && (!Producto.ControlaStock || Producto.Stock > 0);
    public bool EsNormal => !EsInactivo && !EsAgotado && !EsBajo;

    public string Estado => EsInactivo ? "Desactivado" : EsAgotado ? "Agotado" : EsBajo ? "Stock bajo" : "Disponible";

    public string TipoTexto => Enumeraciones.Etiqueta(Producto.Tipo);

    public string Marca => string.IsNullOrWhiteSpace(Producto.Marca) ? "—" : Producto.Marca;
    public bool TieneMarca => !string.IsNullOrWhiteSpace(Producto.Marca);

    /// <summary>Línea secundaria bajo el nombre en la lista: modelo y medidas, si las hay.</summary>
    public string? Detalle => Unir(Producto.Modelo, Producto.Medidas);

    /// <summary>Línea de la tarjeta: modelo y SKU.</summary>
    public string ModeloSku => Unir(Producto.Modelo, Producto.Sku) ?? Producto.Sku;

    public string StockTexto => !Producto.ControlaStock ? "Sin control de stock"
        : Producto.Stock == 1 ? "1 en stock" : $"{Producto.Stock} en stock";

    public string StockMinimoTexto => Producto.ControlaStock ? $"Mínimo {Producto.StockMinimo}" : "";

    public bool TieneFoto => Producto.ImagenUrl is not null;

    /// <summary>Miniatura chica de ENAI para la fila de la lista.</summary>
    public string? Miniatura => ProvisionApi.Miniatura(Producto.ImagenUrl, 96);

    /// <summary>Foto de la tarjeta del catálogo: suficiente para verse nítida a ~360 px en pantallas 1.5x.</summary>
    public string? Foto => ProvisionApi.Miniatura(Producto.ImagenUrl, 640);

    public bool OcultoEnTienda => Producto is { Activo: true, VisibleTienda: false };

    /// <summary>Características ópticas cargadas, en el orden en que se leen en la óptica. Las vacías no se muestran.</summary>
    public IReadOnlyList<Dato> Caracteristicas
    {
        get
        {
            var a = Producto.Atributos;
            var lista = new List<Dato>();
            void Agregar(string etiqueta, string? valor) { if (!string.IsNullOrWhiteSpace(valor)) lista.Add(new(etiqueta, valor)); }
            Agregar("Para quién", a.Genero switch { "UNISEX" => "Unisex", "MUJER" => "Mujer", "HOMBRE" => "Hombre", "NINO" => "Niños", _ => a.Genero });
            Agregar("Medidas", Producto.Medidas is { } m ? $"{m} mm" : null);
            Agregar("Forma", a.Forma);
            Agregar("Material", a.Material);
            Agregar("Color", a.Color);
            Agregar("Índice", a.Indice is { } i ? i.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : null);
            Agregar("Tratamiento", a.Tratamiento);
            return lista;
        }
    }

    public bool TieneCaracteristicas => Caracteristicas.Count > 0;

    private static string? Unir(params string?[] partes) =>
        string.Join(" · ", partes.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } d ? d : null;
}
