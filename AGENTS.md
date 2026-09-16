# AGENTS.md

Proyecto: KineGestion (ASP.NET Core + EF Core + SQL Server). No escribir comentarios salvo que se pidan.

## Comandos
- Build Web: `dotnet build KineGestion.Web/KineGestion.Web.csproj`
- Suites de tests (NUNCA en paralelo entre sí: lock en `KineGestion.Core.dll`, CS2012; correr en secuencia):
  1. `dotnet test KineGestion.Web.Tests/KineGestion.Web.Tests.csproj --no-restore`
  2. `dotnet test KineGestion.Tests/KineGestion.Tests.csproj --no-restore` (integración sobre SQL Express `localhost\SQLEXPRESS`, BD efímera por test; noblear `KINEGESTION_TEST_CONNECTION` con `{DatabaseName}`)
- Migraciones local: `dotnet ef database update --project KineGestion.Data --startup-project KineGestion.Web` (NO usar `-v q`: se parsea como migración destino). BD dev `KineGestionDB`.
- Push a `origin main` una vez verdes y commiteado.

## Contexto reciente
- Los eventos operativos de negocio ya no se escriben como `AuditLogs`: ahora van a `BillingBatchEvents` y `DispatchEvents` (migración `AddBusinessEventTables` copió el histórico desde `AuditLogs`; opción A de retención queda como red de seguridad).
- `DispatchEvent.DispatchType`: `PatientReminder` | `BillingFollowUp:<Tier>` | `BillingBatchLowEffectivenessAlert`. El worker escribe 1 fila solo cuando `SendAsync` no lanza; la alerta usa `SessionId=null` y ahora `FechaHora=nowUtc.Date` (hash determinista por día → dedup de `DispatchJob` abiertos con el índice único `(SessionId, DispatchType, PayloadHash)`).
- Repos nuevos: fecha desde → `>= fecha.Date`; fecha hasta → `< fecha.Date.AddDays(1)`.
- Errores de envío truncados a 2000 (2 mensajes max, `ReminderDispatchQueue.BuildErrorsSummary`); `FilterSearch` truncado a 200 en `LogBillingBatchAsync`.
- Los `DispatchType` están centralizados en `KineGestion.Core.DispatchTypes` (constantes `PatientReminder`, `BillingFollowUpPrefix`, `BillingBatchLowEffectivenessAlert` + helper `BillingFollowUp(tier)`); no usar literales en código nuevo.

## Pendiente para la próxima sesión (deuda media/baja, prioridad alta ya resuelta)
1. Límite de filas con parámetro en `GetByTypeAsync`/`GetByTypePrefixAsync` (los controllers hacen `.Take()` en memoria: `RemindersController.cs:101`, `BillingFollowUpController`, `.Take(3)` en `HomeController`).
2. `ChangedBy` sin truncar (columna 256) — si `User.Identity.Name` largo, perdería por lossless en la cola/eventos.
3. `RetentionProtectedEntityNames` en `AuditLogRepository` quedó obsoleto: las 4 entidades de negocio ya no escriben auditoría; evaluar limpiarlo.