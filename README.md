# Miches 360 · Punto de venta

Punto de venta para micheladas y bebidas preparadas. Funciona sin internet: la laptop
(WPF) guarda todo en SQLite y sirve a los celulares de los meseros por la red Wi-Fi local
(la app del mesero vive en el proyecto `Miches360_AppMovil`).

## Proyectos

| Proyecto | Capa | Contenido |
|---|---|---|
| `Miches360.Dominio` | Núcleo | Entidades y reglas: catálogo, mesas, cuentas, subcuentas por persona, cobros. Sin dependencias. |
| `Miches360.Aplicacion` | Núcleo | Casos de uso (catálogo para venta, venta directa con cobro) e interfaces de acceso a datos. |
| `Miches360.Infraestructura` | Infraestructura | EF Core + SQLite, configuración de tablas y datos iniciales. |
| `Miches360.Servidor` | Infraestructura | Servidor local dentro de la caja: API HTTP + avisos en vivo (SignalR) para los celulares. Puerto 5360. |
| `Miches360` | Presentación | WPF (MVVM con CommunityToolkit.Mvvm). Tema "Opción 1 · Atardecer". |

## Ejecutar

1. Abrir `Miches360.slnx` en Visual Studio y dejar `Miches360` como proyecto de inicio.
2. Restaurar paquetes NuGet (requiere internet solo la primera vez que compilas).
3. F5. La base se crea en `%LOCALAPPDATA%\Miches360\miches360.db` con un catálogo de ejemplo.

Los datos de transferencia (banco, CLABE o tarjeta, titular) los captura el administrador
en **Ajustes**, protegido con PIN. El PIN de fábrica es `1234` y la pantalla pide cambiarlo.
El nombre de la caja de cada equipo está en `Miches360/appsettings.json`.

> Al arrancar, la base se pone al día sola: se agregan las tablas y columnas nuevas de
> cada versión sin perder ventas ni catálogo. Solo renombrar o quitar columnas
> requeriría migraciones (hasta hoy no ha hecho falta).

## Flujo de mesas

1. **Mesas** → toca una mesa libre → *Una cuenta* o *Separadas* (y cuántas personas) → **Abrir mesa**.
2. En la orden, elige a quién va (Persona 1, 2…) y toca los productos → **Enviar orden**.
3. Desde la mesa: **Agregar productos**, **Dividir y cobrar** (una columna por persona; toca un
   producto y luego *Pasar aquí* para moverlo) o **Cobrar todo**.
4. Cuando todas las personas quedan pagadas, la mesa se libera sola.
5. **Partes iguales**: divide lo pendiente entre N; cada parte se marca pagada en efectivo
   (con su cambio) o transferencia y al final se registra un solo cobro.
6. En la cuenta, **Nombre** cambia "Persona 1" por el nombre real. **Cancelar cuenta** (con motivo y
   PIN) está en el panel de la mesa.
7. **Notas por producto**: en la venta o en la orden, toca el nombre del producto para agregar
   "sin chile", "con clamato", etc. Salen en la cuenta y en el ticket.
8. Después de cobrar, el aviso trae **Imprimir ticket**.

## Corte de caja

- **Día / Semana / Mes** (la semana va de lunes a domingo), con flechas para ver periodos anteriores.
- Solo cuenta lo **cobrado** en el periodo y no incluye ventas canceladas. Si hay mesas abiertas se muestra un aviso con lo pendiente.
- **Imprimir reporte**: hoja carta con resumen, por día, por categoría, por producto, cancelaciones,
  arqueos y firmas. Para guardarlo en PDF elige "Microsoft Print to PDF".
- **Ticket de corte**: el mismo corte resumido en la impresora de tickets.
- **Arqueo** (modo día): fondo inicial + ventas en efectivo = lo que debe haber; captura lo
  contado y se guarda con la diferencia (cuadra, faltan o sobran).

## Historial, cancelaciones y tickets

- **Historial**: ventas del día (cobradas, abiertas y canceladas) con su ticket en pantalla y **Reimprimir ticket**.
- **Cancelar venta**: pide motivo y PIN de administrador. Funciona para ventas cobradas o mesas abiertas;
  lo cobrado deja de contar en el corte y aparece en la sección de cancelaciones.
- **Ticket**: en Ajustes se capturan nombre, dirección, teléfono, mensaje al pie, la impresora
  (lista de impresoras de Windows), el ancho del rollo (58 u 80 mm) y si lleva el logo. Tickets y
  reportes usan el diseño de la plataforma: logo, Bahnschrift condensada, bandas para secciones y el
  total destacado (el ticket en blanco y negro; el reporte en hoja con los colores café, crema y dorado). Al cobrar no se imprime nada;
  el ticket se imprime desde **Historial → Reimprimir ticket**. En una mesa, **Imprimir cuenta** saca
  la cuenta pendiente antes de cobrar.
- **Eliminar** mesas, categorías y productos desde Catálogo (dos toques para confirmar). Es un borrado
  lógico: desaparecen del sistema pero el historial y el corte conservan sus nombres. Una categoría
  solo se elimina sin productos y una mesa solo sin cuenta abierta.

## Celulares de los meseros

La caja es el servidor: los celulares se conectan a ella por el Wi-Fi del negocio (sin internet).
Toman la orden; **el cobro siempre se hace en la caja**.

1. Laptop y celulares en la misma red (router, repetidor o la zona Wi-Fi de la laptop).
2. La primera vez, en **Ajustes → Celulares de meseros** toca **Permitir en el firewall**
   (Windows pide permiso de administrador una sola vez).
3. En el celular abre *Miches 360*, escribe la **dirección** que muestra Ajustes (ej. `192.168.1.50`),
   el **código de 6 dígitos** y el nombre del mesero → **Conectar**.
4. Listo: el mesero ve el mapa de mesas, abre mesas (una cuenta o separadas), toma la orden por
   persona con notas, revisa la cuenta y **Pide la cuenta** (a la caja le llega un aviso).
5. Todo se sincroniza en vivo: lo que se cobra o cambia en la caja aparece en los celulares y las
   órdenes de los celulares aparecen en la caja.

- **Código nuevo** invalida el anterior (también cambia solo tras 5 intentos fallidos).
- **Quitar** un celular de la lista lo desconecta; para volver a usarlo necesita un código nuevo.
- El puerto y si el servidor arranca se configuran en `appsettings.json` → `Servidor`.
- Para instalar la caja en otra laptop se necesita el runtime **ASP.NET Core 10** además del de escritorio.

## Iteraciones

- [x] 1. Arquitectura, base de datos, venta rápida y cobro (efectivo, transferencia, mixto).
- [x] 1.1 Ajustes de administrador con PIN: datos de transferencia y cambio de PIN.
- [x] 2. Catálogo (con PIN): categorías, productos con precio por tamaño, agotado y alta de mesas.
- [x] 3. Mesas: abrir mesa, una cuenta o separadas, tomar orden por persona, pasar productos entre personas, cobrar por persona o toda la mesa.
- [x] 4. Corte de caja diario y semanal: efectivo/transferencia, por categoría, por producto, por día y arqueo de efectivo.
- [x] 5. Eliminar catálogo, historial con cancelaciones, tickets, reporte de corte diario/semanal/mensual.
- [x] 6. Servidor local (API + SignalR) y app del mesero en el celular (Android).
- [ ] 7. Comandas de barra/cocina.
