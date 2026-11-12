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
    /// <summary>Series de días: el último (hoy) va en esmeralda pleno y los anteriores más suaves.</summary>
    public static readonly StyledProperty<bool> ResaltarUltimoProperty = AvaloniaProperty.Register<GraficoBarras, bool>(nameof(ResaltarUltimo));
    public bool ResaltarUltimo { get => GetValue(ResaltarUltimoProperty); set => SetValue(ResaltarUltimoProperty, value); }
    static GraficoBarras() => AffectsRender<GraficoBarras>(DatosProperty, ResaltarUltimoProperty);
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
            var fuerte = !ResaltarUltimo || i == datos.Count - 1;
            double ancho = Math.Min(slot * .6, 56), x = 76 + i * slot + (slot - ancho) / 2;
            c.DrawRectangle(new SolidColorBrush(Color.Parse(fuerte ? "#087F72" : "#9CCFC6")), null, new Rect(x, 14 + h - bh, ancho, bh), 5, 5);
            // El valor va sobre la barra (o dentro, si no cabe arriba): el gráfico se lee sin mirar el eje.
            if (datos[i].Valor > 0)
            {
                var valor = new FormattedText(datos[i].Valor.ToString("#,0", CultureInfo.GetCultureInfo("es-BO")), CultureInfo.GetCultureInfo("es-BO"), FlowDirection.LeftToRight,
                    new Typeface("fonts:Inter#Inter", FontStyle.Normal, FontWeight.SemiBold), 11, bh > h - 16 && fuerte ? Brushes.White : tinta);
                c.DrawText(valor, new Point(x + (ancho - valor.Width) / 2, bh > h - 16 ? 18 + h - bh : 14 + h - bh - 16));
            }
            var etiqueta = new FormattedText(datos[i].Etiqueta, CultureInfo.GetCultureInfo("es-BO"), FlowDirection.LeftToRight, new Typeface("fonts:Inter#Inter"), 11, tinta);
            c.DrawText(etiqueta, new Point(x + (ancho - etiqueta.Width) / 2, 22 + h));
        }
    }
}
