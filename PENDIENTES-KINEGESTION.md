# PENDIENTES-KINEGESTION

> Memoria de trabajo del agente. **Leer al inicio de cada sesión** antes de explorar el repo.
> Regla de oro (ahorro de tokens): si la tarea es sobre contenido/documentación, leer `RESUMEN-INTEGRAL-KINEGESTION.md`; NO re-explorar las 4 capas de nuevo.

Última actualización: 2026-10-29 (módulo Cobranzas pulido; fin de sesión con verificación runtime hecha).

---

## 1. Estado actual

- Repo: `C:\Users\Santy\OneDrive\Desktop\Nueva carpeta` (KineGestion, ASP.NET Core 8 MVC + EF Core 8 + SQL Server 2022). Branch `main`.
- Último commit: `d78864e Redesign billing doughnut: semantic gradients, center total, informative legend, theme-aware rendering; add startup-time task`.
- Documento maestro de sistema: `RESUMEN-INTEGRAL-KINEGESTION.md` (consolidado en la raíz).
- **Cambios abiertos sin commitear** del pulido de Cobranzas (ver §4). Suites: Web.Tests 183 verde / 3 rojos preexistentes de Home; Tests (integración) 156 verde.
- Estado de esta sesión: **trabajo de Cobranzas terminado y verificado en runtime**: GET `/Billing` 200 con los 4 canvas (`billingEvolutionChart`, `billingStatusChart`, `billingAgingChart`, `billingTrendChart`) y `/Billing/ExportCsv` 200 `text/csv`. App de verificación detenida. Falta solo commitear (no pedido).

## 2. Tareas pendientes (backlog)

### Alta prioridad (documentación / defensa)
- [x] ~~Guion de presentación~~ → cubierto por §12 del `RESUMEN-INTEGRAL` (pitch 2 min + 5 lentes + matriz Base/Full).
- [ ] Decidir si profundizar **Seguridad** o **flujo de recordatorios** dentro del mismo documento.
- [ ] Revisar coherencia entre `RESUMEN-INTEGRAL-KINEGESTION.md` y los otros docs ya existentes (`TESIS-*`, `GUIA-PARA-ENTENDER-KINEGESTION.md`, `ANALISIS-SISTEMA-KINEGESTION.md`) para no duplicar/contradecir.

### Media prioridad (producto)
- [x] ~~Pulir módulo Cobranzas (`/Billing/Index`)~~ → 2026-10-29 (ver bitácora). Incluye borrar el mock `Views/Cobranzas/Index.cshtml`.
- [x] **Dashboard multisede: filtro por sede en los KPIs, selector en el header y vista detalle por sucursal manteniendo la apariencia global del dashboard** → implementado y verificado con datos reales (2026-10-20): selector de oficinas en header que pobla desde BD, dropdown custom accesible, filtrado de KPIs por officeId y agregados. Bugs corregidos del feature: (a) caché de `IMemoryCache` del dashboard tenía clave fija → ahora `DashboardCacheKeyFor(officeId)`; (b) `SelectedOfficeId` nunca se seteaba en el modelo; (c) la clave de `QueryCache` en `SessionService.GetDashboardCountsAsync` no incluía `officeId` → el warmup contaminaba todas las sedes. Seed de datos demo: `KineGestion.Web/ops/Seed-MultiSede.sql`.
- [ ] Entidad `Payment` (montos, parciales, método de cobro, recibo).
- [ ] Reportes PDF + cierre de caja.
- [ ] Portal de autogestión del paciente (ver turnos, reprogramar, pagar).

### Media prioridad (técnica / robustez)
- [ ] **Sacar las llaves/secretos de GitHub del repo** (no es seguro): barrido 2026-10-19 → sin PAT/tokens/API keys hardcodeados (workflows usan `GITHUB_TOKEN`; prod usa `CHANGE_ME` con fail-fast). ~~Credencial de seed `Admin1234` en `appsettings.json`~~ → resuelto 2026-10-20: sacada del `appsettings.json` rastreado; vive solo en `appsettings.Development.json` (gitignored) + env var `Seed__AdminPassword` en no-dev (fail-fast en `Program.cs` y en `ValidateProductionSafetyConfiguration`); scripts de ops leen `KINEGESTION_AUTH_SECRET`. Pendiente: **secret scanning en CI y rotar si algún secreto llegó a publicarse** (el literal `Admin1234` sigue en historial de git).
- [ ] Caché distribuida (Redis) para multi-instancia (hoy `QueryCache` es por proceso).
- [ ] **Mejorar tiempos de arranque de la página**: perfilar cold start (startup, warmup, JIT, migraciones y queries iniciales), fijar objetivo p95 y documentar resultados en `REPORTE-BENCHMARK-ARRANQUE-Y-LATENCIA-2026-09-23.md`.
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

