using Avalonia.Controls;
using Avalonia.Threading;
using Provision.Desktop.ViewModels;

namespace Provision.Desktop.Views;

public partial class ShellView : UserControl
{
    private readonly DispatcherTimer _sesion = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _revisando;

    public ShellView()
    {
        InitializeComponent();
        _sesion.Tick += async (_, _) =>
        {
            if (_revisando || DataContext is not ShellViewModel vm) return;
            _revisando = true;
            try { await vm.RefrescarSesionAsync(); }
            finally { _revisando = false; }
        };
        AttachedToVisualTree += (_, _) => _sesion.Start();
        DetachedFromVisualTree += (_, _) => _sesion.Stop();
    }
}
