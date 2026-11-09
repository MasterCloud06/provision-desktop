using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Provision.Desktop.Controles;

/// <summary>
/// Icono de trazo dibujado sobre una grilla de 24×24 (como Lucide). Toma el color del texto que lo rodea, así un
/// botón cambia el color de su icono junto con el del texto (hover, activo, deshabilitado).
/// </summary>
public sealed class Icono : Control
{
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<Icono, Geometry?>(nameof(Data));

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Icono>();

    public static readonly StyledProperty<double> GrosorProperty =
        AvaloniaProperty.Register<Icono, double>(nameof(Grosor), 1.8);

    static Icono()
    {
        AffectsRender<Icono>(DataProperty, ForegroundProperty, GrosorProperty);
        WidthProperty.OverrideDefaultValue<Icono>(18);
        HeightProperty.OverrideDefaultValue<Icono>(18);
        // Decorativo: el nombre accesible lo lleva el botón que lo contiene.
        IsHitTestVisibleProperty.OverrideDefaultValue<Icono>(false);
    }

    public Geometry? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double Grosor
    {
        get => GetValue(GrosorProperty);
        set => SetValue(GrosorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Data is null || Foreground is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var escala = Math.Min(Bounds.Width, Bounds.Height) / 24;
        var pluma = new Pen(Foreground, Grosor, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(escala, escala)))
        {
            context.DrawGeometry(null, pluma, Data);
        }
    }
}
