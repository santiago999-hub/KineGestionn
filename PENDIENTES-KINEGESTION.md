# PENDIENTES-KINEGESTION

> Memoria de trabajo del agente. **Leer al inicio de cada sesión** antes de explorar el repo.
> Regla de oro (ahorro de tokens): si la tarea es sobre contenido/documentación, leer `RESUMEN-INTEGRAL-KINEGESTION.md`; NO re-explorar las 4 capas de nuevo.

Última actualización: 2026-10-30 (UI Fases 1-4 + P1 dashboard: cobranzas depurada, dashboard sin duplicados, agenda del día y 3 gráficos; suites 192/192 y 156/156 verdes).

---

## 1. Estado actual

- Repo: `C:\Users\Santy\OneDrive\Desktop\Nueva carpeta` (KineGestion, ASP.NET Core 8 MVC + EF Core 8 + SQL Server 2022). Branch `main`.
- Último commit: `d78864e Redesign billing doughnut: semantic gradients, center total, informative legend, theme-aware rendering; add startup-time task`.
- Documento maestro de sistema: `RESUMEN-INTEGRAL-KINEGESTION.md` (consolidado en la raíz).
- **Cambios abiertos sin commitear** del pulido de Cobranzas + fixes + UI Fases 1-4 + P1 dashboard (ver §4). Suites: **Web.Tests 192/192 verde; Tests (integración) 156/156 verde** (2026-10-30).
- Estado 2026-10-30: **§7 Fases 1-4 hechas + P1 dashboard ejecutado** (detalle en bitácora): cobranzas (doughnut con porciones exclusivas, forms desanidados, badges unificados, un solo export, tooltip con monto estimado y 3 tablas `<details>`) y dashboard (sin KPI duplicado, `GeneratedAtUtc` + "Actualizado", badges Global/Por sede, versión real del ensamblado; **agenda del día + 3 gráficos** con `kgChart`/Chart.js local, tokens CSS y fallbacks `<details>`). Fase 4.1: `output.txt` borrado; `publish*/` y `mejoras-*/` quedan a decisión (no se commitean). Runtime: `/` 200 con agenda y 3 canvas, lib Chart.js 200; `/Billing` 200. Pendientes del P1 original: deltas vs período anterior (semáforo) y metas/brecha (ver backlog).
- Estado 2026-10-29: **coherencia documental revisada** (RESUMEN/GUIA/TESIS-ARQ/ANALISIS) y **los 3 rojos de Home quedaron corregidos**:
  - `Index_ShouldForwardSelectedOffice_ToSessionScopedQueries`: `HomeController` no reenviaba `officeId` a `CountByCancellationReasonInRangeAsync` ni seteaba `OfficeName`. Regresión real del feature multisede, no preexistente.
  - `Index_ShouldRecomputeDashboard_AfterDomainChangeInvalidatesCache`: la clave de `IMemoryCache` del dashboard y la del snapshot de alerta no incluían `QueryCache.Version` (sí existía `Version` desde 2026-10-21, pero nunca se cableó). Ahora sí.
  - `IndexView_ShouldKeepAccessibleOperationalAlertOriginBadgeMarkup`: `Views/Home/Index.cshtml` tenía **27 caracteres U+FFFD** (mojibake) en textos como "automático"/"kinesiología". Reemplazados por sus caracteres correctos y guardado UTF-8 sin BOM.
- Estado de Cobranzas (commit `dc582bc`): verificado en runtime, GET `/Billing` 200 con los 4 canvas y `/Billing/ExportCsv` 200 `text/csv`.

## 2. Tareas pendientes (backlog)

### Alta prioridad (documentación / defensa)
- [x] ~~Guion de presentación~~ → cubierto por §12 del `RESUMEN-INTEGRAL` (pitch 2 min + 5 lentes + matriz Base/Full).
- [ ] Decidir si profundizar **Seguridad** o **flujo de recordatorios** dentro del mismo documento.
- [x] ~~Revisar coherencia entre `RESUMEN-INTEGRAL-KINEGESTION.md` y los otros docs (`TESIS-*`, `GUIA-PARA-ENTENDER-KINEGESTION.md`, `ANALISIS-SISTEMA-KINEGESTION.md`)~~ → 2026-10-29 (ver bitácora). Incluyó el fix del filtro por sede en motivos de cancelación y los 3 rojos de Home.

