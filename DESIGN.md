---
name: Provisión Desktop
description: Gestión óptica nativa con contexto visible y composiciones propias de cada operación.
colors:
  primary: "#087F72"
  primary-deep: "#11524A"
  primary-soft: "#E3F1EE"
  ink: "#12343B"
  crystal: "#7FD1C4"
  background: "#F4F5F2"
  surface: "#FFFFFF"
  border: "#DCE3E3"
  field-border: "#C3CECE"
  muted: "#4E5D69"
  danger: "#B42318"
  warning: "#8A5300"
typography:
  title:
    fontFamily: Inter
    fontSize: 26px
    fontWeight: 600
  subtitle:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: 600
  body:
    fontFamily: Inter
    fontSize: 14px
  label:
    fontFamily: Inter
    fontSize: 13px
  number:
    fontFamily: Inter
    fontSize: 26px
    fontWeight: 600
    letterSpacing: -0.4px
rounded:
  control: 8px
  panel: 12px
  workspace: 14px
spacing:
  small: 8px
  panel: 16px
  page: 20px
  dashboard: 24px
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.surface}"
    rounded: "{rounded.control}"
    padding: 8px 16px
  workspace:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    rounded: "{rounded.workspace}"
    padding: 20px
  sidebar:
    backgroundColor: "{colors.ink}"
    width: 232px
---
# Design System: Provisión Desktop

## Overview
**Creative North Star: "Mostrador óptico"**
La aplicación combina un marco de navegación estable con áreas de trabajo diseñadas para cada operación. La identidad de petróleo, esmeralda y gris cálido organiza el trabajo sin sustituir las reglas comerciales ni los controles nativos de Avalonia.

**Key Characteristics:**
- Navegación agrupada por función y perfil persistente.
- Sucursal y entorno visibles en el menú lateral; cada pantalla trae su propio título (sin barra superior repetida).
- Cifras clave en una sola franja dividida por líneas finas, nunca una tarjeta por número.
- Composiciones diferenciadas para análisis, venta, expediente, calendario y catálogo.
- Importes legibles y estados expresados con texto además de color.

## Colors
El esmeralda identifica acciones principales y selección; el petróleo sostiene la navegación y el texto. El gris cálido separa las superficies blancas, con bandas suaves para filtros y seguimiento. Peligro y advertencia conservan sus colores semánticos y etiquetas.

**The Contraste Rule.** Las ayudas de entrada activas usan el texto secundario a opacidad completa, también en el estado vacío del tema Fluent. El texto deshabilitado no representa instrucciones necesarias para operar.

## Typography
Inter es la fuente del entorno nativo. Los títulos y cifras usan la escala efectiva del estilo final; los subtítulos identifican zonas de trabajo y los metadatos permanecen subordinados. Los importes usan dígitos tabulares. Las recetas conservan su notación monoespaciada para alinear OD y OI.

**The Lectura Rule.** Un cliente o producto largo debe envolver o tener elipsis con información recuperable; no desplaza los importes fuera de su columna.

## Layout
La estructura usa un lateral de 232 en verde petróleo (fondo, hover y activo de la misma familia) y no tiene barra superior: el título vive en cada pantalla y la sucursal, el entorno y la actualización (F5) en el lateral. El menú organiza Visión del negocio, Operación comercial, Atención óptica y Administración. Los grupos se filtran según los permisos existentes. El perfil se sitúa al pie y la navegación puede desplazarse en ventanas bajas.

Mi panel usa composición asimétrica para actividad, prioridades (filas, no tarjetas anidadas) y citas. Indicadores, Auditoría, Caja y Finanzas abren con la franja de cifras. Auditoría prioriza registros y filtros, dejando el análisis secundario plegable. Vender reúne una lista densa de mostrador (miniatura, nombre, código, precio y agregar) y carrito, cliente y pagos en otra columna. Laboratorio y Pedidos muestran su proceso como etapas numeradas; Ventas, Pedidos y Laboratorio separan registro de inspector. Caja distingue apertura y operación. Agenda incorpora un calendario sin grilla gris (hoy con contorno, elegido en esmeralda); Clientes abre con la cabecera del expediente (identidad y saldo) y organiza datos rotulados, recetas e historial en pestañas. Inventario mantiene filtros laterales y catálogo o lista. Usuarios combina directorio e inspector. Finanzas reúne resumen, balance, movimientos y cuentas.

**The Densidad Rule.** Las tarjetas de inventario dependen del ancho disponible, no del alto de la ventana. A 1100 de ancho se conservan dos tarjetas, con fotografía acotada entre 130 y 260 unidades; los filtros tienen etiquetas independientes y la ayuda inferior ocupa su propia fila.

Validado visualmente en 1440×900, 1100×900 y 1100×680 unidades a escala 1. Los paneles largos conservan desplazamiento vertical. Esto no acredita todavía escalado de Windows a 125 % o 150 %.

## Elevation & Depth
Las superficies de trabajo usan sombra ambiental suave sin borde adicional: `0 3 16 -6 #2012343B` en notación Avalonia. Los paneles secundarios conservan borde tenue. Las bandas de seguimiento usan separación tonal. Las prioridades tienen borde uniforme de una unidad; no llevan franjas laterales decorativas. Las superposiciones conservan el velo y la separación de las acciones de fondo.

## Shapes
Campos y botones tienen esquinas suaves; las superficies principales tienen un radio algo mayor. Los iconos son vectores consistentes y los estados combinan texto e indicación cromática. Las fotografías preservan su proporción y la ausencia de imagen se identifica como tal.

## Components
**Acción principal:** esmeralda, texto blanco, foco nativo y estados de interacción. Los botones secundarios y discretos mantienen las acciones auxiliares.

**Campo:** superficie blanca, borde definido, ayuda secundaria opaca y foco de dos unidades. La corrección de contraste incluye explícitamente el estado `TextBox:empty` del tema Fluent.

**Navegación:** grupos titulados, icono lineal, selección esmeralda y contexto persistente. La disponibilidad de opciones procede de permisos del servidor.

**Prioridad:** primero el título operativo, después Urgente, Pendiente, Para hoy o Al día. El estado no se presenta como rótulo decorativo encima del título.

**Catálogo:** dos o más tarjetas según ancho, imagen acotada, nombre, SKU, precio, stock y estado. La paginación, flechas y navegación por teclado se conservan.

**Gráfico:** barras nativas desde cero y valores derivados de datos recibidos. Cuando no hay movimientos se explica el vacío; no se fabrican tendencias.

**Ficha:** identidad del cliente seguida de pestañas de datos, recetas e historial comercial; conserva los comandos originales y sus permisos.

## Do's and Don'ts
- Do reutilizar los recursos nativos compartidos y comprobar su valor efectivo en el tema Fluent.
- Do revisar las tres resoluciones y la densidad del catálogo con datos reales o fixtures identificados.
- Do conservar el contexto de sucursal y los importes legibles.
- Don't inventar ventas, citas, fotografías o tendencias para llenar un estado vacío.
- Don't volver a usar ayudas activas semitransparentes, filtros recortados o franjas de urgencia.

Fuente: Estilos/Recursos.axaml, Estilos/Controles.axaml, las trece vistas y capturas del renderizador Avalonia con ENAI local. Las unidades documentadas como px corresponden a unidades lógicas a escala 1. Los ejemplos HTML del sidecar son representaciones documentales, no tecnología incorporada a la aplicación.

**The Movimiento Rule.** Los botones ceden a 0,97 al presionar (160 ms, ease-out fuerte) y los colores cambian en 150 ms. El menú lateral y las acciones de teclado no se animan.
