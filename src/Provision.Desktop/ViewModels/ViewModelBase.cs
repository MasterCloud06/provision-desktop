using CommunityToolkit.Mvvm.ComponentModel;
using Provision.Core.Api;

namespace Provision.Desktop.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool _ocupado;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string? _aviso;

    /// <summary>
    /// Ejecuta una operación contra la API mostrando el estado de carga y los errores
    /// en pantalla en vez de cerrar la app. Devuelve true si terminó bien.
    /// </summary>
    protected async Task<bool> IntentarAsync(Func<Task> accion, string? mensajeExito = null, string? mensajeEspera = null)
    {
        Error = null;
        Aviso = mensajeEspera;
        Ocupado = true;
        try
        {
            await accion();
            Aviso = mensajeExito;
            return true;
        }
        catch (ApiException ex)
        {
            Error = ex.MensajeCompleto;
            return false;
        }
        catch (Exception ex)
        {
            Error = "Error inesperado: " + ex.Message;
            return false;
        }
        finally
        {
            Ocupado = false;
        }
    }

    partial void OnOcupadoChanged(bool value) => AlCambiarOcupado(value);

    /// <summary>Para que las pantallas reactiven sus botones al terminar una operación.</summary>
    protected virtual void AlCambiarOcupado(bool ocupado) { }

    /// <summary>Muestra un error de validación local (antes de llamar a la API).</summary>
    protected bool Falla(string mensaje)
    {
        Aviso = null;
        Error = mensaje;
        return false;
    }
}
