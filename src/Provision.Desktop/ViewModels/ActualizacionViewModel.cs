using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Pantalla que bloquea la app hasta instalar la versión nueva.</summary>
public sealed partial class ActualizacionViewModel(VersionPublicada? ultima) : ViewModelBase
{
    public VersionPublicada? Ultima { get; } = ultima;

    public string Titulo => "Hay una versión nueva de Provisión";

    public string Detalle => Ultima is null
        ? "Para seguir trabajando tienes que actualizar la app."
        : $"Tienes la {Actualizador.VersionTexto.ToLowerInvariant()} y ya está publicada la 0.1.{Ultima.Compilacion}. " +
          "Para seguir trabajando tienes que actualizarla: toma menos de un minuto y no se pierde nada.";

    public string? Tamano => Ultima?.Tamano is { } t ? $"Descarga de {t / 1024d / 1024d:0} MB" : null;

    public bool PuedeReemplazarse => Actualizador.PuedeReemplazarse && Ultima is not null;
    public bool SoloNavegador => !PuedeReemplazarse && Ultima is not null;

    [ObservableProperty] private bool _descargando;
    [ObservableProperty] private double _progreso;
    [ObservableProperty] private string _estado = "";

    [RelayCommand]
    private async Task ActualizarAhoraAsync()
    {
        if (Ultima is null || Descargando) return;
        Descargando = true;
        Error = null;
        Estado = "Descargando la versión nueva…";
        try
        {
            await Actualizador.DescargarEInstalarAsync(Ultima.Url, new Progress<double>(p =>
            {
                Progreso = p * 100;
                Estado = $"Descargando… {p * 100:0}%";
            }));
            Estado = "Listo. Abriendo la versión nueva…";
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime vida) vida.Shutdown();
        }
        catch (Exception ex)
        {
            Error = $"No se pudo actualizar: {ex.Message}. Intenta de nuevo o descárgala desde el navegador.";
            Estado = "";
        }
        finally
        {
            Descargando = false;
        }
    }

    [RelayCommand]
    private void AbrirEnNavegador()
    {
        if (Ultima is null) return;
        try { Actualizador.AbrirEnNavegador(Ultima.Url); }
        catch (Exception ex) { Error = $"No se pudo abrir el navegador: {ex.Message}"; }
    }

    [RelayCommand]
    private void Salir()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime vida) vida.Shutdown();
    }
}
