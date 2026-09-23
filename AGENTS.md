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
- `IDispatchEventRepository.GetByTypeAsync`/`GetByTypePrefixAsync` aceptan `int? limit` opcional y aplican `Take` en SQL (los controllers ya no hacen `.Take()` en memoria). `HomeController` pide 3, `RemindersController`/`BillingFollowUpController` 20.
- `ChangedBy` se trunca a 256 en `KineGestion.Core.AuditActor.Truncate` (aplicado en `HttpContextCurrentUserProvider`, el worker de despacho y `BillingController.LogBillingBatchAsync`).
- `RetentionProtectedEntityNames` fue eliminado de `AuditLogRepository`: la retención purga también las filas legadas de eventos operativos (ya copiadas a `DispatchEvents`/`BillingBatchEvents`).
- `RemindersController.Index` reutiliza el lote de candidatas en memoria para ventanas operativas ≤ `hoursAhead` (evita round-trips por ventana).
- `QueryCache` limpia el `KeyLocks` al invalidar por prefijo o al expirar una entrada.