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

## Pantallas

- **Inicio**: ventas del día y del mes, margen bruto, cuentas por cobrar, productos bajo stock, más vendidos y estado del laboratorio.
- **Vender**: buscar productos (nombre, SKU o código de barras), carrito con cantidades y descuento, cliente (buscar o registrar al vuelo), pagos mixtos (efectivo, QR, tarjeta, transferencia) con cálculo del cambio, y anticipo con saldo. Si la venta lleva lentes, pide fecha de entrega e instrucciones para la orden de laboratorio. El carrito se conserva al cambiar de pantalla.
- **Ventas**: historial por día, semana o mes, filtro de saldos pendientes y registro de abonos.
- **Caja**: abrir con fondo inicial, totales por método de pago, efectivo esperado, gastos, depósitos y retiro del dueño (que puede entrar directo a una cuenta personal), y cierre con arqueo que muestra el faltante o sobrante.
- **Inventario**: productos con stock, costo promedio y precio; filtros por tipo y bajo stock; alta de productos y marcas (administrador) y entrada de mercadería.
- **Mis finanzas**: ingresos, gastos, ahorro y patrimonio del mes; cuentas con saldo; registro de ingresos, gastos y transferencias; presupuesto por categoría con barra de avance.

Lo que no puede hacer tu rol, no aparece (por ejemplo, un vendedor no ve "Nuevo producto" ni "Retiro del dueño"), y la API lo vuelve a validar.

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
