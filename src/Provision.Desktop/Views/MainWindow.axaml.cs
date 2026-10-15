using Avalonia;
using Avalonia.Controls;

namespace Provision.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools(); // F12 abre las herramientas de diagnóstico
#endif
    }
}