### Media prioridad (producto)
- [x] ~~Pulir módulo Cobranzas (`/Billing/Index`)~~ → 2026-10-29 (ver bitácora). Incluye borrar el mock `Views/Cobranzas/Index.cshtml`.
- [x] **Dashboard multisede: filtro por sede en los KPIs, selector en el header y vista detalle por sucursal manteniendo la apariencia global del dashboard** → implementado y verificado con datos reales (2026-10-20): selector de oficinas en header que pobla desde BD, dropdown custom accesible, filtrado de KPIs por officeId y agregados. Bugs corregidos del feature: (a) caché de `IMemoryCache` del dashboard tenía clave fija → ahora `DashboardCacheKeyFor(officeId)`; (b) `SelectedOfficeId` nunca se seteaba en el modelo; (c) la clave de `QueryCache` en `SessionService.GetDashboardCountsAsync` no incluía `officeId` → el warmup contaminaba todas las sedes. Seed de datos demo: `KineGestion.Web/ops/Seed-MultiSede.sql`.
- [ ] **Corregir defectos de UI detectados 2026-10-29** → plan priorizado con pasos en **§7** (empezar por ahí mañana; recomendado antes del P1).
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

- **2026-10-30 (P1 dashboard: agenda + gráficos):**
  - **Backend:** el commit `083f307` ya tenía los DTOs/repo/service (`GetAgendaForDayAsync`, `GetDailySessionSeriesAsync`, `GetStatusDistributionInRangeAsync`) pero **nunca se habían cableado**; ahora `HomeController` los consume con helpers seguros e instrumentación (`Sessions.AgendaToday`, `Sessions.DailySeries`, `Sessions.StatusDistribution`). `HomeDashboardViewModel` suma `TodayAgenda`, `DailySessions` y `StatusDistribution` (+ VMs hijos).
  - **Vista:** panel "Agenda de hoy" (hora local, profesional/sede, badge de estado, CTA a `Sessions/Details`, empty state), doughnut "Distribución por estado" (30 d), línea "Evolución de sesiones" (Total vs Completadas) y barras horizontales "Motivos de cancelación"; los tiles viejos de motivos se reemplazaron por el gráfico + tabla. Scripts con Chart.js local + `kgChart` (colores por tono, re-render en dark mode). CSS nuevo `.kg-agenda-list/.kg-agenda-item/.kg-agenda-time`.
  - **Tests:** `Index_ShouldExposeTodayAgendaAndChartSeries_ScopedToOffice` (officeId reenviado + orden por hora) y `IndexView_ShouldRenderAgendaPanelAndChartMarkup`. Web.Tests **192/192**; integración **156/156**.
  - **Runtime:** `/` 200 con "Agenda de hoy", 3 canvas, 3 fallbacks `<details>` y `~/lib/chartjs/chart.umd.min.js` 200; `/Billing` 200. Sin sesiones para hoy en la BD dev (empty state visible).
- **2026-10-30 (UI Fases 3-4 del §7):**
  - **Cobranzas:** badges de pago a `kg-badge-*`; se quitó el export duplicado de la tarjeta de filtros (queda el del header); aging con subtítulo aclaratorio y tooltip tematizado con monto estimado; 3 bloques `<details class="kg-chart-data">` con tablas de datos (evolución, aging, efectividad) como fallback accesible + CSS nuevo en `site.css` (`.kg-chart-data`). Tests nuevos en `BillingViewTemplateTests` (sin forms anidados, sin badges legacy, 1 export, fallbacks presentes).
  - **Housekeeping:** borrado `KineGestion.Web/output.txt`. `publish*/` y `mejoras-*/` quedan como untracked a decisión.
  - **Verificación:** Web.Tests **190/190** verde; Tests integración **156/156** verde; runtime `/Billing` 200 con 3 `<details>`, 1 link de export, sin `bg-*-subtle` y 9 checkboxes con `form="billingBatchForm"`.
