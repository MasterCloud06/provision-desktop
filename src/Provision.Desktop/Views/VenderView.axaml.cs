using Avalonia.Controls;
using Avalonia.Input;

namespace Provision.Desktop.Views;

public partial class VenderView : UserControl
{
    public VenderView()
    {
        InitializeComponent();
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.F2) return;
            BuscarProducto.Focus();
            BuscarProducto.SelectAll();
            e.Handled = true;
        };
    }
}
