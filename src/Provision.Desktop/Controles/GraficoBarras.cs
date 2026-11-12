using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Provision.Desktop.ViewModels;
namespace Provision.Desktop.Controles;
/// <summary>Gráfico nativo: valores reales, escala desde cero y etiquetas legibles.</summary>
public sealed class GraficoBarras : Control
{
    public static readonly StyledProperty<IReadOnlyList<PuntoSerie>?> DatosProperty = AvaloniaProperty.Register<GraficoBarras, IReadOnlyList<PuntoSerie>?>(nameof(Datos));
    public IReadOnlyList<PuntoSerie>? Datos { get => GetValue(DatosProperty); set => SetValue(DatosProperty, value); }
    static GraficoBarras() => AffectsRender<GraficoBarras>(DatosProperty);
    public override void Render(DrawingContext c)
    {
        base.Render(c);
        var datos = Datos;
        var tinta = new SolidColorBrush(Color.Parse("#4E5D69"));
        void Texto(string t, double x, double y, double size = 11) => c.DrawText(new FormattedText(t, CultureInfo.GetCultureInfo("es-BO"), FlowDirection.LeftToRight, new Typeface("fonts:Inter#Inter"), size, tinta), new Point(x,y));
        if (datos is null || datos.Count == 0 || datos.All(x => x.Valor == 0)) { Texto("Sin movimientos en este período", 12, 30, 13); return; }
        double w = Math.Max(1, Bounds.Width - 82), h = Math.Max(1, Bounds.Height - 48), max = Math.Max(1, datos.Max(x => x.Valor));
        for (int i = 0; i < 4; i++) {
            double y = 14 + h * i / 3;
            c.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#E5EAE7"))), new Point(76,y), new Point(Bounds.Width,y));
            Texto((max * (3-i)/3).ToString("0.#", CultureInfo.GetCultureInfo("es-BO")), 0, y - 6);
        }
        double slot = w / datos.Count;
        for (int i=0; i<datos.Count; i++) {
            double bh = Math.Clamp(datos[i].Valor / max, 0, 1) * h;
            c.DrawRectangle(new SolidColorBrush(Color.Parse("#087F72")), null, new Rect(76+i*slot+slot*.2,14+h-bh,slot*.6,bh),4,4);
            Texto(datos[i].Etiqueta,76+i*slot+2,22+h);
        }
    }
}