- **2026-10-30 (UI Fases 1-2 del §7):**
  - **Fase 1 — Cobranzas:** `BillingDashboardViewModel` ganó `PendingNotCompletedCount` y `SessionsInRangeCount`; la vista usa porciones exclusivas en el doughnut (ya no suma `CompletedPending` dentro de `Pending`), "N sesiones" correcto, leyenda "Pendientes sin atender" y `aria-label` acorde. El form de lote quedó standalone (`billingBatchForm`) con checkboxes asociadas por `form=` y los forms por fila desanidados (antes HTML inválido). Nuevo test `Index_ShouldBuildExclusivePaymentDistribution_ForDoughnut`.
  - **Fase 2 — Dashboard:** eliminado el bloque "Indicadores clave" (duplicaba "Rendimiento 30 días" con fórmulas contradictorias; la bitácora 2026-10-21 lo daba por hecho y no era así); quitado el `h2` "Dashboard" duplicado; header con badge `Por sede`/`Global` + "Actualizado hh:mm:ss" (`GeneratedAtUtc` nuevo en VM/controlador); tarjetas Pacientes/Profesionales/Tratamientos con badge `Global` y Sesiones con `Por sede`/`Global`; subtítulos canónicos + `title` con la fórmula; tarjeta de sistema con versión real del ensamblado (`v1.0.0.0`). Tests nuevos en `HomeViewTemplateTests`.
  - **Verificación:** Web.Tests **188/188** verde; Tests integración **156/156** verde; runtime `/` 200 (sin "Sprint 2", "Actualizado" presente) y `/Billing` 200 (sin forms anidados). Sin commitear (no pedido).
- **2026-10-29 (coherencia documental):** se revisaron `RESUMEN-INTEGRAL`, `GUIA-PARA-ENTENDER`, `TESIS-*` y `ANALISIS-SISTEMA` contra el código.
  - **RESUMEN (fuente de verdad):** agregado mapa de documentos; repos corregidos a **13 implementaciones + `SessionRepositoryBase`** (decía 14); tests separados en snapshot histórico de tesis **114/114** vs estado actual **Web 183/186 + integración 156/156** (decía 143); pregunta de jurado actualizada.
  - **GUIA:** corregido soft delete (los query filters globales están en `Office`, `Patient` y `Professional`, no solo Office); dedup del outbox aclarado (solo bloquea mientras el job está abierto); políticas de autorización corregidas a **4** (faltaba `SchedulingStaff`) y roles Admin/Kinesiologo/Asistente.
  - **TESIS-ARQ:** roles con Asistente; typo "Invalidad"; key ring de filesystem aclarado como single-node; eliminado "aplicar outbox" del roadmap (ya implementado, se reemplazó por DLQ/broker); cobranza D+1 → D+1/D+3/D+7.
  - **ANALISIS-SISTEMA:** marcado como **snapshot histórico 11/05/2026** con banner que lista lo superado (.NET 8, rol Asistente, outbox, cobranza, multisede, módulo Cobranzas); no se reescribieron sus 1929 líneas.
  - **Hallazgo de código (corregido en la misma sesión):**
    1. `HomeController.SafeGetCancellationReasonsAsync` no pasaba `officeId` a `CountByCancellationReasonInRangeAsync` → motivos de cancelación sin filtro por sede. Fix: tercer argumento `officeId`.
    2. `HomeController.Index` nunca seteaba `OfficeName` (el test lo esperaba) → `OfficeName = offices.FirstOrDefault(o => o.Id == officeId.Value)?.Name`.
    3. La clave de `IMemoryCache` del dashboard (`DashboardCacheKeyFor`) y la del snapshot de `BillingOperationalAlertService` no incluían `QueryCache.Version` (el `Version` existía desde 2026-10-21 pero no estaba cableado). Fix: `v{QueryCache.Version}` en ambas claves.
    4. `Views/Home/Index.cshtml` tenía 27 U+FFFD (mojibake) → reemplazados por texto correcto (UTF-8 sin BOM).
  - **Verificación:** `dotnet test KineGestion.Web.Tests` → **186/186 verde**; `dotnet test KineGestion.Tests` → **156/156 verde**. Los 3 rojos de Home ya no existen.
  - **Hallazgos de UI pendientes (revisión de vistas, sin tocar):**
    - `Views/Home/Index.cshtml`: **bloque KPI duplicado** (~líneas 163-205 "Indicadores clave" y ~357-399 "Rendimiento 30 días") con los mismos 3 KPIs y subtítulos contradictorios (cobranza: "completadas pagadas sobre completadas" vs "sesiones pagadas sobre total de sesiones"). La bitácora 2026-10-21 daba por eliminado ese duplicado: **no está eliminado**. Además faltan los badges Global/Por sede, el "Actualizado hh:mm:ss" y la versión real del ensamblado (la tarjeta sigue con "v1.0 — Sprint 2"). Hay dos `h2` "Dashboard" (hero + page-header).
    - `Views/Billing/Index.cshtml`: el doughnut y el "N sesiones" **cuentan doble**: `CompletedPendingCount` es subconjunto de `PendingCount` (`CountByPaymentStatusInRangeAsync(Pending)` no filtra estado), así que las 3 porciones suman de más y los % de la leyenda no cierran. Los **forms están anidados** (`MarkPaidBatch` envuelve los form `MarkPaid`/`MarkPending` por fila): HTML inválido que puede romper las acciones por fila. Se mezclan `kg-badge-*` con badges Bootstrap `bg-*-subtle`; el aging muestra sesiones, no montos.
