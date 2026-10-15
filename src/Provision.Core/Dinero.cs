using System.Globalization;

namespace Provision.Core;

/// <summary>
/// La API maneja montos en centavos enteros (Bs 150,50 = 15050) para no perder precisión.
/// Aquí se convierten para mostrarlos y para leer lo que escribe el usuario.
/// </summary>
public static class Dinero
{
    /// <summary>15050 → "Bs 150,50"; 123456789 → "Bs 1.234.567,89".</summary>
    public static string Formatear(long centavos, bool conSimbolo = true)
    {
        var negativo = centavos < 0;
        var abs = Math.Abs(centavos);
        var enteros = (abs / 100).ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
        var texto = $"{enteros},{abs % 100:00}";
        if (conSimbolo) texto = "Bs " + texto;
        return negativo ? "-" + texto : texto;
    }

    public static string Formatear(long? centavos) => centavos is null ? "—" : Formatear(centavos.Value);

    /// <summary>
    /// Lee montos como "150", "150,5", "150.50", "1.234,50" o "1,234.50". Devuelve centavos.
    /// </summary>
    public static bool TryLeer(string? texto, out long centavos)
    {
        centavos = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var t = texto.Trim().Replace("Bs", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");
        if (t.Length == 0) return false;

        // El último separador (punto o coma) seguido de 1 o 2 dígitos es el decimal; el resto son miles.
        var ultimo = Math.Max(t.LastIndexOf(','), t.LastIndexOf('.'));
        string parteEntera, parteDecimal = "";
        if (ultimo >= 0 && t.Length - ultimo - 1 is 1 or 2)
        {
            parteEntera = t[..ultimo];
            parteDecimal = t[(ultimo + 1)..];
        }
        else
        {
            parteEntera = t;
        }
        parteEntera = parteEntera.Replace(".", "").Replace(",", "");
        var negativo = parteEntera.StartsWith('-');
        if (negativo) parteEntera = parteEntera[1..];

        if (parteEntera.Length == 0) parteEntera = "0";
        if (!parteEntera.All(char.IsAsciiDigit) || !parteDecimal.All(char.IsAsciiDigit)) return false;
        if (!long.TryParse(parteEntera, out var bs)) return false;
        var cent = parteDecimal.Length switch { 0 => 0, 1 => (parteDecimal[0] - '0') * 10, _ => int.Parse(parteDecimal) };
        centavos = (bs * 100 + cent) * (negativo ? -1 : 1);
        return true;
    }

    /// <summary>Monto en centavos como texto editable, sin símbolo: 15050 → "150,50".</summary>
    public static string ParaEditar(long centavos) => Formatear(centavos, conSimbolo: false);
}