## 3. Bitácora reciente

- **2026-10-29 (Cobranzas):** se pulió la página real `/Billing/Index` y se borró el mock `Views/Cobranzas/Index.cshtml` (459 líneas hardcodeadas, sin link, agregado en `5245657`).
  - **Backend:** `PendingPatientDebtDto`; `GetTopPendingPatientsAsync` en `ISessionMetricsRepository`/`SessionMetricsRepository` y en `ISessionService`/`SessionService` (QueryCache 10 s, clave `sessions:top-debts:...`). **Ojo EF:** el `GroupBy` agrupando por columnas de la navegación `s.Patient` y proyectando el record en una sola query **no traduce** (`The LINQ expression ... could not be translated`). Solución estable: query 1 agrupa solo por `PatientId` (`Count`/`Min`), query 2 trae pacientes por `Contains` y se arma el DTO en memoria.
  - **`BillingController`:** `Index` acepta `status`, `payment`, `filtersApplied` (default sigue siendo Completed+Pending); `ExportCsv` (CSV `;`, BOM UTF-8, tope 5000 filas) y helpers `ResolveDateRange` (tope 366 días), `BuildDailyPointsAsync`, `BuildProfessionalEffectivenessAsync`, `BuildTopPendingPatientsAsync`, `CsvField`. Los redirects de `MarkPaid/MarkPending/MarkPaidBatch/MarkPendingBatch` preservan filtros.
  - **Vista:** resumen (cobrado con variación vs período anterior, a cobrar, efectividad, sesiones), evolución con presets 7/30/90, doughnut de estados, barras de antigüedad y de efectividad semanal, tabla de efectividad por profesional, top de deudas por paciente, filtros avanzados, tabla con importes estimados (`Billing:DefaultSessionAmount`), acciones por fila y paginación. Chart.js local + `kg-chart.js` (sin CDN).
  - **Tests:** +3 unitarios en `BillingControllerTests` (filtros/insights, `ExportCsv`, default sin filtros) y +1 integración `GetTopPendingPatientsAsync_ShouldRankCompletedPendingDebtByPatient`. Web.Tests 183/186; los 3 rojos de Home son **preexistentes** (no tocamos Home): `HomeViewTemplateTests.IndexView_ShouldKeepAccessibleOperationalAlertOriginBadgeMarkup`, `HomeControllerTests.Index_ShouldRecomputeDashboard_AfterDomainChangeInvalidatesCache`, `HomeControllerTests.Index_ShouldForwardSelectedOffice_ToSessionScopedQueries`. Pista: `KineGestion.Web/output.txt` contiene un diff viejo de `Views/Home/Index.cshtml` sin aplicar. Tests suites: 156/156 verde.
  - **Runtime:** verificado con `dotnet run --no-build --urls http://localhost:5089` + login `admin@kinegestion.com`/`Admin1234`: `/Billing` 200 (4 canvas) y `/Billing/ExportCsv?...` 200 `text/csv`. App detenida al cerrar.
