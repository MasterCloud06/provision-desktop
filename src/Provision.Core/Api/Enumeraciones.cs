using System.Text.Json;

namespace Provision.Core.Api;

/// <summary>Textos legibles de las enumeraciones y su valor tal como lo espera la API.</summary>
public static class Enumeraciones
{
    /// <summary>LenteOftalmico → "LENTE_OFTALMICO".</summary>
    public static string ApiValor<T>(T valor) where T : struct, Enum =>
        JsonNamingPolicy.SnakeCaseUpper.ConvertName(valor.ToString());

    public static string Etiqueta(Rol r) => r switch
    {
        Rol.Superadmin => "Super administrador",
        Rol.Admin => "Administrador",
        Rol.Encargado => "Encargado de sucursal",
        Rol.Cajero => "Cajero",
        Rol.Vendedor => "Ejecutivo de ventas",
        Rol.Optometrista => "Optometrista",
        Rol.Laboratorio => "Laboratorio",
        _ => r.ToString(),
    };

    public static string Etiqueta(ResultadoEvento r) => r switch
    {
        ResultadoEvento.Exito => "Éxito",
        ResultadoEvento.Denegado => "Sin permiso",
        ResultadoEvento.Rechazado => "Rechazado",
        ResultadoEvento.Fallido => "Ingreso fallido",
        ResultadoEvento.Error => "Error",
        _ => r.ToString(),
    };

    public static string EtiquetaModulo(string modulo) => modulo switch
    {
        "acceso" => "Acceso",
        "usuarios" => "Usuarios",
        "sucursales" => "Sucursales",
        "catalogo" => "Catálogo",
        "inventario" => "Inventario",
        "clientes" => "Clientes",
        "caja" => "Caja",
        "ventas" => "Ventas",
        "laboratorio" => "Laboratorio",
        _ => modulo,
    };

    public static string Etiqueta(TipoProducto t) => t switch
    {
        TipoProducto.Montura => "Montura",
        TipoProducto.LenteOftalmico => "Lente oftálmico",
        TipoProducto.LenteContacto => "Lente de contacto",
        TipoProducto.GafasSol => "Gafas de sol",
        TipoProducto.Accesorio => "Accesorio",
        TipoProducto.Liquido => "Líquido",
        TipoProducto.Servicio => "Servicio",
        _ => t.ToString(),
    };

    public static string Etiqueta(EstadoCita e) => e switch
    {
        EstadoCita.Pendiente => "Por confirmar",
        EstadoCita.Confirmada => "Confirmada",
        EstadoCita.Atendida => "Atendida",
        EstadoCita.NoAsistio => "No asistió",
        EstadoCita.Cancelada => "Cancelada",
        _ => e.ToString(),
    };

    public static string Etiqueta(EstadoPedidoWeb e) => e switch
    {
        EstadoPedidoWeb.PendientePago => "Esperando pago",
        EstadoPedidoWeb.Pagado => "Pagado",
        EstadoPedidoWeb.Preparando => "En preparación",
        EstadoPedidoWeb.Listo => "Listo para retirar",
        EstadoPedidoWeb.Enviado => "Enviado",
        EstadoPedidoWeb.Entregado => "Entregado",
        EstadoPedidoWeb.Cancelado => "Cancelado",
        _ => e.ToString(),
    };

    public static string Etiqueta(MetodoPago m) => m switch
    {
        MetodoPago.Efectivo => "Efectivo",
        MetodoPago.Qr => "QR",
        MetodoPago.Tarjeta => "Tarjeta",
        MetodoPago.Transferencia => "Transferencia",
        _ => m.ToString(),
    };

    public static string Etiqueta(ConceptoCaja c) => c switch
    {
        ConceptoCaja.CobroVenta => "Cobro de venta",
        ConceptoCaja.Devolucion => "Devolución",
        ConceptoCaja.Gasto => "Gasto",
        ConceptoCaja.RetiroDueno => "Retiro del dueño",
        ConceptoCaja.Deposito => "Depósito",
        ConceptoCaja.Otro => "Otro",
        _ => c.ToString(),
    };

    public static string Etiqueta(EstadoPago e) => e switch
    {
        EstadoPago.Pendiente => "Pendiente",
        EstadoPago.Parcial => "Con saldo",
        EstadoPago.Pagada => "Pagada",
        _ => e.ToString(),
    };

    public static string Etiqueta(EstadoTrabajo e) => e switch
    {
        EstadoTrabajo.Pendiente => "Pendiente",
        EstadoTrabajo.EnLaboratorio => "En laboratorio",
        EstadoTrabajo.Listo => "Listo para entregar",
        EstadoTrabajo.Entregado => "Entregado",
        EstadoTrabajo.Cancelado => "Cancelado",
        _ => e.ToString(),
    };

    public static string Etiqueta(TipoCuentaPersonal t) => t switch
    {
        TipoCuentaPersonal.Efectivo => "Efectivo",
        TipoCuentaPersonal.Banco => "Banco",
        TipoCuentaPersonal.BilleteraDigital => "Billetera digital",
        TipoCuentaPersonal.TarjetaCredito => "Tarjeta de crédito",
        TipoCuentaPersonal.Ahorro => "Ahorro",
        _ => t.ToString(),
    };

    public static string Etiqueta(TipoTransaccion t) => t switch
    {
        TipoTransaccion.Ingreso => "Ingreso",
        TipoTransaccion.Gasto => "Gasto",
        TipoTransaccion.Transferencia => "Transferencia",
        _ => t.ToString(),
    };
}

/// <summary>Opción para listas desplegables: un valor con su texto visible.</summary>
public sealed record Opcion<T>(T Valor, string Texto)
{
    public override string ToString() => Texto;
}
