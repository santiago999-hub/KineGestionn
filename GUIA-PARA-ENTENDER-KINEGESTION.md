# GUÍA PARA ENTENDER KINEGESTION
## Qué pasa en cada parte y cómo razonar el sistema completo

**Prerrequisito:** esta guía asume que ya conocés los conceptos técnicos del resumen de aprendizaje (C# y LINQ/async-await, HTTP y MVC, Entity Framework Core, inyección de dependencias, ASP.NET Identity, Middleware, workers de fondo, tests, SQL Server). No necesitás saber nada de KineGestion antes de leerla: acá se explica qué hace cada pieza de este código en particular y *por qué* está diseñada así.

**Cómo leer esta guía:** cada sección sigue la misma lógica: (1) qué existe, (2) qué pasa cuando se ejecuta, (3) por qué se decidió así. Si entendés el "por qué", el código deja de ser una lista de archivos y se vuelve una historia coherente.

---

## 1. MAPA MENTAL DEL SISTEMA

```
 HTML/CSS/JS  (navegador del usuario)
      │  HTTP (GET/POST)
      ▼
┌─────────────────────────────────────────────┐
│  KineGestion.Web  ← capa de PRESENTACIÓN + OPERACIÓN │
│  Middlewares → Controllers → ViewModels → Vistas Razor │
│  Services de web (email, colas, workers, health checks) │
└──────┬─────────────────────┬──────────────────┘
       │  inyecta interfaces │  usa interfaces
       ▼                     ▼
┌──────────────────┐  ┌──────────────────┐
│ KineGestion.Core │  │ KineGestion.Data │
│ entidades, DTOs, │  │ AppDbContext,    │
│ contratos (interf.)│ │ repos, migraciones│
│ services de negocio│ │ (EF Core + SQL)  │
└──────────────────┘  └────────┬─────────┘
                               ▼
                     SQL Server (KineGestionDB)
```

**La regla de dependencias (lo más importante para entender todo):**
- `KineGestion.Core` **no conoce** ni a la web ni a la base de datos. Solo define: entidades (Patient, Session…), contratos (interfaces como `IPatientRepository`, `ISessionService`), DTOs y la lógica de negocio pura.
- `KineGestion.Data` conoce a Core (usa sus entidades) y a SQL Server.
- `KineGestion.Web` conoce a Core y a Data. Es la única que sabe que existe HTTP, cookies, email, etc.
- Resultado: si mañana cambiara SQL Server por PostgreSQL, solo se toca `Data`. Si cambiara la web por una API, solo se toca `Web`.

**El "pegamento" es `KineGestion.Web/Program.cs`.** Es el único lugar donde se registran todas las piezas en el contenedor de **inyección de dependencias** (DI). Ahí se declara: "cada vez que alguien pida `IPatientRepository`, dale un `PatientRepository`". Nadie en el código instancia nada con `new` para estas piezas; todas llegan por constructor.

---

## 2. QUÉ PASA CUANDO UN USUARIO HACE UN REQUEST

Tomá el ejemplo "el admin abre la lista de sesiones" (`GET /Sessions`). Esto es lo que pasa en cada parte:

### 2.1. `Program.cs` — se arma el pipeline HTTP
Al arrancar, el programa registra en orden estricto los **middlewares**. Un middleware es una función que envuelve la siguiente; cada request los atraviesa como una cebolla. Orden real (Program.cs:241-333):

```
RequestMetricsMiddleware   → mide duración y registra métricas
GlobalExceptionMiddleware → try/catch de todo el request: si algo explota sin
                            poder, redirige a /Home/Error con mensaje amable
UseRequestLocalization    → detecta idioma (es/en) y lo fija para la request
UseStaticFiles            → sirve CSS/JS/imágenes directamente
UseRouting                → resuelve qué Controller/Action matchea la URL
UseCookiePolicy           → aplica SameSite/Secure global a todas las cookies
UseAuthentication         → DESENCRIPTA la cookie de sesión y carga el usuario (claims)
UseAuthorization          → verifica los [Authorize(Roles=...)] de la acción
controlador (mvc)         → ejecuta el código de la acción
```

**Análisis importante:** el orden importa. Si ponés `UseAuthorization` antes de `UseAuthentication`, no hay usuario todavía y todo se rechaza. El perfilado opcional de pipeline (mide tiempos de cada etapa) está ahí para detectar dónde se gasta el tiempo (auth, acción o render de la vista).

### 2.2. `SessionsController.Index` — la capa de presentación
`SessionsController` (KineGestion.Web/Controllers/SessionsController.cs) está decorado con `[Authorize(Roles = "Admin,Kinesiologo,Asistente")]`: sin login con esos roles, el request ni entra a este método.

Qué hace la acción `Index` (SessionsController.cs:44-99), paso a paso:
1. **Restaura filtros guardados** en una cookie JSON (`kg.sessions.index.filters`) si el usuario entró sin query string → el usuario vuelve a la página y encuentra sus últimos filtros. Por eso se usa cookie `HttpOnly` con expiración de 14 días.
2. **Valida los parámetros**: `page >= 1`, `pageSize` entre 5 y 50. Esto es "defensa en profundidad": el servicio también valida, pero acá se evita pedir 1.000.000 de filas.
3. **Llama al servicio**: `_sessionService.GetPagedListForAdminAsync(...)`. El controller NO sabe nada de SQL; solo pide "pagina 1, 10 items, filtro estado=Pending".
4. **Mapea DTO → ViewModel** con `SessionViewModel.FromDto(...)`. El ViewModel es la forma "lista para la vista": strings formateadas, `SelectListItem`s, etc. Convierte datos crudos en lo que el HTML necesita.
5. **Guarda el tiempo de la acción** en `HttpContext.Items["kg.pipeline.actionMs"]` para que el perfilador del pipeline lo use (encadenamiento de partes a través de `HttpContext.Items`).
6. **Devuelve la vista** `View(model)` → Razor genera HTML.

**Dos patrones de la misma categoría que conviene distinguir:**
- En *entrada* (del navegador hacia la BD): el **Model Binding** toma el form/query string y construye automáticamente el `SessionViewModel`. Si algo no matchea, `ModelState.IsValid` es falso y la acción retorna el formulario con errores.
- En *salida* (de la BD hacia el navegador): DTO → ViewModel → HTML. Nunca se pasa una entidad de BD directo a la vista.

### 2.3. `SessionService` — la lógica de negocio (Core)
El servicio es donde "viven las reglas". En `SessionService.GetPagedListForAdminAsync` no hay SQL: por eso llama a un **QueryCache** primero:

```
QueryCache.GetOrCreateAsync(clave, factory, ttl=8s)
```

Cómo razona `QueryCache` (KineGestion.Core/Services/QueryCache.cs):
1. Si hay un valor en el caché en memoria (`ConcurrentDictionary`) que no expiró (8 segundos), lo devuelve sin tocar la BD.
2. Si no, usa un `SemaphoreSlim` por clave para que **solo un request** compute el valor y los demás esperen (evita "thundering herd": 10 requests cache-miss golpeando la BD a la vez).
3. Al guardar, cada servicio de escritura invalida el prefijo `"sessions:"` → la data queda vieja como mucho 8 segundos, pero jamás "endre entre caché y BD".

**Clave de caché con "scope por rol"** (SessionService.cs:530-537): la clave incluye si el usuario es Admin (`:all`) o el `ProfessionalId` (`:prof:5`). Esto es una protección de **IDOR** (Insecure Direct Object Reference): si a un kinesiólogo se le cacheara el listado de otro profesional, al reusar la clave mezclaría datos de pacientes ajenos. Incluir el scope en la clave hace imposible esa filtración cruzada.

### 2.4. `SessionQueryRepository` — acceso a datos (Data)
Ya dentro de Data, el repositorio arma un `IQueryable` (LINQ que aún NO se ejecutó; EF lo compila a SQL recién cuando se llama `CountAsync`/`ToListAsync`). Lo clave (SessionQueryRepository.cs:72-132):
- `.AsNoTracking()`: EF no registra las entidades en el ChangeTracker → menos memoria, porque acá solo leemos.
- **Filtro por rol (IDOR de nuevo)**: `GetProfessionalIdFilter()` devuelve el `ProfessionalId` del claim del usuario; si no es Admin, se agrega `WHERE ProfessionalId = X` **en la consulta SQL, no en C#**. La restricción se aplica en la base, no "confiando" en que el controller lo haga.
- **Proyección directa a DTO** (`Select(s => new SessionListDto(...))`): en vez de traer pacientes/tratamientos completos, la consulta SQL trae solo `Apellido, Nombre` concatenados. Menos bytes de red y menos memoria. Además **no se carga `Evolution`** en el listado (dato sensible innecesario).
- **Paginación en SQL**: `.Skip((page-1)*pageSize).Take(pageSize)` → la BD hace `OFFSET/FETCH`, no trae las 100.000 filas para filtrar en memoria.

### 2.5. EF Core → SQL Server → respuesta
EF traduce `IQueryable` a SQL con parámetros (`@__term`, etc.) → **es imposible la inyección SQL** porque nada se concatena textual. La BD usa los índices definidos para responder rápido, devuelve las filas, y el request regresa por el mismo camino en la vista Razor hasta el navegador.

**Flujo completo del ejemplo:**
```
GET /Sessions
 → AuthMiddleware lee cookie, carga claims
 → Authorize verifica rol Admin
 → SessionsController.Index valida y arma ViewModel
 → SessionService aplica caché de 8s
 → SessionQueryRepository: WHERE + OFFSET/FETCH + SELECT de solo lo necesario
 → SQL Server con índices
 → DTOs → ViewModel → Razor → HTML
```

---

## 3. EL CORAZÓN DE DATOS: `AppDbContext`
## Qué pasa solo con guardar (SaveChanges)

`AppDbContext` (KineGestion.Data/Context/AppDbContext.cs) es el único punto de contacto con la base. Además de definir tablas, hace **cuatro cosas automáticas** que son las que más sorprenden al leer el sistema:

### 3.1. Auditoría automática (sin código en los controllers)
Cada vez que alguien llama `SaveChangesAsync`, el contexto sobreescribe ese método y ejecuta en secreto (AppDbContext.cs:235-264):

1. `PrepareAuditEntries()`:
   - `ApplyAuditInfo()`: a toda entidad `BaseEntity` que se agrega/modifica le setea `UpdatedAt = now`, `UpdatedBy = actor`, y si es nueva `CreatedAt`/`CreatedBy`. El actor se obtiene de `ICurrentUserProvider` (que en el request es `HttpContextCurrentUserProvider`, que lee `User.Identity.Name`). Fuera de un request (ej. workers), cae en un provider fallback que devuelve `"system"`.
   - `AddAuditLogs()`: recorre el `ChangeTracker`, y por cada entidad de negocio modificada crea un `AuditLog` con `Action = Create/Update/Delete`, `OldValuesJson` y `NewValuesJson` (valores antes/después serializados).
2. `base.SaveChanges()`: inserta las entidades.
3. **Detalle fino**: si la entidad era nueva (ID temporal/memoria), el audit log primero se guarda con el ID temporal; después de la inserción real, `PersistPendingAuditEntityIds` hace un **segundo** `SaveChanges` solo para corregir el `EntityId` del audit log con el ID real. Dos saves en vez de uno, para que la trazabilidad apunte al registro correcto.

**Análisis del diseño:**
- La auditoría vive en una sola capa (el `SaveChanges`). Ningún controller "debe acordarse" de auditar; es imposible olvidarse.
- Los campos sensibles (Evolution, notas internas, teléfono, email — los que tienen *value converter* de encriptación) se guardan en el audit como `"[encriptado]"`: el audit log registra *que hubo un cambio* sin filtrar PII en claro.
- Se excluyen de la auditoría `DispatchJob`, `DispatchEvent`, `BillingBatchEvent`, `AuditLog` e `IdentityUser` (ruido de sistema).

### 3.2. Encriptación transparente de PII
`DataProtectionEncryptionService` es un **ValueConverter de EF Core**: se configuró en `Session.Evolution`, `InternalNotes`, `Observaciones`, `Patient.Telefono`, `Patient.Email`. El converter aplica: al escribir → `Encrypt(valor)`; al leer → `Decrypt(valor)`. En la base, los datos están cifrados; en memoria (para el usuario) están legibles. La app no sabe que encripta: es transparente a nivel de contexto.

### 3.3. Consistencia e índices (AppDbContext.cs:48-233)
- **UNIQUE**: `Patient.DNI`, `Professional.Matricula`, `(TreatmentId, NroSesionEnTratamiento)` → el número de sesión por tratamiento no puede repetirse, aunque dos requests corran en paralelo.
- **Índices compuestos** que matchean las consultas reales: `(ProfessionalId, FechaHora)`, `(OfficeId, FechaHora)`, `(Status, FechaHora)`, `(PaymentStatus, FechaHora)`, `(CancellationReason, FechaHora)`, `(EntityName, EntityId, ChangedAt)`.
- **CheckConstraints en la BD** (no solo validación en C#): DNI solo dígitos, fecha de nacimiento en el pasado, `Status` solo 0/1/2, número de sesión >= 1, `CantidadSesionesTotales >= 1`. Esto es "barrera final": aunque un bug de aplicación intente guardar datos inválidos, SQL lo rechaza.
- **DeleteBehavior.Restrict** en las relaciones de Session→Patient/Professional/Treatment: no se puede borrar un paciente que tiene sesiones (preserva historial clínico). Office usa `SetNull` (si se borra un consultorio, las sesiones quedan sin consultorio pero se conservan).

### 3.4. Soft Delete
Las entidades de la clínica tienen `IsActivo`/`IsActive` (borrado lógico). `Office` tiene **Query Filter global** (`Where IsActive = true`) aplicado automáticamente por EF en TODAS las consultas. Patient/Professional la aplican en sus repositorios. Borrar = marcar flag; el historial clínico queda intacto. La auditoría detecta ese cambio como `Action = "Delete"` (soft delete transition, AppDbContext.cs:478-486).

---

## 4. CÓMO RAZONA LA LÓGICA DE NEGOCIO: `SessionService`

`SessionService` es el mejor ejemplo para aprender a "leer análisis lógico" en este sistema. Cuatro casos:

### 4.1. Crear sesión con detección de conflictos (CreateAsync, SessionService.cs:408-427)
Pasos:
1. `ValidateProfessionalAvailabilityAsync`: pregunta al repositorio si el profesional ya tiene una sesión dentro de la ventana de ±45 minutos (configurable vía `Scheduling:ProfessionalConflictWindowMinutes`). Si la tiene → lanza `BusinessValidationException`.
2. `ValidateOfficeAvailabilityAsync`: lo mismo para el consultorio, PERO ignora sesiones canceladas (una cancelada "libera el turno" del consultorio). En cambio, el conflicto por profesional sí cuenta la cancelada contra el buffer. Esto no es un error: es una **decisión de negocio** documentada en el repositorio. Un profesional no puede tener dos turnos en 45 minutos aun si uno quedó cancelado tarde (quizá el paciente igual apareció); un consultorio sí puede reasignarse.
3. Cuenta sesiones existentes del tratamiento, valida contra el límite planificado y asigna `NroSesionEnTratamiento = sesiones + 1`.

Notá el patrón de capas: **validación (service) → persistencia (repository) → integridad (BD)**. Si dos admins agregan sesión 8 y 9 a la vez, el service calcula bien, pero el índice único `(TreatmentId, NroSesionEnTratamiento)` en BD es el que detecta la colisión real entre procesos concurrentes.

### 4.2. Inmutabilidad de la evolución clínica (UpdateAsync, SessionService.cs:429-474)
Regla regulatoria: la evolución, una vez escrita, no se modifica.
- Si la sesión original tiene `EvolutionLockedAt` seteado y la evolución cambió → excepción.
- Si es la primera vez que se escribe la evolución → se setea `EvolutionLockedAt = UtcNow` automáticamente.

O sea: no existe el estado "evolución escrita pero no bloqueada". Escribir es firmar. El bloqueo es automático, no un botón que el usuario pueda olvidar.

### 4.3. Recaptura (reprogramación) de una sesión cancelada (ReprogramAsync, SessionService.cs:245-280)
- Solo se puede reprogramar una sesión `Canceled`.
- Valida conflictos de horario del nuevo turno.
- Copia la ficha clínica (paciente, profesional, tratamiento, observaciones) y crea una NUEVA sesión con el siguiente número de tratamiento. La sesión cancelada no se "edita": queda como evento histórico, la nueva es un registro distinto (trazabilidad completa).

### 4.4. Confirmar/cancelar por recordatorio (ConfirmByReminderAsync / CancelByReminderAsync, SessionService.cs:180-218)
Son acciones **idempotentes** y a prueba de replays:
- Confirmar sobre una sesión ya completada → no-op (reprocesar un link no rompe nada).
- Confirmar una sesión cancelada → excepción.
- Cancelar una sesión cancelada → no-op.
- Cancelar una sesión completada → excepción ("no se puede cancelar una sesión ya atendida: preserva cobranza y KPIs").
¿Por qué importa? El link llega en un email y puede clickearse varias veces (o alguien lo reenvía, o expiró pero el worker reintentó). El sistema asume que el input puede repetirse o llegar tarde y lo hace **inofensivo**. Cada acción escribe una nota interna (`SessionNotes`) para que el funnel operativo pueda medir la respuesta del paciente.

**Lección general:** cada método del service demuestra la cadena *validar → mutar → persistir → invalidar caché*. La invalidación de caché con `QueryCache.InvalidatePrefix("sessions:")` es el cuarto paso obligatorio: sin él, las pantallas mostrarían datos viejos.

---

## 5. LOS TRABAJADORES DE FONDO (workers) Y LA COLA DURABLE

Hay 5 `BackgroundService`s registrados en Program.cs:94-98. El más importante para entender el diseño del sistema es el de recordatorios. Toda la funcionalidad operativa (recordatorios de turnos, cobranza, alertas) se basa en un patrón llamado **outbox**.

### 5.1. El problema que resuelve
Enviar un email/WhatsApp es lento y puede fallar (SMTP caído, red, API de terceros). Si el envío se hiciera *dentro* del request HTTP, el admin quedaría esperando 10 segundos o el proceso web podría morir a mitad de envío y el paciente nunca recibiría su recordatorio. Además, dos clicks podrían enviar dos veces el mismo recordatorio.

### 5.2. La solución: persistir el trabajo antes de hacerlo
Flujo (ReminderDispatchQueue.cs → DispatchJobRepository → ReminderDispatchBackgroundService):

```
Admin selecciona sesiones y aprieta "Enviar recordatorios"
   │
   ▼                                  (lo único que hace el request HTTP)
RemindersController.DispatchSelected
   │  por cada sesión: crea ReminderDispatchWorkItem (payload)
   ▼
ReminderDispatchQueue.QueueAsync
   │  1. calcula SHA256 del contenido del trabajo (PayloadHash)
   │  2. pregunta: ¿ya hay un job abierto con (SessionId, DispatchType, Hash)?
   │     → sí: no hace nada (dedup, adiós doble-click)
   │  3. INSERT DispatchJob (Status=Pending) en la base  ← ANTES de enviar nada
   ▼
Se responde al admin "ya fue encolado". El request termina rápido.
```

El worker `ReminderDispatchBackgroundService` (ReminderDispatchQueue.cs:132-315), cada 5 segundos en su propio bucle:
1. Pide un lote de hasta 10 jobs elegibles con `ClaimNextBatchAsync`.
2. **Claim atómico**: marca cada fila como `Processing` con un `ClaimToken` único (GUID). El subquery SQL elige la fila más antigua elegible; `ExecuteUpdateAsync` setea el token en la misma operación atómica → si hubiera otro worker (o el mismo proceso reiniciado) no re-clama la misma fila. Es una **lease**: si el worker muere a mitad de envío, la fila queda `Processing` con `ClaimedAtUtc` viejo, y pasada la duración del lease (120 s) vuelve a ser elegible (condición `Processing && ClaimedAtUtc < leaseCutoff`).
3. Deserializa el payload, llama a `ReminderDeliveryService.SendAsync` (email vía SMTP + WhatsApp vía HTTP a una API configurada).
4. Si el envío **no lanzó excepción**, escribe un `DispatchEvent` (histórico: qué se envió, por qué canal, errores truncados a 2000 chars). El evento se escribe SOLO cuando `SendAsync` no lanza → si el SMS falló parcialmente, queda el error en `Errors`, pero el intento queda registrado.
5. Marca el job `Succeeded` con el resultado serializado. Si lanzó excepción → `MarkFailedAsync`: suma 1 intento; si llegó al máximo (5), pasa a `Failed` terminal; si no, vuelve a `Pending` con `NextAttemptAtUtc = ahora + retry` (backoff base de 60 s).
6. Cada ~100 ciclos de pulsos vacíos, borra jobs terminales viejos (retención configurable, 30 días).

### 5.3. Los dos niveles de deduplicación
- **Nivel BD (filtro único)**: índice único filtrado `(SessionId, DispatchType, PayloadHash) WHERE Status IN (0,1)` (AppDbContext.cs:207-209). Dos productores concurrentes que encolan lo mismo: el primero inserta, el segundo choca contra el índice y cae en `DbUpdateException`, que se trata en `QueueAsync` (ReminderDispatchQueue.cs:72-88): relanza solo si realmente no existía.
- **Nivel hash (re-encolado)**: el hash no incluye timestamps ni actor, así que reencolar "el mismo contenido" después de que el job ya terminó tampoco crea un duplicado mientras haya uno abierto.

**Por qué todo esto es "operativo" y no "técnico":** la garantía que se busca es *at-least-once* con control de duplicados, y contabilidad de cada intento (quién lo disparó, cuándo, resultado). Eso es lo que permite luego medir efectividad (funnel) sin adivinar.

---

## 6. LOS PIPELINES OPERATIVOS: recordatorios y cobranza

### 6.1. Recordatorios de turno (PatientReminder)
1. `RemindersController.Index` pide "candidatas" a `SessionService.GetReminderCandidatesAsync` (próximas 24 h, solo `Status = Pending`).
2. Para cada una arma un **link confirmar/cancelar firmado**: `DataProtector` de ASP.NET cifra `sessionId|action|expiry|sessionStart`. El token expira a los 2 días **o al momento de iniciarse el turno, lo que ocurra antes** (RemindersController.cs:362-377). Un paciente no puede confirmar/cancelar un turno que ya pasó. El token es `[AllowAnonymous]`: el paciente NO tiene cuenta; se autentica con el propio token.
3. Se encola el trabajo (outbox de la sección 5) y el worker lo envía.
4. El paciente clickea → `RemindersController.Respond` valida token y expiración → `SessionService.Confirm/CancelByReminder` (idempotente, sección 4.4) → se muestra página de resultado.

### 6.2. Cobranza D+1 (BillingFollowUp)
`BillingFollowUpAutomationBackgroundService` corre cada 6 horas (configurable). Su lógica (BillingFollowUpService):
1. `SessionQueryRepository.GetBillingFollowUpCandidatesAsync` busca sesiones `Completed` con `PaymentStatus = Pending` según antigüedad.
2. Clasifica por **tier o escalón** según días de deuda: **D+1 Suave (1-2 días) → D+3 Recordatorio (3-6 días) → D+7 Firme (7+ días)**, cada uno con plantillas de email/WhatsApp propias. La escalada evita bombardear desde el día 1 con lenguaje firme.
3. `DispatchJobRepository.GetDispatchedBillingTypesAsync` consulta qué niveles ya se enviaron para cada sesión → no se vuelve a enviar el mismo nivel. Encola solo los no enviados.

### 6.3. Alerta operativa de baja efectividad (BillingBatchLowEffectivenessAlert)
`BillingOperationalAlertBackgroundService` chequea cada hora un indicador: si el porcentaje de efectividad de los lotes de cobranza de semanas consecutivas bajó del umbral, encola un `DispatchJob` con `DispatchType = "BillingBatchLowEffectivenessAlert"` y `SessionId = null`. El hash determinista usa `FechaHora = nowUtc.Date` (el día, no el instante) para que el índice único deduplique a nivel de día: la alerta se dispara una sola vez por día aunque haya varios ciclos.

### 6.4. Por qué existen 4 tablas y no una
- `DispatchJob` = **trabajo pendiente** (cola). Estado transitorio; se limpia.
- `DispatchEvent` = **histórico de envíos** (contabilidad). Nunca se borra hace poco.
- `BillingBatchEvent` = **histórico de operaciones masivas de facturación** (cuántas filas se marcaron pagadas/saltadas y con qué filtros).
- `AuditLog` = auditoría de cambios de entidades clínicas (capa de trazabilidad general).

Separarlos permite que el "historial" no ralentice la cola y viceversa; además cada uno tiene su propia política de retención.

---

## 7. SEGURIDAD PUNTA A PUNTA

Capas, de afuera hacia adentro:
1. **HTTPS en producción** (`UseHttpsRedirection` + cookie `SecurePolicy.Always`).
2. **Autenticación** (ASP.NET Identity): la cookie es `HttpOnly` + `SameSite=Lax`, dura 8 horas, con bloqueo de 10 min tras 5 intentos fallidos. Contraseña mínima 8 chars con dígito.
3. **Autorización por rol**: atributos `[Authorize]` + políticas en `Program.cs:193-199` (`AdminOnly`, `ClinicalStaff`, `ClinicalNotes`). Acciones específicas exigen Admin (`Delete` de sesiones, `RemindersController` entero).
4. **IDOR (acceso a datos ajenos)**: el filtro `ProfessionalId` se aplica en el **repositorio** (SQL), no solo en el controller, y se duplica en la clave de caché (sección 2.3). La página `MyAgenda` ni siquiera toma el profesional de la URL: lo saca del **claim** del usuario autenticado (`SessionsController.cs:123`).
5. **Anti-CSRF**: todo POST tiene `[ValidateAntiForgeryToken]` + token de antiforgery en el form. Como la cookie antiforgery usa Data Protection con key ring en disco compartible, funciona en multi-instancia.
6. **XSS**: Razor escapa todo por defecto; las notas clínicas se muestran con el escaping estándar.
7. **SQL Injection**: imposible por parámetros de EF.
8. **PII en claro en BD**: nunca — encriptada por ValueConverter (sección 3.2). Y el audit redacta los campos encriptados como `[encriptado]`.
9. **Seguridad defensiva de config (Program.cs:439-483)**: en producción, si la cadena de conexión tiene `CHANGE_ME`/placeholders o la contraseña admin es el default, el proceso **no arranca** y lanza error. Y `ValidateAntiForgeryToken` exige `SameSite=Lax` mínimo en `UseCookiePolicy`.

**Dos defensas contra "el mismo bloque" observadas**: la validación está en el service (regla de negocio) Y en la BD (constraints/índices). El código asume que ninguna capa es perfecta y pone doble candado.

---

## 8. RENDIMIENTO: LAS TÉCNICAS Y SU RAZONAMIENTO

Cada técnica responde a un costo medido:
- **DbContextPool** (Program.cs:80): los DbContext son caros de crear (metadata, listeners). Reutilizarlos entre requests sube el techo de usuarios concurrentes (~700-800).
- **AsNoTracking en lecturas**: sin reparar en el ChangeTracker, menos CPU/memoria; solo se rastrea lo que se va a modificar.
- **Proyecciones a DTO**: el SQL `SELECT` trae solo las columnas mostradas. La mayor baja de memoria/red viene de aquí.
- **QueryCache en memoria** con TTL cortos (8-10 s) e invalidación por prefijo: pantallas calientes casi sin tocar la BD, sin riesgo de datos muy viejos.
- **Índices que matchean los WHERE/ORDER BY reales** del código (sección 3.3). P.ej., el conflicto de horarios usa `(ProfessionalId, FechaHora)`; por eso el chequeo es rápido aunque haya 100K sesiones.
- **Paginación real en SQL** (OFFSET/FETCH) en lugar de traer todo.
- **`ExecuteUpdateAsync`/`ExecuteDeleteAsync`** (EF Core 7+) para las operaciones del worker: actualizan/borran filas directamente en SQL sin cargar entidades — clave en el claim y los retries que corren cada pocos segundos.

---

## 9. EL REPOSITORIO COMO CONCEPTO (POR QUÉ 13 Y NO 1)

Pasó algo importante: `SessionRepository` no lo hace todo. Se separaron en 4 (SessionRepository.cs:16-21 lo documenta):
- `SessionRepository` → CRUD + chequeos de conflicto (integridad transaccional, `AddAsync` con transacción Serializable y retry de deadlock).
- `SessionQueryRepository` → **lecturas para pantallas** (listados paginados con filtro por rol y proyección DTO) y candidatos de pipelines.
- `SessionMetricsRepository` → **KPIs** (contadores y segmentos agregados).
- `SessionBatchRepository` → **operaciones masivas** (marcar muchas como pagadas de una).

Razón de diseño: cada patrón de acceso tiene requisitos opuestos (lecturas livianas vs. escrituras con transacción vs. agregaciones por rangos). Mezclarlos obligaría a un repositorio gigante donde un bug de lectura afectaría a las escrituras. Separarlos permite que cada clase tenga una razón para existir y un único tipo de carga.

Otro ejemplo de "análisis lógico para leer": `SessionRepositoryBase` centraliza lo común (el filtro `GetProfessionalIdFilter()` que también usa el QueryRepository). Eso **evita que el fix de una vulnerabilidad IDOR se aplique en dos lugares y se olvide el segundo**. Diseñar para que el esfuerzo de una corrección sea único es una regla clave de este código.

---

## 10. RESUMEN: LAS 8 REGLAS DE ORO DEL ANÁLISIS LÓGICO DE ESTE SISTEMA

Si memorizás estas reglas, podés abrir cualquier archivo y predecir qué vas a encontrar:

1. **Flujo único en 4 saltos**: Controller (valida input) → Service (reglas de negocio) → Repository (SQL/EF) → BD. Nunca un controller toca la BD ni un repository valida "negocio".
2. **Todo muta → persiste → invalida caché**: cada método de escritura termina con `QueryCache.InvalidatePrefix(...)`.
3. **El usuario detrás de cada cambio**: `ICurrentUserProvider` da el actor para memoria (audit) y para autorización a nivel de datos (IDOR). Sin user → "system".
4. **Repositorio ≡ permiso de datos**: el filtro por rol vive en el SQL del repositorio y en la clave de caché, nunca confiando solo en el controller.
5. **Doble candado**: regla en service + constraint/índice único en BD (deadlock, concurrencia y datos inválidos se gestionan en SQL).
6. **Los trabajos NO se hacen en el request**: se persisten primero (outbox), se procesan en worker con lease, retry, dedup y contabilidad en eventos.
7. **Toda acción externa (enviar email/WhatsApp) asume que puede fallar y que el input puede repetirse**: por eso idempotencia, truncamiento de errores y retries con backoff.
8. **Separación por "carga"**: lectura vs. escritura vs. batch vs. KPI → clases y tablas distintas, cada una con su política.

Con estas reglas, leer `AppDbContext` (sorpresas automáticas), `Program.cs` (pegamento), los services (reglas) y los workers (operaciones diferidas) es una sola historia: *un sistema clínico que garantiza integridad, trazabilidad y confiabilidad operativa incluso cuando el mundo exterior (SMTP, WhatsApp, concurrencia) falla*.