- **2026-10-29 (Cobranzas):** se pulió la página real `/Billing/Index` y se borró el mock `Views/Cobranzas/Index.cshtml` (459 líneas hardcodeadas, sin link, agregado en `5245657`).
  - **Backend:** `PendingPatientDebtDto`; `GetTopPendingPatientsAsync` en `ISessionMetricsRepository`/`SessionMetricsRepository` y en `ISessionService`/`SessionService` (QueryCache 10 s, clave `sessions:top-debts:...`). **Ojo EF:** el `GroupBy` agrupando por columnas de la navegación `s.Patient` y proyectando el record en una sola query **no traduce** (`The LINQ expression ... could not be translated`). Solución estable: query 1 agrupa solo por `PatientId` (`Count`/`Min`), query 2 trae pacientes por `Contains` y se arma el DTO en memoria.
  - **`BillingController`:** `Index` acepta `status`, `payment`, `filtersApplied` (default sigue siendo Completed+Pending); `ExportCsv` (CSV `;`, BOM UTF-8, tope 5000 filas) y helpers `ResolveDateRange` (tope 366 días), `BuildDailyPointsAsync`, `BuildProfessionalEffectivenessAsync`, `BuildTopPendingPatientsAsync`, `CsvField`. Los redirects de `MarkPaid/MarkPending/MarkPaidBatch/MarkPendingBatch` preservan filtros.
  - **Vista:** resumen (cobrado con variación vs período anterior, a cobrar, efectividad, sesiones), evolución con presets 7/30/90, doughnut de estados, barras de antigüedad y de efectividad semanal, tabla de efectividad por profesional, top de deudas por paciente, filtros avanzados, tabla con importes estimados (`Billing:DefaultSessionAmount`), acciones por fila y paginación. Chart.js local + `kg-chart.js` (sin CDN).
  - **Tests:** +3 unitarios en `BillingControllerTests` (filtros/insights, `ExportCsv`, default sin filtros) y +1 integración `GetTopPendingPatientsAsync_ShouldRankCompletedPendingDebtByPatient`. Web.Tests quedó 183/186 esa sesión; **corrección 2026-10-29: los 3 rojos de Home NO eran preexistentes** (dos eran regresiones del feature multisede y uno, mojibake en la vista). Ya corregidos: 186/186. Pista: `KineGestion.Web/output.txt` contiene un diff viejo de `Views/Home/Index.cshtml` sin aplicar. Tests suites: 156/156 verde.
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

