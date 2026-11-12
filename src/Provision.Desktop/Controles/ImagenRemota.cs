using Provision.Core.Api;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace Provision.Desktop.Controles;

/// <summary>
/// Imagen descargada desde ENAI con la sesion actual. Las listas virtualizadas solicitan
/// solo las filas visibles, sin una cache compartida que sobreviva al cierre de sesion.
/// </summary>
public sealed class ImagenRemota : Image
{
    public static readonly StyledProperty<string?> UrlProperty =
        AvaloniaProperty.Register<ImagenRemota, string?>(nameof(Url));

    public static readonly StyledProperty<int> AnchoDecodificadoProperty =
        AvaloniaProperty.Register<ImagenRemota, int>(nameof(AnchoDecodificado), 96);

    public static ApiClient? Cliente { get; set; }

    static ImagenRemota() => UrlProperty.Changed.AddClassHandler<ImagenRemota>((imagen, _) => imagen.Cargar());

    public string? Url
    {
        get => GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public int AnchoDecodificado
    {
        get => GetValue(AnchoDecodificadoProperty);
        set => SetValue(AnchoDecodificadoProperty, value);
    }

    private async void Cargar()
    {
        var url = Url;
        var anterior = Source as Bitmap;
        Source = null;
        anterior?.Dispose();
        if (url is null) return;
        var ancho = AnchoDecodificado;
        var bitmap = await DescargarAsync(url, ancho);
        if (url == Url) Source = bitmap;
        else bitmap?.Dispose();
    }

    /// <summary>Nunca lanza: sin foto o sin red, la fila muestra su hueco neutro.</summary>
    private static async Task<Bitmap?> DescargarAsync(string url, int ancho)
    {
        try
        {
            var cliente = Cliente;
            if (cliente is null) return null;
            var bytes = await cliente.DescargarFotoAsync(url);
            using var stream = new MemoryStream(bytes);
            return Bitmap.DecodeToWidth(stream, ancho);
        }
        catch
        {
            return null;
        }
    }
}