- **2026-10-21 (P0 dashboard / calidad):** encoding, KPIs y caché.
  - **Encoding:** `Views/Home/Index.cshtml` y `Views/Billing/Index.cshtml` estaban guardados en Windows-1252 (45 y 19 caracteres inválidos) → se veían "Auditor�a"/"An�lisis" en el navegador. Convertidos a UTF-8 sin BOM + `.editorconfig` nuevo (`charset = utf-8`) para evitar recurrencia. Verificado: 0 caracteres inválidos en `/`, `/Billing`, `/Sessions`, `/Segmentacion`, `/ReminderFunnel`.
  - **Vista del dashboard saneada:** se eliminó el bloque de KPIs duplicado ("Indicadores clave" vs "Rendimiento 30 días", con fórmulas contradictorias); queda una sola fila canónica con la fórmula visible en el subtítulo y en el `title` (cobranza = pagadas ÷ completadas; cancelación = canceladas ÷ agendadas; tardías con %). Grilla de 5 tarjetas operativas corregida a `col-sm-6 col-lg-4 col-xl`. Tarjetas de pacientes/profesionales/tratamientos marcadas **Global** (no dependen de la sede) y sesiones con badge **Por sede/Global**. Badge de alcance + "Actualizado hh:mm:ss" en el header; tarjeta de sistema con versión real del ensamblado (antes "v1.0 — Sprint 2").
  - **Coherencia de sede:** `CountByCancellationReasonInRangeAsync` ahora acepta `officeId` (Core + Data + HomeController); la clave de caché incluye la sede. Los KPI de pacientes/profesionales/tratamientos se declaran globales en vez de fingir filtro.
  - **Caché del dashboard:** `QueryCache` expone `Version` (se incrementa en cada `InvalidatePrefix`/`ClearAll`) y `NotifyDataChanged()`. La clave del snapshot del dashboard y del snapshot de alerta operativa incluyen la versión → cualquier escritura de dominio (sesión, cobro, paciente, profesional, tratamiento, consultorio) invalida el dashboard de forma inmediata. Antes el ViewModel cacheado (30 s) solo se invalidaba al disparar la alerta de cobranzas (KPIs viejos hasta 30 s). `GeneratedAtUtc` agregado al ViewModel.
  - **Tests:** Web.Tests 183 verde (+2), Tests 143 verde (+4). Nuevos: scoping por sede de motivos de cancelación (integración), versionado de `QueryCache` (3), officeId reenviado a las consultas del dashboard, y recálculo del dashboard tras invalidación de caché.
- **2026-10-20 (seguridad):** credencial seed `Admin1234` fuera del repo: quitada de `appsettings.json` (rastreado), agregada a `appsettings.Development.json` (gitignored); `Program.cs` hace fail-fast si falta `Seed:AdminPassword` en no-dev; scripts de ops usan env var `KINEGESTION_AUTH_SECRET` en vez del literal. `RESUMEN-INTEGRAL` §1 actualizado. Además reparada `HomeControllerTests` (estaba roja en `HEAD` por el feature multisede: faltaba `IOfficeService`/`officeId` y el 4º arg de `GetDashboardCountsAsync`). Suites: Web.Tests 181 verde, Tests 139 verde.
- **2026-10-19:** agregada §12 "Análisis con 5 lentes de defensa" a `RESUMEN-INTEGRAL-KINEGESTION.md`: 3 trade-offs estructurales (monolito MVC / outbox SQL / cifrado+RowVersion), tabla de 5 fallos catastróficos con mitigación, eje entropía Base/Full, matriz financiera Base vs Full y pitch de ascensor de 2 min. Marcado el pendiente del guion de presentación.
- **2026-10-16 (UI + docs):**

- Causa raíz "animaciones no funcionan": Windows tenía "Efectos de animación" OFF → `prefers-reduced-motion: reduce` → el CSS apagaba todo. Se activó `SPI_SETCLIENTAREAANIMATION` (0x1043). Requiere cerrar todo el navegador.
- `KineGestion.Web/wwwroot/css/site.css`: `kgFadeUp` con `backwards`; sidebar `overflow:hidden` + scroll movido a `.kg-sidebar-nav`; `kgSidebarSheen` constante 6.5s + `.kg-sidebar::before` (`kgSidebarGlow` 24s); bloque `prefers-reduced-motion` cubre `::before`.
- Verificado con headless (puppeteer-core + Chrome) a 900px/700px.
- Documentación: consolidado `RESUMEN-INTEGRAL-KINEGESTION.md` con análisis por área (vender/pulir/plan), matriz de madurez, glosario por categorías y preguntas de jurado.

## 4. Archivos sin commitear (revisar antes de commitear)

