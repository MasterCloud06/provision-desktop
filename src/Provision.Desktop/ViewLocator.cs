using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Provision.Desktop.ViewModels;

namespace Provision.Desktop;

/// <summary>
/// Muestra cada ViewModel con su vista por convención de nombres:
/// Provision.Desktop.ViewModels.CajaViewModel → Provision.Desktop.Views.CajaView.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;
        var nombre = data.GetType().FullName!.Replace(".ViewModels.", ".Views.").Replace("ViewModel", "View");
        var tipo = Type.GetType(nombre);
        return tipo is not null
            ? (Control)Activator.CreateInstance(tipo)!
            : new TextBlock { Text = "Vista no encontrada: " + nombre };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
