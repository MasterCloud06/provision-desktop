using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Provision.Core.Api;
using Provision.Desktop.Servicios;

namespace Provision.Desktop.ViewModels;

/// <summary>Una cita en la lista, con los botones de lo que se puede hacer con ella.</summary>
public sealed partial class FilaCita(CitaAgenda cita, Func<FilaCita, EstadoCita, Task> cambiar, Action<FilaCita> whatsapp) : ObservableObject
{
    public CitaAgenda Cita { get; } = cita;
    public bool PuedeConfirmar => Cita.Siguientes.Contains(EstadoCita.Confirmada);
    public bool PuedeAtender => Cita.Siguientes.Contains(EstadoCita.Atendida);
    public bool PuedeCancelar => Cita.Siguientes.Contains(EstadoCita.Cancelada);
    public bool Activa => Cita.Estado is EstadoCita.Pendiente or EstadoCita.Confirmada;
    public bool PorConfirmar => Cita.Estado == EstadoCita.Pendiente;

    [RelayCommand] private Task Confirmar() => cambiar(this, EstadoCita.Confirmada);
    [RelayCommand] private Task Atendida() => cambiar(this, EstadoCita.Atendida);
    [RelayCommand] private Task NoAsistio() => cambiar(this, EstadoCita.NoAsistio);
    [RelayCommand] private Task Cancelar() => cambiar(this, EstadoCita.Cancelada);
    [RelayCommand] private void WhatsApp() => whatsapp(this);
}

/// <summary>
/// Agenda de exámenes de la vista: las citas del día (de la tienda web y las que toma el personal),
/// confirmarlas por WhatsApp, marcarlas atendidas o no asistió, y tomar citas nuevas.
/// </summary>
public sealed partial class AgendaViewModel : ViewModelBase, ISeccion
{
    private static readonly CultureInfo Bo = new("es-BO");
    private readonly Sesion _sesion;

