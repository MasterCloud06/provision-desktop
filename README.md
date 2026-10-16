# Provisión · Escritorio

Aplicación de escritorio de Provisión para ópticas: **vender, cobrar saldos, caja, inventario y finanzas personales**. Se conecta a la API de [`provision-backend`](https://github.com/MasterCloud06/provision-backend).

**Stack:** .NET 10 · C# · Avalonia 11.3 · CommunityToolkit.Mvvm. Corre en Windows (y también en Mac y Linux).

## Requisitos

- **.NET 10 SDK**: `winget install Microsoft.DotNet.SDK.10`
- Para el entorno **local**, el backend corriendo en tu PC (`npm run dev` en `provision-backend`).

## Uso con PowerShell

Desde la carpeta del proyecto:

```powershell
.\scripts\verificar.ps1      # revisa que tengas .NET 10
.\scripts\compilar.ps1       # compila todo
.\scripts\ejecutar.ps1       # abre la aplicación
.\scripts\publicar.ps1       # genera publicado\win-x64\Provision.exe para instalar en otra PC
```

Si PowerShell no deja ejecutar scripts, habilítalo una vez para tu usuario:

```powershell
Set-ExecutionPolicy -Scope CurrentUser RemoteSigned
```

## Entornos

En la pantalla de ingreso eliges el entorno, igual que en el backend:

| Entorno | API |
|---|---|
| **Local** | `http://localhost:3000/api/v1` |
| **Desarrollo** | `https://provision-backend-ib50.onrender.com/api/v1` |

Cuando estás en Desarrollo, el menú lateral lo muestra con una etiqueta amarilla para no confundirlo. El servidor de desarrollo (plan gratuito de Render) se duerme sin uso: el primer ingreso puede tardar hasta un minuto.

Si tu backend local usa otro puerto, edita `UrlLocal` en `%APPDATA%\Provision\preferencias.json`. Ese archivo también recuerda el último entorno y correo; nunca guarda contraseñas.

## Roles y pantallas

Cada usuario ve solo las pantallas que su rol permite. Los permisos los define el backend (`src/lib/permisos.ts` en `provision-backend`) y los vuelve a validar en cada operación, así que ocultar un botón aquí es comodidad, no seguridad.

| Pantalla | Super admin | Administrador | Encargado | Cajero | Ejecutivo de ventas | Optometrista | Laboratorio |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **Mi panel** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Indicadores del negocio | ✓ | ✓ | ✓ | | | | |
| **Auditoría** | ✓ | | | | | | |
| Vender | ✓ | ✓ | ✓ | ✓ cobra | ✓ sin cobrar | | |
| Ventas | ✓ | ✓ | ✓ | ✓ cobra saldos | ✓ consulta | ✓ consulta | |
| Caja | ✓ | ✓ | ✓ | ✓ | | | |
| Laboratorio | ✓ | ✓ | ✓ | ✓ entrega | ✓ entrega | ✓ consulta | ✓ arma |
| Clientes y recetas | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ recetas | ✓ consulta |
| Inventario | ✓ CRUD | ✓ CRUD | ✓ CRUD | consulta | consulta | consulta | consulta |
| Pedidos web | ✓ | ✓ | ✓ | ✓ cobra | ✓ sin cobrar | | |
| Usuarios | ✓ todos | ✓ sin administradores | | | | | |
| Mis finanzas | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

El retiro del dueño en Caja lo ven el super administrador y el administrador. El primer usuario del sistema es el super administrador.

## Pantallas

- **Mi panel** (todos, es la primera pantalla): para qué existe tu rol, tus responsabilidades, **qué hacer ahora** —tareas reales ordenadas por urgencia (urgente, pendiente, para hoy) con un botón que lleva a la pantalla donde se resuelven— y tus números. Ejemplos: el cajero ve "Abre la caja" o "2 ventas esperan cobro"; el ejecutivo, "3 clientes por avisar: sus lentes están listos"; el laboratorio, "1 orden atrasada".
- **Auditoría** (solo super administrador): eventos, usuarios con actividad, intentos negados y rechazados; actividad por rol (clic para filtrar), por día, por módulo y por usuario; últimos intentos negados; y el registro completo filtrable por rol, resultado, módulo y texto. Las finanzas personales no se registran.
- **Indicadores**: ventas del día y del mes, margen bruto, cuentas por cobrar, productos bajo stock, más vendidos y estado del laboratorio.
- **Vender**: buscar productos, carrito con cantidades y descuento, cliente (buscar o registrar al vuelo), pagos mixtos con cálculo del cambio, anticipo con saldo y orden de laboratorio si lleva lentes. Quien no opera caja (ejecutivo de ventas) registra la venta sin cobrar y el cliente pasa a caja.
- **Ventas**: historial por día, semana o mes, saldos pendientes y cobro de abonos (caja).
- **Caja**: abrir con fondo, totales por método, efectivo esperado, gastos, depósitos, retiro del dueño hacia una cuenta personal y cierre con arqueo.
- **Laboratorio**: tablero de órdenes por estado y atrasadas; graduación, productos e instrucciones; botones solo para los pasos que tu rol puede hacer (enviar, listo, entregar, cancelar) e historial de cada orden.
- **Clientes**: búsqueda, ficha con datos, saldo pendiente y compras; recetas OD/OI (esfera, cilindro, eje, adición, DNP) y registro de nuevas recetas.
- **Inventario**: CRUD de productos (crear, editar, eliminar —o desactivar si tiene historial— y reactivar) con **foto** (se sube a Cloudinary), **descripción**, **características** (para quién, medidas de montura `52□18 140`, material, forma, color; índice y tratamiento en lentes) y si se **muestra en la tienda web**; marcas, filtros por tipo, estado y bajo stock, y entrada de mercadería.
- **Pedidos web** (cajero, ejecutivo de ventas, encargado y administradores): los pedidos de la tienda en línea, por atender, esperando pago, entregados o cancelados. Para cada uno: qué compró, cómo lo recibe y cómo paga; botones para cada paso (preparar → listo para retirar o enviado → entregado); **avisar al cliente por WhatsApp**; notas internas y cancelar (devuelve el stock y, si pagó con tarjeta, el dinero). **Al entregar queda registrado como venta**, y si paga al retirar se **cobra en la caja abierta**.
- **Usuarios**: crear usuarios con su rol y sucursal (muestra qué puede hacer cada rol), cambiar rol, sucursal o contraseña, y activar/desactivar. Nadie puede quitarse su propio rol ni desactivarse.
- **Mis finanzas**: ingresos, gastos, ahorro y patrimonio del mes; cuentas; ingresos, gastos y transferencias; presupuesto por categoría.

## Estructura

```
src/
  Provision.Core/        # conexión con la API, modelos, dinero y entornos. Sin dependencias externas.
  Provision.Desktop/     # la aplicación Avalonia (MVVM)
    ViewModels/          # lógica de cada pantalla
    Views/               # pantallas en XAML
tools/
  Provision.Core.Pruebas/  # prueba de humo contra la API real
scripts/                 # PowerShell: verificar, compilar, ejecutar, probar-api, publicar
```

Montos: la API trabaja en **centavos** (Bs 150,50 = 15050). `Dinero.Formatear` los muestra como `Bs 1.234,50` y `Dinero.TryLeer` acepta lo que escriba el usuario (`150`, `150,50`, `1.234,50`).

## Probar la conexión con la API

`tools/Provision.Core.Pruebas` recorre el flujo completo contra una API real: login, productos, entradas, caja, venta con anticipo, abono, retiro del dueño, finanzas y cierre. **Crea datos**, así que úsalo contra tu entorno local:

```powershell
.\scripts\probar-api.ps1 -Email admin@provision.local -Password CambiaEsto123
```

## Compilación automática

Cada cambio en `main` se compila en GitHub Actions (Windows) y deja `Provision.exe` como artefacto descargable en la pestaña *Actions* del repo.
