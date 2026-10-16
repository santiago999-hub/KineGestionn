# PENDIENTES-KINEGESTION

> Memoria de trabajo del agente. **Leer al inicio de cada sesión** antes de explorar el repo.
> Regla de oro (ahorro de tokens): si la tarea es sobre contenido/documentación, leer `RESUMEN-INTEGRAL-KINEGESTION.md`; NO re-explorar las 4 capas de nuevo.

Última actualización: 2026-10-16 (sesión de UI + documentación).

---

## 1. Estado actual

- Repo: `C:\Users\Santy\OneDrive\Desktop\Nueva carpeta` (KineGestion, ASP.NET Core 8 MVC + EF Core 8 + SQL Server 2022). Branch `main`.
- Último commit: `f70799e style: cubre toda la barra con glow/barrido constante y arregla hover de cards` (pusheado a `origin/main`).
- Documento maestro de sistema: `RESUMEN-INTEGRAL-KINEGESTION.md` (consolidado en la raíz).
- Sin cambios de código abiertos; solo archivos nuevos sin commitear (ver §4).

## 2. Tareas pendientes (backlog)

### Alta prioridad (documentación / defensa)
- [ ] Escribir sección **"Guion de presentación (3-5 min)"** en `RESUMEN-INTEGRAL-KINEGESTION.md` (recorrido en orden de venta).
- [ ] Decidir si profundizar **Seguridad** o **flujo de recordatorios** dentro del mismo documento.
- [ ] Revisar coherencia entre `RESUMEN-INTEGRAL-KINEGESTION.md` y los otros docs ya existentes (`TESIS-*`, `GUIA-PARA-ENTENDER-KINEGESTION.md`, `ANALISIS-SISTEMA-KINEGESTION.md`) para no duplicar/contradecir.

### Media prioridad (producto)
- [ ] Entidad `Payment` (montos, parciales, método de cobro, recibo).
- [ ] Reportes PDF + cierre de caja.
- [ ] Portal de autogestión del paciente (ver turnos, reprogramar, pagar).

### Media prioridad (técnica / robustez)
- [ ] Caché distribuida (Redis) para multi-instancia (hoy `QueryCache` es por proceso).
- [ ] Broker de mensajería (Redis/RabbitMQ/Azure Service Bus) + **DLQ** al escalar a > 1 consumidor.
- [ ] Idempotencia por ventana (SessionId + ventana operativa) en el productor.
- [ ] MFA para perfiles admin + rate limiting en login.
- [ ] Key ring de DataProtection en almacén compartido (hoy filesystem local).
- [ ] IaC + orquestador (K8s/ACA) + política de backups/DR.

### Deuda técnica / calidad
- [ ] Medir cobertura de tests y fijar umbral (quality gate).
- [ ] SAST / escaneo de dependencias y secretos en CI.
- [ ] Completar i18n (hoy ES/EN parcial: layout + auditoría) + test de paridad de claves.
- [ ] Tests e2e (Playwright) de flujos críticos.
- [ ] OpenTelemetry + dashboards (Grafana) para reemplazar el profiling casero.
- [ ] Eliminar métodos `[Obsolete]` (`GetAllAsync`, `GetPagedAsync` legacy) y estandarizar DI (`AddData`/`AddApplication`).

## 3. Bitácora de esta sesión (UI + docs)

- Causa raíz "animaciones no funcionan": Windows tenía "Efectos de animación" OFF → `prefers-reduced-motion: reduce` → el CSS apagaba todo. Se activó `SPI_SETCLIENTAREAANIMATION` (0x1043). Requiere cerrar todo el navegador.
- `KineGestion.Web/wwwroot/css/site.css`: `kgFadeUp` con `backwards`; sidebar `overflow:hidden` + scroll movido a `.kg-sidebar-nav`; `kgSidebarSheen` constante 6.5s + `.kg-sidebar::before` (`kgSidebarGlow` 24s); bloque `prefers-reduced-motion` cubre `::before`.
- Verificado con headless (puppeteer-core + Chrome) a 900px/700px.
- Documentación: consolidado `RESUMEN-INTEGRAL-KINEGESTION.md` con análisis por área (vender/pulir/plan), matriz de madurez, glosario por categorías y preguntas de jurado.

## 4. Archivos sin commitear (revisar antes de commitear)

- `RESUMEN-INTEGRAL-KINEGESTION.md` → conservar.
- `PENDIENTES-KINEGESTION.md` → conservar (actualizado).
- `AGENTS.md` → conservar (memoria de trabajo).
- Temporales de exploración: **eliminados**.

## 5. Guía de eficiencia (feedback + tokens)

**Para el agente:**
1. Leer PRIMERO `PENDIENTES-KINEGESTION.md` (este) y `RESUMEN-INTEGRAL-KINEGESTION.md`. Solo explorar código si la tarea lo exige.
2. No re-explorar las 4 capas: el resumen ya contiene arquitectura, dominio, persistencia, Web, flujos, seguridad e i18n con detalle.
3. Para cambios de código, leer solo el archivo objetivo + su contexto inmediato (no todo el proyecto).
4. Delegar exploración amplia a subagentes `explore` en paralelo y pedir salida acotada.
5. Confirmar el archivo a editar antes de escribir; preferir `edit` sobre `write`.

**Comandos (del AGENTS.md):**
- Build: `dotnet build KineGestion.Web/KineGestion.Web.csproj`
- Tests (secuencial, nunca en paralelo por lock CS2012): `dotnet test KineGestion.Web.Tests/... --no-restore` y luego `dotnet test KineGestion.Tests/... --no-restore`.
- Migraciones: `dotnet ef database update --project KineGestion.Data --startup-project KineGestion.Web` (no usar `-v q`).
- Correr app: `dotnet run --project KineGestion.Web --no-build --urls http://localhost:5138`.

## 6. Convenciones rápidas

- Commits: `tipo: descripción` en español minúscula (`style:`, `fix:`, `build:`). No commitear sin pedido explícito.
- Repos de fecha: desde `>= fecha.Date`, hasta `< fecha.Date.AddDays(1)`.
- `DispatchType` centralizados en `KineGestion.Core.DispatchTypes`.
- Tests que tocan BD real: `Integration` en el nombre de la CLASE.
