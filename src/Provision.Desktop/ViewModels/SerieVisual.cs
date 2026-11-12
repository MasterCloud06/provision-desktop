using Provision.Core.Api;
namespace Provision.Desktop.ViewModels;
public sealed record PuntoSerie(string Etiqueta, double Valor);
public static class SeriesVisuales
{
    public static IReadOnlyList<PuntoSerie> VentasDiarias(IEnumerable<VentaResumen> ventas, DateOnly fin, int dias = 7)
    {
        var montos = ventas.Where(v => v.Estado != EstadoVenta.Anulada).GroupBy(v => DateOnly.FromDateTime(v.CreadoEn.ToOffset(TimeSpan.FromHours(-4)).DateTime)).ToDictionary(g => g.Key, g => g.Sum(v => v.Total) / 100d);
        return Enumerable.Range(0, dias).Select(i => fin.AddDays(i - dias + 1)).Select(d => new PuntoSerie(d.ToString("dd/MM"), montos.GetValueOrDefault(d))).ToList();
    }
}