    public AgendaViewModel(Sesion sesion)
    {
        _sesion = sesion;
        _dia = DateOnly.FromDateTime(DateTime.Today);
        _nuevaFecha = DateTime.Today;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TituloDia), nameof(EsHoy))]
    private DateOnly _dia;

    public string TituloDia
    {
        get
        {
            var texto = Dia.ToDateTime(TimeOnly.MinValue).ToString("dddd d 'de' MMMM", Bo);
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            return Dia == hoy ? $"Hoy, {texto}" : Dia == hoy.AddDays(1) ? $"Mañana, {texto}" : char.ToUpper(texto[0]) + texto[1..];
        }
    }

    public bool EsHoy => Dia == DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SinCitas))]
    private IReadOnlyList<FilaCita> _citas = [];

    [ObservableProperty] private string _resumen = "";
    public bool SinCitas => Citas.Count == 0;

    public Task ActivarAsync() => CargarAsync();

    [RelayCommand] private Task DiaAnterior() { Dia = Dia.AddDays(-1); return CargarAsync(); }
    [RelayCommand] private Task DiaSiguiente() { Dia = Dia.AddDays(1); return CargarAsync(); }
    [RelayCommand] private Task IrAHoy() { Dia = DateOnly.FromDateTime(DateTime.Today); return CargarAsync(); }

    [RelayCommand]
    private Task CargarAsync() => IntentarAsync(async () =>
    {
        var lista = await _sesion.Api.CitasAsync(Dia, Dia);
        Citas = lista.Select(c => new FilaCita(c, CambiarAsync, AbrirWhatsApp)).ToList();
        var activas = lista.Count(c => c.Estado is EstadoCita.Pendiente or EstadoCita.Confirmada);
        var porConfirmar = lista.Count(c => c.Estado == EstadoCita.Pendiente);
        Resumen = lista.Count == 0 ? "Sin citas este día"
            : $"{activas} por atender" + (porConfirmar > 0 ? $" · {porConfirmar} por confirmar" : "") + $" · {lista.Count} en total";
        if (Motivos.Count == 0)
        {
            var cfg = await _sesion.Api.ConfigCitasAsync();
            Motivos = cfg.Motivos.Select(m => new Opcion<string>(m.Key, m.Value)).ToList();
            NuevoMotivo = Motivos.FirstOrDefault();
        }
    });

    private async Task CambiarAsync(FilaCita fila, EstadoCita estado)
    {
        var c = fila.Cita;
        string? nota = estado == EstadoCita.Cancelada ? "Cancelada por la óptica" : null;
        if (!await IntentarAsync(() => _sesion.Api.CambiarEstadoCitaAsync(c.Id, estado, nota))) return;
        await CargarAsync();
        Aviso = $"{c.Hora} · {c.NombreContacto}: {Enumeraciones.Etiqueta(estado).ToLowerInvariant()}.";
    }

    private void AbrirWhatsApp(FilaCita fila)
    {
        var c = fila.Cita;
        var numero = new string(c.Telefono.Where(char.IsDigit).ToArray());
        if (numero.Length == 8) numero = "591" + numero;
        if (numero.Length < 8) { Falla("El teléfono de la cita no parece un número de WhatsApp"); return; }
        var dia = DateOnly.ParseExact(c.Fecha, "yyyy-MM-dd").ToDateTime(TimeOnly.MinValue).ToString("dddd d 'de' MMMM", Bo);
        var texto = $"Hola {c.NombreContacto.Split(' ')[0]}, te escribimos de la óptica para confirmar tu cita de {c.MotivoTexto.ToLowerInvariant()} " +
                    $"el {dia} a las {c.Hora}. ¿Nos confirmas que vienes? Si usas lentes, tráelos.";
        try
        {
            Process.Start(new ProcessStartInfo($"https://wa.me/{numero}?text={Uri.EscapeDataString(texto)}") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Falla($"No se pudo abrir WhatsApp: {ex.Message}");
        }
    }

    /* ─────────── Nueva cita (por teléfono o en mostrador) ─────────── */

    [ObservableProperty] private bool _mostrandoNueva;
    [ObservableProperty] private IReadOnlyList<Opcion<string>> _motivos = [];
    [ObservableProperty] private Opcion<string>? _nuevoMotivo;
    [ObservableProperty] private DateTime? _nuevaFecha;
    [ObservableProperty] private IReadOnlyList<string> _turnosLibres = [];
    [ObservableProperty] private string? _nuevaHora;
    [ObservableProperty] private string _nuevoNombre = "";
    [ObservableProperty] private string _nuevoTelefono = "";
    [ObservableProperty] private string _nuevasNotas = "";
    [ObservableProperty] private string _textoTurnos = "";

    partial void OnNuevaFechaChanged(DateTime? value) => _ = CargarTurnosAsync();

    [RelayCommand]
    private Task Nueva()
    {
        MostrandoNueva = true;
        NuevaFecha = Dia.ToDateTime(TimeOnly.MinValue);
        return CargarTurnosAsync();
    }

    [RelayCommand]
    private void CancelarNueva()
    {
        MostrandoNueva = false;
        NuevoNombre = NuevoTelefono = NuevasNotas = "";
        NuevaHora = null;
    }

    private async Task CargarTurnosAsync()
    {
        if (!MostrandoNueva || NuevaFecha is not { } f) return;
        List<DiaAgenda>? dias = null;
        if (!await IntentarAsync(async () => dias = await _sesion.Api.DisponibilidadCitasAsync(DateOnly.FromDateTime(f), 1))) return;
        var dia = dias!.FirstOrDefault(d => d.Fecha == DateOnly.FromDateTime(f).ToString("yyyy-MM-dd"));
        TurnosLibres = dia?.Turnos.Where(t => t.Libre).Select(t => t.Hora).ToList() ?? [];
        NuevaHora = TurnosLibres.FirstOrDefault();
        TextoTurnos = dia is null ? "Ese día la óptica no atiende." : TurnosLibres.Count == 0 ? "No quedan turnos libres ese día." : $"{TurnosLibres.Count} turnos libres";
    }

    [RelayCommand]
    private async Task GuardarNuevaAsync()
    {
        if (NuevaFecha is not { } f || NuevaHora is null) { Falla("Elige un día con turnos libres y la hora"); return; }
        if (NuevoNombre.Trim().Length < 2) { Falla("Escribe el nombre del paciente"); return; }
        if (NuevoTelefono.Trim().Length < 6) { Falla("Escribe un teléfono de contacto"); return; }
        var cita = new NuevaCita
        {
            Fecha = DateOnly.FromDateTime(f).ToString("yyyy-MM-dd"),
            Hora = NuevaHora,
            Motivo = NuevoMotivo?.Valor ?? "EXAMEN_VISTA",
            NombreContacto = NuevoNombre.Trim(),
            Telefono = NuevoTelefono.Trim(),
            Notas = string.IsNullOrWhiteSpace(NuevasNotas) ? null : NuevasNotas.Trim(),
        };
        if (!await IntentarAsync(() => _sesion.Api.CrearCitaAsync(cita))) return;
        var mensaje = $"Cita agendada: {cita.NombreContacto}, {cita.Hora}.";
        Dia = DateOnly.FromDateTime(f);
        CancelarNueva();
        await CargarAsync();
        Aviso = mensaje;
    }
}
