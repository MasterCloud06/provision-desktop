using Avalonia.Media;

namespace Provision.Desktop.Controles;

/// <summary>Iconos de trazo de Provisión (grilla de 24×24). Se usan con <see cref="Icono"/>.</summary>
public static class Iconos
{
    private static Geometry G(string datos) => StreamGeometry.Parse(datos);

    // Marca: anteojos (el mismo motivo que el icono de la tienda web).
    public static Geometry Marca { get; } = G("M2 11h1.5 M20.5 11H22 M10.5 12.5a3.5 3.5 0 1 1 -7 0a3.5 3.5 0 1 1 7 0 M20.5 12.5a3.5 3.5 0 1 1 -7 0a3.5 3.5 0 1 1 7 0 M10.5 12c1-0.7 2-0.7 3 0");

    // Menú
    public static Geometry Panel { get; } = G("M4 4h7v7H4z M13 4h7v4h-7z M13 10h7v10h-7z M4 13h7v7H4z");
    public static Geometry Indicadores { get; } = G("M4 20h16 M7 16v-5 M12 16V7 M17 16v-8");
    public static Geometry Auditoria { get; } = G("M12 3l7 3v6c0 4.4-3 7.6-7 9c-4-1.4-7-4.6-7-9V6z M9 12l2 2l4-4");
    public static Geometry Vender { get; } = G("M3 4h2.5l2.2 10.5h10.6L20.5 7.5H6.4 M8.5 19.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0 M15.5 19.5a1 1 0 1 0 2 0a1 1 0 1 0 -2 0");
    public static Geometry Ventas { get; } = G("M6 3h12v18l-3-2l-3 2l-3-2l-3 2z M9 8h6 M9 12h6 M9 16h4");
    public static Geometry Pedidos { get; } = G("M12 3l8 4.5v9L12 21l-8-4.5v-9z M4 7.5l8 4.5l8-4.5 M12 12v9");
    public static Geometry Caja { get; } = G("M4 8h16v11H4z M4 8l2-4h12l2 4 M8 12h2 M14 12h2 M8 15.5h2 M14 15.5h2");
    public static Geometry Agenda { get; } = G("M5 5h14v15H5z M5 10h14 M9 3v4 M15 3v4");
    public static Geometry Laboratorio { get; } = G("M9 3h6 M10 3v6l-5.2 8.8A2 2 0 0 0 6.5 21h11a2 2 0 0 0 1.7-3.2L14 9V3 M7.3 15h9.4");
    public static Geometry Clientes { get; } = G("M12.5 8a3.5 3.5 0 1 1 -7 0a3.5 3.5 0 1 1 7 0 M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6 M16 4.5a3.5 3.5 0 0 1 0 7 M21 20c0-2.6-1.6-4.8-4-5.6");
    public static Geometry Inventario { get; } = G("M4 4h16v16H4z M4 12h16 M10 8h4 M10 16h4");
    public static Geometry Usuarios { get; } = G("M14.5 9a4.5 4.5 0 1 1 -9 0a4.5 4.5 0 1 1 9 0 M13.2 12.2L20 19 M17.5 16.5l2-2 M15.5 14.5l1.5-1.5");
    public static Geometry Finanzas { get; } = G("M12 3v9h9 M20.6 15A9 9 0 1 1 9 3.5");

    // Acciones
    public static Geometry Salir { get; } = G("M10 4H5v16h5 M15 8l4 4l-4 4 M19 12H9");
    public static Geometry Anterior { get; } = G("M15 6l-6 6l6 6");
    public static Geometry Siguiente { get; } = G("M9 6l6 6l-6 6");
    public static Geometry Cerrar { get; } = G("M6 6l12 12 M18 6L6 18");
    public static Geometry Mas { get; } = G("M12 5v14 M5 12h14");
    public static Geometry Menos { get; } = G("M5 12h14");
    public static Geometry Recargar { get; } = G("M20 12a8 8 0 1 1 -2.3-5.7L20 8.5 M20 4v4.5h-4.5");
    public static Geometry Buscar { get; } = G("M17 10.5a6.5 6.5 0 1 1 -13 0a6.5 6.5 0 1 1 13 0 M15.2 15.2L20 20");
    public static Geometry Check { get; } = G("M5 12.5l4.5 4.5L19 7.5");
    public static Geometry Alerta { get; } = G("M12 4l9 16H3z M12 10v4 M12 17h0.01");
    public static Geometry Vacio { get; } = G("M4 13l2.5-7h11l2.5 7v6H4z M4 13h5l1 2h4l1-2h5");
    public static Geometry Descargar { get; } = G("M12 4v11 M7 10l5 5l5-5 M5 20h14");
    public static Geometry Opciones { get; } = G("M13 5.5a1 1 0 1 1 -2 0a1 1 0 1 1 2 0 M13 12a1 1 0 1 1 -2 0a1 1 0 1 1 2 0 M13 18.5a1 1 0 1 1 -2 0a1 1 0 1 1 2 0");
    public static Geometry Editar { get; } = G("M4 20h4L19 9l-4-4L4 16z M13.5 6.5l4 4");
    public static Geometry Eliminar { get; } = G("M4 7h16 M9 7V4h6v3 M6 7l1 13h10l1-13 M10 11v5 M14 11v5");
    public static Geometry Foto { get; } = G("M3 5h18v14H3z M3 16l5-5l4 4l3-3l6 6 M16.5 9a1.5 1.5 0 1 1 -3 0a1.5 1.5 0 1 1 3 0");
    public static Geometry Catalogo { get; } = G("M3 5h8v8H3z M13 5h8v8h-8z M3 16h8 M3 19h5 M13 16h8 M13 19h5");
    public static Geometry Lista { get; } = G("M8 6h13 M8 12h13 M8 18h13 M3.5 6h0.5 M3.5 12h0.5 M3.5 18h0.5");
    public static Geometry Entrada { get; } = G("M12 3v10 M8 9l4 4l4-4 M4 14v6h16v-6");
}