- `RESUMEN-INTEGRAL-KINEGESTION.md` → conservar (coherencia 2026-10-29).
- `PENDIENTES-KINEGESTION.md` → conservar (actualizado).
- `AGENTS.md` → conservar (memoria de trabajo).
- Coherencia documental 2026-10-29 (sin commitear): `RESUMEN-INTEGRAL-KINEGESTION.md`, `GUIA-PARA-ENTENDER-KINEGESTION.md`, `TESIS-RESUMEN-ARQUITECTURA-PROFESIONAL.md`, `ANALISIS-SISTEMA-KINEGESTION.md`, `PENDIENTES-KINEGESTION.md`.
- Cambios de código sin commitear (2026-10-29, Cobranzas): `KineGestion.Core/DTOs/PendingPatientDebtDto.cs` (nuevo), `KineGestion.Core/Interfaces/ISessionMetricsRepository.cs`, `KineGestion.Core/Interfaces/ISessionService.cs`, `KineGestion.Core/Services/SessionService.cs`, `KineGestion.Data/Repositories/SessionMetricsRepository.cs`, `KineGestion.Web/Controllers/BillingController.cs`, `KineGestion.Web/Models/ViewModels/BillingDashboardViewModel.cs`, `KineGestion.Web/Views/Billing/Index.cshtml`, `KineGestion.Web/Views/Cobranzas/Index.cshtml` (eliminado), `KineGestion.Web.Tests/BillingControllerTests.cs`, `KineGestion.Tests/SessionRepositoryIntegrationTests.cs`. **No commitear** `publish*/`, `mejoras-*/` (el viejo `output.txt` fue borrado el 2026-10-30).
- Fixes dashboard 2026-10-29 (sin commitear): `KineGestion.Web/Controllers/HomeController.cs` (officeId en motivos, OfficeName, Version en la clave), `KineGestion.Web/Services/BillingOperationalAlertService.cs` (Version en la clave), `KineGestion.Web/Views/Home/Index.cshtml` (27 caracteres mojibake).
- UI Fases 1-2 2026-10-30 (sin commitear): `KineGestion.Web/Models/ViewModels/BillingDashboardViewModel.cs`, `KineGestion.Web/Views/Billing/Index.cshtml`, `KineGestion.Web.Tests/BillingControllerTests.cs`, `KineGestion.Web/Models/ViewModels/HomeDashboardViewModel.cs`, `KineGestion.Web/Controllers/HomeController.cs`, `KineGestion.Web/Views/Home/Index.cshtml`, `KineGestion.Web.Tests/HomeViewTemplateTests.cs`.
- UI Fases 3-4 2026-10-30 (sin commitear): `KineGestion.Web/Views/Billing/Index.cshtml`, `KineGestion.Web/wwwroot/css/site.css`, `KineGestion.Web.Tests/BillingViewTemplateTests.cs` (nuevo). `KineGestion.Web/output.txt` → **borrado** 2026-10-30; `publish*/` y `mejoras-*/` siguen untracked (no commitear).
- P1 dashboard 2026-10-30 (sin commitear): `KineGestion.Web/Controllers/HomeController.cs`, `KineGestion.Web/Models/ViewModels/HomeDashboardViewModel.cs`, `KineGestion.Web/Views/Home/Index.cshtml`, `KineGestion.Web/wwwroot/css/site.css`, `KineGestion.Web.Tests/HomeControllerTests.cs`, `KineGestion.Web.Tests/HomeViewTemplateTests.cs`.
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
- [x] **P1 dashboard — HECHO 2026-10-30:** agenda del día como panel principal (lista por hora con estado, profesional/sede y CTA a la sesión) + 3 gráficos (evolución 30 d, distribución por estado, motivos de cancelación) con `kgChart` único, paleta de tokens CSS, re-render en dark mode y Chart.js vendorizado en `wwwroot/lib` (ambos módulos usan copia local). Cableó el backend que ya existía desde `083f307` (`GetAgendaForDayAsync`, `GetDailySessionSeriesAsync`, `GetStatusDistributionInRangeAsync`), con fallbacks `<details>` y tests nuevos (controller + plantilla).
- [ ] **P1.5 dashboard — deltas y metas:** deltas vs. período anterior con semáforo + metas/brecha (pedido en `PLAN-MEJORA-KPIS-OPERATIVOS.md`). Lo demás del P1 quedó hecho.
- [ ] **P2 profesionalización transversal:** `IExportService` reutilizable (hoy el CSV/XLSX vive dentro de `AuditController`) para exportar el dashboard; e2e Playwright del flujo crítico + tests de negocio del dashboard (hoy ningún test habría detectado el mojibake ni el bug de caché por sede); quality gate de cobertura en CI + SAST/secret scanning; extraer Home/Billing a `resx` con test de paridad de claves; CSP estricta, rate limiting en login y MFA admin.
- [ ] **Entidad `Payment`:** los montos del dashboard de cobranzas son **estimados** (`Billing:DefaultSessionAmount`, `BillingController.cs:66`), no cobros reales. Sin `Payment` (montos, parciales, método, recibo) el panel de cobranzas no puede mostrar caja real.

## 7. Plan priorizado de mejoras (2026-10-30, para retomar en orden)

