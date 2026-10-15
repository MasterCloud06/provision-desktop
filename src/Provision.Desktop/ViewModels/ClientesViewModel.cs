using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Clientes: búsqueda, ficha con compras y saldo, y recetas (graduación).</summary>
public sealed partial class ClientesViewModel(Sesion sesion) : ViewModelBase, ISeccion
{
    public bool PuedeGestionar => sesion.Puede("clientes.gestionar");
    public bool PuedeRecetar => sesion.Puede("recetas.gestionar");

    [ObservableProperty] private string _busqueda = "";
    [ObservableProperty] private IReadOnlyList<Cliente> _clientes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayFicha))]
    private ClienteDetalle? _ficha;

    public bool HayFicha => Ficha is not null;

    [ObservableProperty] private Cliente? _seleccionado;

    public Task ActivarAsync() => BuscarAsync();

    [RelayCommand]
    private Task BuscarAsync() => IntentarAsync(async () => Clientes = await sesion.Api.BuscarClientesAsync(Busqueda));

    partial void OnSeleccionadoChanged(Cliente? value)
    {
        CreandoReceta = false;
        if (value is not null) _ = CargarFichaAsync(value.Id);
        else Ficha = null;
    }

    private Task CargarFichaAsync(Guid id) => IntentarAsync(async () =>
    {
        Ficha = await sesion.Api.ClienteAsync(id);
        EditNombre = Ficha.Nombre;
        EditCi = Ficha.Ci ?? "";
        EditTelefono = Ficha.Telefono ?? "";
    });

    /* ─────────── Nuevo y edición ─────────── */

    [ObservableProperty] private bool _creando;
    [ObservableProperty] private string _nuevoNombre = "";
    [ObservableProperty] private string _nuevoCi = "";
    [ObservableProperty] private string _nuevoTelefono = "";

    [RelayCommand]
    private void AlternarNuevo() => Creando = !Creando;

    [RelayCommand]
    private async Task CrearAsync()
    {
        if (NuevoNombre.Trim().Length < 2) { Falla("Escribe el nombre del cliente"); return; }
        Cliente? c = null;
        var ok = await IntentarAsync(async () => c = await sesion.Api.CrearClienteAsync(new NuevoCliente
        {
            Nombre = NuevoNombre.Trim(),
            Ci = Vacio(NuevoCi),
            Telefono = Vacio(NuevoTelefono),
        }));
        if (!ok) return;
        NuevoNombre = NuevoCi = NuevoTelefono = "";
        Creando = false;
        Busqueda = c!.Nombre;
        await BuscarAsync();
        Seleccionado = Clientes.FirstOrDefault(x => x.Id == c.Id);
        Aviso = $"Cliente {c.Nombre} registrado.";
    }

    [ObservableProperty] private string _editNombre = "";
    [ObservableProperty] private string _editCi = "";
    [ObservableProperty] private string _editTelefono = "";

    [RelayCommand]
    private async Task GuardarClienteAsync()
    {
        if (Ficha is null) return;
        if (EditNombre.Trim().Length < 2) { Falla("El nombre es muy corto"); return; }
        var id = Ficha.Id;
        var cambios = new NuevoCliente { Nombre = EditNombre.Trim(), Ci = Vacio(EditCi), Telefono = Vacio(EditTelefono) };
        if (!await IntentarAsync(() => sesion.Api.ActualizarClienteAsync(id, cambios))) return;
        await BuscarAsync();
        Seleccionado = Clientes.FirstOrDefault(x => x.Id == id);
        Aviso = "Datos del cliente guardados.";
    }

    /* ─────────── Nueva receta ─────────── */

    [ObservableProperty] private bool _creandoReceta;
    [ObservableProperty] private DateTime? _recetaFecha = DateTime.Today;
    [ObservableProperty] private string _recetaProfesional = "";
    [ObservableProperty] private string _odEsfera = "";
    [ObservableProperty] private string _odCilindro = "";
    [ObservableProperty] private string _odEje = "";
    [ObservableProperty] private string _odAdicion = "";
    [ObservableProperty] private string _oiEsfera = "";
    [ObservableProperty] private string _oiCilindro = "";
    [ObservableProperty] private string _oiEje = "";
    [ObservableProperty] private string _oiAdicion = "";
    [ObservableProperty] private string _dnpOd = "";
    [ObservableProperty] private string _dnpOi = "";
    [ObservableProperty] private string _usoRecomendado = "";
    [ObservableProperty] private string _observaciones = "";

    [RelayCommand]
    private void AlternarReceta()
    {
        CreandoReceta = !CreandoReceta;
        if (CreandoReceta && string.IsNullOrEmpty(RecetaProfesional)) RecetaProfesional = sesion.Usuario.Nombre;
    }

    [RelayCommand]
    private async Task GuardarRecetaAsync()
    {
        if (Ficha is null) return;
        if (RecetaFecha is not DateTime fecha) { Falla("Indica la fecha del examen"); return; }
        double? odEsf, odCil, odAdd, oiEsf, oiCil, oiAdd, dnpOd, dnpOi;
        int? odEje, oiEje;
        if (!Numero(OdEsfera, "OD esfera", out odEsf) || !Numero(OdCilindro, "OD cilindro", out odCil) ||
            !Entero(OdEje, "OD eje", out odEje) || !Numero(OdAdicion, "OD adición", out odAdd) ||
            !Numero(OiEsfera, "OI esfera", out oiEsf) || !Numero(OiCilindro, "OI cilindro", out oiCil) ||
            !Entero(OiEje, "OI eje", out oiEje) || !Numero(OiAdicion, "OI adición", out oiAdd) ||
            !Numero(DnpOd, "DNP OD", out dnpOd) || !Numero(DnpOi, "DNP OI", out dnpOi))
            return;

        var receta = new NuevaReceta
        {
            Fecha = DateOnly.FromDateTime(fecha),
            Profesional = Vacio(RecetaProfesional),
            OdEsfera = odEsf, OdCilindro = odCil, OdEje = odEje, OdAdicion = odAdd,
            OiEsfera = oiEsf, OiCilindro = oiCil, OiEje = oiEje, OiAdicion = oiAdd,
            DnpOd = dnpOd, DnpOi = dnpOi,
            UsoRecomendado = Vacio(UsoRecomendado),
            Observaciones = Vacio(Observaciones),
        };
        var id = Ficha.Id;
        if (!await IntentarAsync(() => sesion.Api.CrearRecetaAsync(id, receta))) return;
        OdEsfera = OdCilindro = OdEje = OdAdicion = OiEsfera = OiCilindro = OiEje = OiAdicion = DnpOd = DnpOi = "";
        UsoRecomendado = Observaciones = "";
        CreandoReceta = false;
        await CargarFichaAsync(id);
        Aviso = "Receta registrada.";
    }

    private static string? Vacio(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Lee "-1,25", "+0.75" o vacío. Vacío es válido (null).</summary>
    private bool Numero(string texto, string campo, out double? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (double.TryParse(texto.Trim().Replace(',', '.').Replace("+", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            valor = v;
            return true;
        }
        Falla($"{campo}: \"{texto}\" no es un número");
        return false;
    }

    private bool Entero(string texto, string campo, out int? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;
        if (int.TryParse(texto.Trim().TrimEnd('°'), out var v))
        {
            valor = v;
            return true;
        }
        Falla($"{campo}: \"{texto}\" debe ser un número entero de 0 a 180");
        return false;
    }
}
