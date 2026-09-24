# Reporte de perfilado: dónde se va el tiempo y prioridades - 2026-09-23

## Objetivo
Identificar con datos qué partes del sistema concentran el tiempo, para priorizar mejoras de rendimiento con evidencia (perfilado, no percepción).

## Método
1. **dotnet-trace** (`dotnet-trace collect -p <pid>`): traza de CPU durante el benchmark. Las pilas quedaron truncadas/en parte sin símbolos en esta captura (archivo sin footer; `report topN` no pudo leerla completa). La traza `trace-dbfocus-live.nettrace` previa (30/05) ya mostraba que la app es **I/O-bound**: domina la espera (ManualResetEventSlim/Monitor/Task.InternalWait) y la única cadena activa es EF → DB.
2. **Log de comandos EF Core** (Information): la app ya registra cada `Executed DbCommand (Nms)` → ranking real de consultas por duración acumulada.
3. **Benchmark autenticado** (Measure-P95-Authenticated.ps1) + pipeline profile (auth/action/render) mientras se capturaba.

## Benchmark (Release, local, 12 iteraciones + 2 warmup por ruta)
| Ruta | Cold (ms) | Warm p50 (ms) | Warm p95 (ms) | Max (ms) |
|---|--:|--:|--:|--:|
| / | 119 | 98 | 111 | 119 |
| /Sessions | 63 | 87 | 368 (outlier) | 368 |
| /Patients | 80 | 116 | 140 | 140 |
| /Billing | 93 | 97 | 144 | 144 |
| /Reminders | 93 | 109 | 137 | 137 |
| /Users | 89 | 91 | 370 (outlier) | 370 |

Warm p50 entre 87 y 116 ms: aceptable en caliente. Los outliers (~370 ms) corresponden a 1er hit post-arranque / GC, no representativos.

## Top de consultas SQL por tiempo acumulado (142 comandos capturados en el run)
| Query | Runs | Total ms | Max ms |
|---|---|---|---:|
| `COUNT(*) FROM Sessions` | 19 | 125 | 22 |
| `UPDATE DispatchJobs` (claim del worker, subquery por fila) | 4 | 157 | 152 |
| Roles/claims de Identity por request (`AspNetRoles` join) | 4 | 136 | 130 |
| Listado Sessions filtrado (paginado admin) | 2 | 54 | 31 |
| Candidatas de recordatorio | 14 | 40 | 17 |
| `COUNT(*) FROM AspNetUsers` | 14 | 28 | 7 |
| `COUNT(*) FROM DispatchJobs` | 5 | 21 | 5 |
| Detalle Sesion (detail) | 1 | 18 | 18 |

## Conclusiones (prioridades con evidencia)
1. **Dashboard (`HomeController`)**: 19 `COUNT(*)` sobre Sessions → ~125 ms de query por carga. El fix de mayor ROI ya identificado: **consolidar los conteos en 1-2 lecturas agregadas** (GROUP BY / una sola query) o cachear el modelo entero 30 s (el MemoryCache ya existe para el dashboard).
2. **Claim del worker (DispatchJobRepository, subquery+UPDATE)**: 1 UPDATE aislado de 152 ms. Convolver hacia índice (Status, NextAttemptAtUtc) + hints UPDLOCK/READPAST (o Channel wake-up que reduce la frecuencia del claim).
3. **Roles/claims Identity 130 ms puntual (frío)**: es una consulta fría de caché; no sistémico pero cachear los claims del usuario en el `ICurrentUserProvider` o reducir validaciones por request bajara los outliers de /Users y /Sessions (370 ms). %.
4. El resto de las consultas (listados, candidatas, detalle) está en 5-30 ms: **no optimizar**.

## Notas de herramienta
- `dotnet-trace report topN` exige el archivo .nettrace completo (con footer); un `collect` interrumpido no es analizable por `report` (aunque `convert` a speedscope tolera lo parcial).
- Para una tesis, la evidencia de **command log de EF + pipeline profile** es más directa que las pilas truncadas; PerfView daría el desglose por método si se quiere profundizar.

## Cambios ya aplicados en esta sesión (commit 6ba3a43)
- `ReadyToRun` en el Dockerfile (menor arranque en frío en hosting). Nota: este flag se REVERTIRÍA después (ver seguimiento: rompía el job de GHCR).
- `Observability:PipelineProfilePaths` ampliado a `/Sessions,/Patients,/Billing,/Reminders,/Users`.
- Warmup de caché extendido a `patients:paged:first` y `patients:select:active`.

## Seguimiento 2026-09-24 (resolución de prioridades 1-3)
1. **Dashboard consolidado (prioridad 1) — RESUELTO** (`b03b541`): los 19 `COUNT(*)` quedaron en un solo `GetDashboardCountsAsync` (SELECT agregado con `COUNT(CASE WHEN...)`), cacheado 10 s. Evidencia: Web.Tests 181/181 y KineGestion.Tests 139/139 verdes tras el cambio.
2. **Claim del worker (prioridad 2) — RESUELTO** (`fa17840`): el bucle fila por fila de `batchSize` UPDATE correlacionados quedó en UNA sola sentencia `UPDATE TOP (N)` con subquery `FROM DispatchJobs x WITH (UPDLOCK, READPAST)` ordenada por `CreatedAtUtc` → 1 round-trip (antes hasta N). La subquery re-evalúa la elegibilidad dentro del UPDATE, así que conserva la atómica del claim y el lease. Evidencia: `ClaimNextBatchAsync_ShouldClaimPendingInOrder_AndSkipClaimedJobs` (orden, skip dentro del lease y re-claim tras vencer) pasa contra SQL Express real en la suite 139/139.
3. **Roles/claims Identity (prioridad 3) — DESCARTADA sin código**: los índices ya existen (`IX_AspNetRoleClaims_RoleId`, `IX_AspNetUserClaims_UserId`, `IX_AspNetUserRoles_RoleId`, PK `(UserId, RoleId)`). El pico de 130 ms corresponde a frío de primer arranque (compilación de queries EF + páginas en cache y primeros hits de sesión), consistente con los outliers de ~370 ms en `/Users` y `/Sessions`. No se añade un índice redundante; no hay acción estructural pendiente.
4. **Resto 5-30 ms — SIN TOCAR**, como marca la prioridad 4 original.