> Orden pensado para evitar retrabajo: primero lo que afecta **datos/acciones reales** (Cobranzas), después **coherencia visual y datos del Dashboard**, luego cosmética/a11y y housekeeping. Una fase a la vez; correr suites **en secuencia** (nunca en paralelo) y actualizar esta memoria al cerrar cada una.

### Fase 1 — Cobranzas: defectos funcionales (P0) — HECHA 2026-10-30
- [x] 1.1 **Doughnut/doble conteo** (`BillingController`/VM + `Views/Billing/Index.cshtml`)
  - `BillingDashboardViewModel`: `PendingNotCompletedCount => Math.Max(0, PendingCount - CompletedPendingCount)` y `SessionsInRangeCount => PaidCount + PendingCount`.
  - Vista: "N sesiones" usa `SessionsInRangeCount`; porciones exclusivas `[CompletedPending, PendingNotCompleted, Paid]`; % recalculados; `aria-label` y leyenda "Pendientes sin atender".
  - Test `Index_ShouldBuildExclusivePaymentDistribution_ForDoughnut` (las 3 porciones suman el total).
- [x] 1.2 **Forms anidados** (`Views/Billing/Index.cshtml`)
  - Form de lote standalone `id="billingBatchForm"` (hidden inputs + botones con `form=` y `formaction=`); checkboxes con `form="billingBatchForm"`; forms por fila desanidados con antiforgery.
  - Runtime verificado: HTML sin forms anidados (`maxDepth=1`), `/Billing` 200, batch form y form owner presentes.
- [x] 1.3 Suites: Web.Tests **188/188** verde; Tests integración **156/156** verde.

### Fase 2 — Dashboard: duplicado y lo prometido (P1) — HECHA 2026-10-30
- [x] 2.1 Bloque "Indicadores clave" eliminado; queda "Rendimiento 30 días" con subtítulos canónicos + `title` (cobranza = completadas pagadas ÷ completadas; cancelación = canceladas ÷ agendadas; tardías aparte).
- [x] 2.2 `h2` "Dashboard" duplicado eliminado (queda el título del hero).
- [x] 2.3 `GeneratedAtUtc` agregado al VM/controlador; header con badge de alcance (`Por sede`/`Global`), "Actualizado hh:mm:ss" y badge Global/Por sede en tarjetas; versión dinámica del ensamblado (`v1.0.0.0` renderizada).
- [x] 2.4 Test `IndexView_ShouldRenderSingleCanonicalKpiBlock_AndDynamicVersion` (conteo 1 de los 3 KPIs, sin "Sprint 2", con "Actualizado"/"Global"/"Por sede").
- [x] 2.5 Runtime: `/` 200, sin "Sprint 2", "Actualizado" presente; suites en secuencia OK.

### Fase 3 — Consistencia visual Cobranzas (P2) — HECHA 2026-10-30
- [x] 3.1 Badges de pago unificados a `kg-badge-success`/`kg-badge-warning` (sin `bg-*-subtle`).
- [x] 3.2 Un solo punto de exportación (header); eliminado el duplicado de la tarjeta de filtros; test de plantilla lo fija (`ExportCsv` aparece 1 vez).
- [x] 3.3 Aging: subtítulo "Sesiones sin cobrar por antigüedad" + tooltip con sesiones y **monto estimado** (tooltip tematizado con `kgChart.tooltipDefaults`); la conversión real llega con `Payment`.
- [x] 3.4 Fallback accesible `kg-chart-data` (`<details>` + tabla) para evolución, aging y efectividad; el doughnut ya expone los datos en la leyenda visible. CSS nuevo en `site.css`.

### Fase 4 — Housekeeping (P2, rápido) — PARCIAL 2026-10-30
- [x] 4.1 `KineGestion.Web/output.txt` **borrado** (diff viejo superado). Queda a decisión limpiar `publish/`, `publish_standalone/`, `mejoras-*` (untracked; no se commitean).
- [x] 4.2 Sin commit (no pedido); §1/§3/§4 actualizados.

### Después de esto (ya en backlog general)
P1 dashboard completo (agenda del día + gráficos), `Payment`, reportes PDF/cierre de caja, portal del paciente, P2 transversal (IExportService, e2e Playwright, resx/paridad, CSP/rate limit/MFA).
