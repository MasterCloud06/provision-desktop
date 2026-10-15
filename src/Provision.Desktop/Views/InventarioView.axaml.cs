using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Provision.Desktop.ViewModels;

namespace Provision.Desktop.Views;

public partial class InventarioView : UserControl
{
    public InventarioView() => InitializeComponent();

    /// <summary>Abre el selector de archivos y pasa la foto elegida al formulario.</summary>
    private async void ElegirFoto_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InventarioViewModel vm || TopLevel.GetTopLevel(this) is not { } ventana) return;
        var archivos = await ventana.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Elegir foto del producto",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Fotos") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.heic", "*.heif"] },
            ],
        });
        if (archivos.Count == 0) return;
        try
        {
            await using var origen = await archivos[0].OpenReadAsync();
            using var memoria = new MemoryStream();
            await origen.CopyToAsync(memoria);
            vm.UsarFoto(memoria.ToArray(), archivos[0].Name);
        }
        catch (Exception ex)
        {
            vm.Error = $"No se pudo leer la foto: {ex.Message}";
        }
    }
}