- `RESUMEN-INTEGRAL-KINEGESTION.md` → conservar.
- `PENDIENTES-KINEGESTION.md` → conservar (actualizado).
- `AGENTS.md` → conservar (memoria de trabajo).
- Cambios de código sin commitear (2026-10-29, Cobranzas): `KineGestion.Core/DTOs/PendingPatientDebtDto.cs` (nuevo), `KineGestion.Core/Interfaces/ISessionMetricsRepository.cs`, `KineGestion.Core/Interfaces/ISessionService.cs`, `KineGestion.Core/Services/SessionService.cs`, `KineGestion.Data/Repositories/SessionMetricsRepository.cs`, `KineGestion.Web/Controllers/BillingController.cs`, `KineGestion.Web/Models/ViewModels/BillingDashboardViewModel.cs`, `KineGestion.Web/Views/Billing/Index.cshtml`, `KineGestion.Web/Views/Cobranzas/Index.cshtml` (eliminado), `KineGestion.Web.Tests/BillingControllerTests.cs`, `KineGestion.Tests/SessionRepositoryIntegrationTests.cs`. **No commitear** `KineGestion.Web/output.txt` (diff viejo de Home) ni `publish*/`, `mejoras-*/`.
- Cambios de código sin commitear (2026-10-21, P0 dashboard): `KineGestion.Core/Interfaces/ISessionMetricsRepository.cs`, `KineGestion.Core/Interfaces/ISessionService.cs`, `KineGestion.Core/Services/QueryCache.cs`, `KineGestion.Core/Services/SessionService.cs`, `KineGestion.Data/Repositories/SessionMetricsRepository.cs`, `KineGestion.Web/Controllers/HomeController.cs`, `KineGestion.Web/Models/ViewModels/HomeDashboardViewModel.cs`, `KineGestion.Web/Services/BillingOperationalAlertService.cs`, `KineGestion.Web/Views/Home/Index.cshtml`, `KineGestion.Web/Views/Billing/Index.cshtml`, `KineGestion.Tests/QueryCacheTests.cs`, `KineGestion.Tests/SessionRepositoryIntegrationTests.cs`, `KineGestion.Web.Tests/HomeControllerTests.cs` + `.editorconfig` (nuevo).
- Cambios de código sin commitear (2026-10-20, seed Admin1234): `KineGestion.Web/appsettings.json`, `KineGestion.Web/Program.cs`, `KineGestion.Web/ops/Run-P95-DailyChecklist.ps1`, `KineGestion.Web/ops/Measure-P95-Authenticated.ps1`, `KineGestion.Web.Tests/HomeControllerTests.cs` (+ `appsettings.Development.json` local, NO commitear: gitignored).
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

### Alta prioridad (gráficos / cobranzas)
- [x] **Mejorar módulo Cobranzas (nivel profesional):** **HECHO 2026-10-29** (ver bitácora; export CSV, sin PDF/Excel todavía). rediseño de UI/UX en /Billing/Index (layout con cards superiores, tablas más legibles, filtros avanzados, badges/estados claros). Agregar **gráficos** para análisis (Chart.js/ApexCharts): evolución de cobranza últimos 30/90 días, distribución por estado (Pendiente/Pagado), monto total cobrado vs pendiente, efectividad de cobranza por profesional/sede, top de deudas por paciente. Incluir exportación (PDF/Excel) y métricas agregadas en header (Total pendiente, Total cobrado, % efectividad). Estudiar datos existentes (PaymentStatus, sesiones completadas sin cobrar) y mantener coherencia con arquitectura actual (sin romper tests).
- [ ] **P1 dashboard (propuesta aprobada, pendiente de ejecución):** agenda del día como panel principal (timeline por hora/profesional con estado y CTA) + gráficos en el dashboard (evolución 30 d, distribución por estado, motivos de cancelación) con helper `kgChart` único, paleta leída de tokens CSS, re-render en dark mode y **Chart.js vendorizado en `wwwroot/lib`** (resuelto 2026-10-29: `/Billing/Index` usa copia local + `kg-chart.js`; CSP estricta sigue pendiente). Agregar deltas vs. período anterior (semáforo) y metas/brecha (ya pedido en `PLAN-MEJORA-KPIS-OPERATIVOS.md`).
- [ ] **P2 profesionalización transversal:** `IExportService` reutilizable (hoy el CSV/XLSX vive dentro de `AuditController`) para exportar el dashboard; e2e Playwright del flujo crítico + tests de negocio del dashboard (hoy ningún test habría detectado el mojibake ni el bug de caché por sede); quality gate de cobertura en CI + SAST/secret scanning; extraer Home/Billing a `resx` con test de paridad de claves; CSP estricta, rate limiting en login y MFA admin.
- [ ] **Entidad `Payment`:** los montos del dashboard de cobranzas son **estimados** (`Billing:DefaultSessionAmount`, `BillingController.cs:66`), no cobros reales. Sin `Payment` (montos, parciales, método, recibo) el panel de cobranzas no puede mostrar caja real.
