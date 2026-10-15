using System.Globalization;
using Avalonia.Data.Converters;
using Provision.Core;

namespace Provision.Desktop.Conversores;

/// <summary>Muestra centavos como bolivianos: 15050 → "Bs 150,50".</summary>
public sealed class CentavosConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        long l => Dinero.Formatear(l),
        int i => Dinero.Formatear(i),
        null => "—",
        _ => value.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
