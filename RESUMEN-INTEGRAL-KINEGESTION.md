# KineGestion — Resumen integral del sistema

Documento consolidado para defensa oral. Cubre arquitectura, modelo de dominio, persistencia, capa Web, flujos end-to-end, rendimiento, seguridad, i18n, infraestructura/CI y tests. Incluye un **análisis por área (qué vender, qué pulir, plan a futuro)**, una matriz de madurez, un **glosario por categorías** y preguntas de jurado.

---

## 1. Qué es y stack

Sistema de gestión para un centro de kinesiología: pacientes, profesionales, tratamientos, sesiones (turnos), consultorios, equipamiento, evolución clínica, cobranzas, recordatorios automáticos al paciente y trazabilidad/auditoría completa.

- **Stack:** ASP.NET Core 8 MVC (Razor) + EF Core 8 + SQL Server 2022. Identity para autenticación. Docker multi-stage. CI en GitHub Actions con SQL Server en contenedor.
- **Proyectos:** `KineGestion.Core`, `KineGestion.Data`, `KineGestion.Web` (producción) + `KineGestion.Tests`, `KineGestion.Web.Tests` (test). Ver `KineGestion.sln`.
- **Puerto dev:** `http://localhost:5138`.
- **Seed:** `admin@kinegestion.com` / `Admin1234` (`KineGestion.Web/Program.cs:402-403`).
- **Imagen:** `ghcr.io/santiago999-hub/kinegestionn` (se publica en push a `main`).

## 2. Arquitectura por capas (Clean / N-layer)

`Core` define contratos; `Data` los implementa; `Web` los orquesta y sirve HTTP. **Core no depende de nadie** (solo BCL).

| Capa | Responsabilidad | Dependencias |
|---|---|---|
| `KineGestion.Core` | Dominio puro: entidades, enums, constantes, DTOs, interfaces, excepciones, servicios de negocio | **ninguna** |
| `KineGestion.Data` | EF Core, `AppDbContext`, repositorios, migraciones, cifrado en reposo, auditoría | Core + EF Core 8 |
| `KineGestion.Web` | MVC, controllers, ViewModels, vistas, middleware, seguridad, i18n, background services | Core + Data |
| `KineGestion.Tests` | Unit de Core/Data + integración SQL | Core + Data |
| `KineGestion.Web.Tests` | Unit de controllers/services Web | Web |

Detalle no obvio: **no hay** `IEntityTypeConfiguration`, ni `AddData()`/`AddRepositories()`, ni `HasData`, ni seed en Data. Todo se centraliza en `AppDbContext.OnModelCreating`, `Program.cs` y los servicios.

## 3. Modelo de dominio (`KineGestion.Core/Entities`)

10 tablas de dominio + Identity.

- **`BaseEntity`**: `CreatedBy`/`UpdatedBy` (default `"system"`), `CreatedAt`/`UpdatedAt` (UtcNow). Heredan Patient, Professional, Treatment, Session, Office, Equipment.
- **`Patient`**: DNI único (7-8 dígitos), `FechaNacimiento` pasada, `ObraSocial`/`Telefono`/`Email` (los 2 últimos cifrados), `IsActivo` (soft-delete).
- **`Professional`**: `Matricula` única, `Especialidad`, `IsActivo`.
- **`Treatment`**: `CantidadSesionesTotales >= 1`, FK `PatientId` (Restrict), `Sesiones`.
- **`Session`** (agregado central): `FechaHora`; `Status` (Pending/Completed/Canceled); `PaymentStatus` (Pending/Paid); `CancellationReason?` + `CancellationObs` + `CancelledAt?`; `Observaciones`; `Evolution` (evolución clínica, cifrada); `InternalNotes` (cifrada, historial de sistema); `EvolutionLockedAt?` (firma/inmutabilidad); `NroSesionEnTratamiento`; `RowVersion` (concurrencia optimista); FKs Patient/Professional/Treatment/Office(`?`).
- **`Office`** (`IsActive`, nombre único, equipamiento y sesiones vinculadas).
- **`Equipment`** (`OfficeId?`, recurso físico del consultorio).
- **`AuditLog`** (no hereda BaseEntity): `EntityName`/`EntityId`/`Action`/`ChangedBy`/`ChangedAt` + `OldValuesJson`/`NewValuesJson`.
- **`BillingBatchEvent`** y **`DispatchEvent`**: eventos operativos de negocio (reemplazan el uso de `AuditLogs` para esos fines).
- **`DispatchJob`** + `enum DispatchJobStatus`: cola durable (outbox) en SQL Server.

**Enums y constantes:**
- `SessionStatus`, `PaymentStatus`, `CancellationReason` (9 motivos), `CancellationTiming` (Early/Late), `AuditEntityType`, `AuditActionType`, `DispatchJobStatus`.
- `DispatchTypes` (`PatientReminder`, `BillingFollowUpPrefix`, `BillingBatchLowEffectivenessAlert`, helper `BillingFollowUp(tier)`), `AuditActor.Truncate` (256), `SessionNotes` (formato único de notas de sistema).

## 4. Persistencia (`KineGestion.Data`)

- **`AppDbContext : IdentityDbContext<IdentityUser>`**; 10 DbSets de dominio. Config EF **100% en `OnModelCreating`**.
- **Soft delete selectivo:** query filter global solo en `Patient`/`Professional`/`Office`. El historial clínico (`Session`) se conserva usando `IgnoreQueryFilters()` explícito en los repositorios.
- **Índices clave:**
  - Únicos: `IX_Patients_DNI`, `IX_Professionals_Matricula`, `IX_Offices_Name`, `IX_Sessions_TreatmentId_NroSesionEnTratamiento`.
  - Performance: `(IsActivo, Apellido, Nombre)` en Patient/Professional; `(ProfessionalId, FechaHora)`, `(Status, FechaHora)`, `(PaymentStatus, FechaHora)`, `(OfficeId, FechaHora)`, `(CancellationReason, FechaHora)` en Session.
  - Cola: `IX_DispatchJobs_SessionId_DispatchType_PayloadHash` **único filtrado** `WHERE [Status] IN (0,1)` → deduplicación de jobs abiertos.
- **Check constraints** (`CK_*`): estado/pago válidos, motivo de cancelación válido, sesiones positivas, fecha de nacimiento pasada, DNI solo dígitos.
- **Cifrado en reposo transparente:** `ValueConverter` estático compartido (purpose `KineGestion.ClinicalData.v1`, prefijo `kg1:`), inicializado una vez con `AppDbContext.SetEncryptionService(...)` (`Program.cs:224`) porque usa `AddDbContextPool`. Campos cifrados: `Patient.Telefono`/`Email`, `Session.Evolution`/`InternalNotes`/`Observaciones`. La auditoría **redacta** esos valores como `"[encriptado]"`.
- **Auditoría automática en `SaveChanges`** (4 overrides): detecta Create/Update/Delete (incluido soft-delete `true → false`), guarda diff old/new JSON, excluye `AuditLog`/`DispatchJob`/`DispatchEvent`/`BillingBatchEvent`/Identity, y corrige PKs temporales en una segunda pasada.
- **Concurrencia:** `Session.RowVersion` (optimista) + inserción en transacción `Serializable` con **3 reintentos** ante deadlock (SQL 1205) + índice único de numeración; `DbUpdateConcurrencyException` se traduce a `BusinessValidationException`.
- **Cola `DispatchJob`:** claim set-based de una sola sentencia `UPDATE TOP (N) ... WITH (UPDLOCK, READPAST)` (lease por `ClaimToken`), reintentos con backoff configurable, limpieza de jobs terminales.
- **21 migraciones.** Destacan `AddBusinessEventTables` (crea `BillingBatchEvents`/`DispatchEvents` y copia el histórico desde `AuditLogs` con `JSON_VALUE`/`TRY_CAST`) y `AddSessionRowVersion`. La última (`AddSoftDeleteGlobalQueryFilters`) es **vacía a propósito** (solo sincroniza metadatos).
- **Repositorios (14):** sesiones partidas en 4 interfaces para Interface Segregation (`ISessionRepository`, `ISessionQueryRepository`, `ISessionMetricsRepository`, `ISessionBatchRepository`, todas sobre `SessionRepositoryBase` con scoping por rol / anti-IDOR). Operaciones destacadas: batch set-based con `ExecuteUpdateAsync`, proyección a DTO con subquery `COUNT(*)` en SQL, y `Take` aplicado en SQL para límites.

## 5. Capa Web (`KineGestion.Web`)

**Pipeline (`Program.cs`)**, en orden: ExceptionHandler + HSTS (no-Dev) → `RequestMetricsMiddleware` → `GlobalExceptionMiddleware` → HttpsRedirection (no-Dev) → Localization → StaticFiles → Routing → profiling inline → CookiePolicy → Authentication → Authorization → profiling 2 → health checks → `/ops/metrics` → ruta MVC.

- **Seguridad:**
  - Identity (`IdentityUser`/`IdentityRole`), password min 8 + dígito, lockout 5 intentos / 10 min.
  - Cookie 8 h HttpOnly, `SameSite` configurable, `SecurePolicy = Always` en producción.
  - **CSRF global** (`AutoValidateAntiforgeryTokenAttribute` como filtro MVC).
  - **4 políticas:** `AdminOnly`, `ClinicalStaff`, `SchedulingStaff`, `ClinicalNotes`.
  - **DataProtection** con key ring persistido en `App_Data/DataProtectionKeys`.
  - `ValidateProductionSafetyConfiguration` falla el arranque ante placeholders `CHANGE_ME`, contraseña admin insegura o reset de contraseña en producción.
- **Controllers (18):** Home (dashboard cacheado 30 s), Sessions (Index/MyAgenda/Create/Edit/Details/Delete/AvailableSlots/AgendaSlots/Reprogram/Cancel), Billing (MarkPaid/Pending individual y por lote → `BillingBatchEvent`), Reminders (ventanas 24h/3h, links firmados con DataProtection, `Respond` anónimo), BillingFollowUp (tiers Soft/Reminder/Firm), DispatchQueue (reintento/cancelación), Users, Audit (filtros + export CSV/Excel), Segmentacion (KPIs por profesional y franja horaria), ReminderFunnel (embudo), Account (login/logout/access denied/profile), Localization (`SetLanguage`), BaseController + CRUD de Patients/Professionals/Offices/Equipments/Treatments.
- **Background services (5):**
  - `ReminderDispatchBackgroundService` (outbox: poll 5 s, lease 120 s, 5 intentos, retención 30 días).
  - `BillingOperationalAlertBackgroundService` (alerta de baja efectividad).
  - `BillingFollowUpAutomationBackgroundService` (D+1/D+3/D+7 sin reenvío).
  - `CacheWarmupBackgroundService` (precalienta 7 queries).
  - `AuditRetentionBackgroundService` (purga > 180 días).
- **Middleware propio:** `RequestMetricsMiddleware` (header `X-Response-Time-Ms`, logs por severidad) y `GlobalExceptionMiddleware` (redirect a `/Home/Error`).
- **Servicios:** `ReminderDispatchQueue` (dedup SHA-256 + índice único), `ReminderDeliveryService` (Email SMTP + WhatsApp API, normaliza teléfono), `BillingOperationalAlertService`, `BillingFollowUpService`/`BillingFollowUpAutomationService`, `IdentityService`, `RequestMetricsStore`, `OperationalConfig` (config acotada con clamps), 3 health checks.
- **29 ViewModels** + `ErrorViewModel`.

## 6. Flujos end-to-end clave

1. **Alta de sesión:** Controller valida ModelState → `SessionService` valida conflicto de profesional (±45 min) y de consultorio → valida límite del tratamiento → `NroSesion = existentes + 1` → repositorio con transacción Serializable/retry → `SaveChanges` genera auditoría → se invalida caché.
2. **Recordatorio:** `RemindersController` obtiene candidatos (24h/3h) → encola `DispatchJob` (con dedup) → worker envía Email/WhatsApp → escribe `DispatchEvent` solo si el envío no lanzó excepción → el paciente responde por link firmado (confirmar/cancelar) con validación de expiración.
3. **Cobranza:** dashboard de efectividad semanal; lote con `ExecuteUpdate` set-based → `BillingBatchEvent`; alerta si 2 semanas consecutivas por debajo del umbral (70%).
4. **Auditoría/export:** filtros por entidad/id/usuario/acción/fecha; export en CSV y XLSX.

## 7. Rendimiento y observabilidad

`AddDbContextPool` (~700-800 usuarios concurrentes), proyecciones DTO, `AsNoTracking`, caché en memoria con locks por clave e invalidación por prefijo (`QueryCache`) con **scoping por rol en la clave** (anti-IDOR), warmup en background, header `X-Response-Time-Ms`, endpoint `/ops/metrics` (Admin), pipeline profiling por path, y scripts de P95 en `ops/`. Health checks: `/health/live` y `/health/ready` (DB + cola).

## 8. Localización

ES (default) / EN. `AddLocalization` + cookie `CookieRequestCultureProvider` (1 año, no HttpOnly a propósito). Helpers estáticos con `ResourceManager`: `LayoutText` (`SharedLayout.resx`) y `AuditText` (`AuditLabels.resx`), con fallback español embebido. Selector ES/EN en `_Layout`.

## 9. Infraestructura y CI/CD

- **Docker:** `Dockerfile` multi-stage (puerto 8080, keyring `/app/keyring`, publish llano sin ReadyToRun); `docker-compose.yml` (SQL Server 2022 + web, imagen GHCR); `.env.example`.
- **CI (`.github/workflows/ci.yml`):**
  - `build-and-unit-tests`: unit Web + core (`FullyQualifiedName!~Integration`).
  - `integration-tests`: SQL Server 2022 en contenedor, `KINEGESTION_TEST_CONNECTION` con `{DatabaseName}`, filtro `~Integration`.
  - `publish-container`: GHCR, solo en push a `main`.

## 10. Tests

`KineGestion.Tests` (Core/Data: unit + integración SQL) y `KineGestion.Web.Tests` (controllers/services). Regla: todo test que toque BD real debe tener `Integration` en el nombre de la clase. El guion de defensa reporta **114/114 en verde** (Core/Data 49 → 53; Web 51 → 61).

## 11. Análisis por área: qué vender, qué pulir, plan a futuro

Criterio de venta: **una fortaleza se cuenta por el problema que resuelve**, no por la tecnología que usa. Y **un límite reconocido + un plan concreto = señal de madurez de ingeniería**, no de debilidad.

### 11.1 Arquitectura y separación de capas
- **Qué vender:** arquitectura en capas limpia (Clean/N-layer) donde **Core no depende de nada**; las reglas de negocio son testeables sin base de datos ni HTTP; se puede cambiar UI, persistencia o proveedores sin reescribir el dominio. Dependencias invertidas por interfaces.
- **Qué pulir:** conviven convenciones (repos separados por Interface Segregation) con algún archivo "legacy" (`[Obsolete] GetAllAsync`, `GetPagedAsync` viejo); no hay contratos de capa formalizados (p. ej. no existe un `IUnitOfWork` explícito ni extensiones `AddData`).
- **Plan a futuro:** consolidar un módulo de composición (`AddData`/`AddApplication`) para estandarizar el DI, eliminar gradualmente los métodos `[Obsolete]` y evaluar *feature folders* o vertical slices para escalar el equipo.

### 11.2 Modelo de dominio y reglas de negocio
- **Qué vender:** el sistema **no es CRUD**. Tiene reglas clínicas reales y verificables: conflicto de agenda por profesional y consultorio con ventana configurable (±45 min), límite de sesiones por tratamiento, **inmutabilidad de la evolución clínica una vez firmada**, numeración secuencial de sesión por tratamiento, motivos de cancelación obligatorios y clasificación temprana/tardía.
- **Qué pulir:** algunas invariantes viven en el servicio (`SessionService`) y no en el agregado; mezcla de idioma en nombres (`Status`/`PaymentStatus` vs. `Nombre`/`Apellido`); la auditoría de firmas de evolución podría ser más explícita (quién firmó y cuándo, en un campo dedicado).
- **Plan a futuro:** mover invariantes al agregado (constructores/factorías con validación), separar "sesión planificada" de "sesión ejecutada" si el negocio lo pide, y agregar firma digital/versionado de la evolución.

### 11.3 Persistencia y datos (EF Core)
- **Qué vender:** integridad llevada a la base: **índices únicos** de negocio (DNI, matrícula, nombre de consultorio, numeración de sesión), **check constraints** de dominio, **soft delete selectivo** con query filters, **cifrado en reposo transparente** por `ValueConverter`, **auditoría automática** en `SaveChanges` con diff old/new JSON, y **concurrencia** optimista (`RowVersion`) + transacción `Serializable` con reintentos ante deadlock. 21 migraciones versionadas.
- **Qué pulir:** el cifrado impide búsquedas SQL sobre esos campos (limitación inherente, hoy no se necesita pero condiciona el futuro); la cola vive en la misma BD de negocio (mezcla carga OLTP con mensajería); i18n de los datos no contemplado; auditoría como tabla JSON (puede crecer mucho → ya hay retención, pero se podría particionar).
- **Plan a futuro:** evaluar cifrado determinístico o *searchable encryption* si se requieren búsquedas sobre PII; mover la cola a broker; particionar/archivar `AuditLogs`; *read replicas* para reportes intensivos.

### 11.4 Seguridad
- **Qué vender:** defensa en profundidad real: Identity con política de password y **lockout**, cookies `HttpOnly`/`SameSite`/`Secure` en prod, **CSRF global** en todos los POST, autorización por **4 políticas de rol**, **anti-IDOR** a nivel de repositorio y de caché (scoping por rol en la clave), **cifrado de datos clínicos y de contacto**, DataProtection con **key ring persistido**, y **fail-fast en producción** ante secretos placeholder. Links de respuesta del paciente **firmados y con expiración**.
- **Qué pulir:** no hay 2FA/MFA; no hay *rate limiting* explícito en login (mitigado por lockout); el key ring en filesystem sirve a 1 nodo (para multi-instancia hay que compartirlo); falta pipeline de escaneo de dependencias/secretos en CI.
- **Plan a futuro:** MFA para perfiles admin, rate limiting por IP/usuario, key ring en almacén compartido (Azure Key Vault / Redis), y *hardening* de headers (CSP estricta).

### 11.5 Mensajería y procesos en segundo plano (outbox)
- **Qué vender:** cola **durable (outbox)** con reintentos y **backoff**, *lease* por token, **deduplicación idempotente** a nivel BD (hash canónico + índice único filtrado), limpieza de jobs terminales, y **tablero de observabilidad** (`/DispatchQueue`) con reintento/cancelación manual y alerta de jobs estancados. Cobertura de recordatorios (24h/3h), cobranzas escalonadas (D+1/D+3/D+7) y alertas operativas, más **retención de auditoría** y **warmup de caché** programados.
- **Qué pulir:** pensado para **un solo consumidor** (no hay *partitioning*/competing consumers); sin **dead-letter queue** explícita (los fallidos quedan en `Failed` para retry manual); sin *idempotencia por ventana* en el productor (hoy dedup por contenido).
- **Plan a futuro:** broker distribuido (Redis/RabbitMQ/Azure Service Bus) con DLQ, idempotencia por (sesión + ventana operativa), y *scheduling* con prioridades.

### 11.6 Frontend y experiencia de usuario
- **Qué vender:** interfaz operativa con dashboard de KPIs, buscadores y filtros persistentes en cookie, **paginación**, estados vacíos, *feedback* de carga y **toasts**; navegación con grupos, **transiciones de página**, **tema claro/oscuro**, **animaciones constantes y accesibles** (respeta `prefers-reduced-motion`), y layout responsive. Selector de idioma en el header.
- **Qué pulir:** UI server-rendered (MVC) → sin actualización en vivo (no hay tiempo real/WebSocket); algunos flujos podrían ser más guiados (asistentes/steppers); i18n de la interfaz parcial (layout + auditoría); sin suite de tests de UI (e2e) formalizada.
- **Plan a futuro:** componentes reactivos en puntos de alta interacción (agenda), notificaciones en vivo, accesibilidad WCAG auditada, y pruebas e2e (Playwright) en CI.

### 11.7 Rendimiento y observabilidad
- **Qué vender:** decisiones con impacto medible: **DbContext pooling** (~700-800 usuarios concurrentes), proyecciones a DTO, `AsNoTracking`, **caché en memoria** con locks por clave e invalidación por prefijo, **warmup** al inicio, **header de latencia** (`X-Response-Time-Ms`), endpoint **`/ops/metrics`**, **profiling por etapa** del request, y **health checks** live/ready para orquestadores. Hay scripts y reportes de P95 en `ops/`.
- **Qué pulir:** la caché es **por proceso** (en multi-instancia cada nodo tiene la suya; la invalidación no se propaga entre nodos); el profiling es "casero" (no hay OpenTelemetry/APM); las métricas no son series temporales (no hay Prometheus/Grafana).
- **Plan a futuro:** caché distribuida (Redis) con invalidación por eventos, **OpenTelemetry** + dashboards (Grafana), y alertas proactivas por SLO.

### 11.8 Localización (i18n)
- **Qué vender:** soporte **ES/EN** con detección por cookie, resources `.resx` y *fallback* español embebido; arquitectura lista para sumar idiomas sin tocar lógica.
- **Qué pulir:** cobertura **parcial** (layout + auditoría); no hay i18n de mensajes de validación ni de otras vistas; no hay validación de completitud de traducciones.
- **Plan a futuro:** extraer **todo** el texto a resources, agregar un test que falle si falta una clave (parity es/en), y sumar idiomas (p. ej. PT).

### 11.9 Testing y CI/CD
- **Qué vender:** doble suite (unit Core/Data + unit Web + **integración sobre SQL Server real en contenedor**), con separación por nombre (`Integration`) para correr unit sin BD; **CI en GitHub Actions** con build Release, tests y **publicación de imagen a GHCR**; reglas de naming que evitan romper el job unitario.
- **Qué pulir:** no hay **cobertura** medida por líneas/ramas ni *quality gate*; los tests de integración pueden ser más lentos; falta escaneo de seguridad (SAST) y de dependencias; no hay entornos de *staging* automatizados.
- **Plan a futuro:** medir cobertura y fijar umbral mínimo, SAST/dependency scanning, *preview environments* por PR, y *smoke tests* de despliegue.

### 11.10 Infraestructura y despliegue
- **Qué vender:** **Dockerfile multi-stage** (imagen liviana, usuario no root, key ring en volumen), **docker-compose** con SQL Server 2022 + web, imagen versionada por `sha` y `latest`, y configuración por variables de entorno (`.env.example`). Listo para correr en un servidor o en un orquestador.
- **Qué pulir:** el deployment es **single-node** (key ring local, cola local); no hay IaC (Terraform/Bicep) ni manifiestos de orquestador (compose no escala horizontal solo); sin *backups*/plan de recuperación documentado en el repo.
- **Plan a futuro:** IaC + orquestador (K8s/ACA), almacenamiento compartido de key ring, política de backups y *disaster recovery*, y multi-sede con partición por tenant.

### 11.11 Matriz de madurez (autoevaluación)

| Área | Madurez | Comentario para la defensa |
|---|---|---|
| Dominio / reglas de negocio | Alta | Diferencial: no es CRUD |
| Persistencia / datos | Alta | Integridad, cifrado, auditoría, concurrencia |
| Seguridad | Alta | Defensa en profundidad; falta MFA/rate limit |
| Mensajería (outbox) | Media-Alta | Confiable en 1 nodo; falta DLQ/broker |
| Rendimiento / observabilidad | Media-Alta | Optimizado y medible; caché por proceso |
| Frontend / UX | Media | Sólida, sin tiempo real ni e2e |
| i18n | Media | ES/EN parcial |
| Testing / CI-CD | Media-Alta | Integración real; falta cobertura/SAST |
| Infraestructura | Media | Docker ok; falta orquestación/IaC |

---

## Glosario (defensa)

Solo palabras y conceptos, agrupados por tema. Objetivo: que puedas nombrar con precisión lo que hiciste y lo que sabés.

### Arquitectura y diseño
- **Arquitectura en capas (N-layer):** organización del código en niveles con responsabilidades separadas (presentación, dominio, datos).
- **Clean Architecture:** regla de dependencias donde el dominio no depende de infraestructura; las dependencias apuntan hacia adentro.
- **Separación de responsabilidades (SoC):** cada módulo hace una sola cosa.
- **Inversión de dependencias (DIP):** depender de interfaces, no de implementaciones.
- **Interface Segregation (ISP):** muchas interfaces específicas en lugar de una genérica.
- **Acoplamiento / cohesión:** dependencia entre módulos / grado de relación interna de un módulo (bajo acoplamiento, alta cohesión).
- **DTO (Data Transfer Object):** objeto plano para transportar datos entre capas.
- **ViewModel:** modelo preparado para la vista.
- **Agregado (aggregate):** grupo de entidades que se tratan como una unidad de consistencia.
- **Invariante:** condición que siempre debe cumplirse en el dominio.
- **Contrato:** interfaz o firma que define cómo se comunican dos partes.
- **Monolito modular:** una sola aplicación desplegable, internamente dividida en módulos.
- **Feature folders / vertical slices:** organizar el código por funcionalidad en lugar de por tipo.
- **Refactorización:** mejorar la estructura interna sin cambiar el comportamiento externo.
- **Deuda técnica:** atajos que ahorran hoy pero cuestan mantenimiento mañana.
- **Trade-off:** decisión con ventajas y desventajas; todo diseño es un compromiso.
- **Legacy:** código heredado, viejo o difícil de cambiar.

### Dominio y negocio
- **Dominio:** el problema del negocio que resuelve el software (kinesiología).
- **Caso de uso:** acción concreta que el usuario realiza con el sistema.
- **Regla de negocio:** restricción del negocio que el sistema hace cumplir (p. ej. no superponer turnos).
- **Entidad:** objeto con identidad propia que persiste en el tiempo.
- **Estado asistencial:** si la sesión fue (Pending/Completed/Canceled).
- **Estado de pago:** si la sesión fue cobrada (Pending/Paid).
- **Ventana de conflicto:** margen de minutos dentro del cual dos turnos se consideran superpuestos.
- **Soft delete:** baja lógica por flag, sin borrar la fila (conserva historial).
- **Hard delete:** borrado físico del registro.
- **Auditoría médico-legal:** registro trazable de quién hizo qué y cuándo, con valor probatorio.
- **Evolución clínica:** nota del profesional sobre la sesión; una vez firmada es inmutable.
- **Firma / bloqueo de evolución:** marcar la evolución como definitiva para que no se altere.
- **Triage / tiering de cobranza:** clasificar deudas por antigüedad y escalar el recordatorio.
- **KPI (Key Performance Indicator):** indicador clave de desempeño.
- **Embudo (funnel):** secuencia de pasos con caída medible (enviado → confirmado → asistido).
- **Tasa de asistencia / confirmación / cancelación:** proporción calculada sobre los envíos.
- **Segmentación:** agrupar datos por criterio (profesional, franja horaria, obra social).
- **Obra social:** cobertura médica del paciente.
- **Recaptura:** recuperar un turno cancelado reprogramándolo.

### Datos y persistencia
- **ORM (Object-Relational Mapping):** traducir objetos a tablas relacionales (EF Core).
- **DbContext:** unidad de trabajo de EF Core que rastrea entidades y guarda cambios.
- **DbContext pooling:** reutilizar instancias de contexto para reducir costo por request.
- **Migración:** cambio versionado y reproducible del esquema de base de datos.
- **Snapshot de modelo:** estado del modelo EF usado para generar el próximo diff de migración.
- **Índice:** estructura que acelera búsquedas (a costa de espacio/escritura).
- **Índice único:** índice que impide valores duplicados (garantiza unicidad de negocio).
- **Índice filtrado:** índice parcial sobre un subconjunto de filas (p. ej. solo jobs abiertos).
- **Check constraint:** restricción de tabla que valida datos a nivel motor.
- **Clave foránea (FK):** vínculo entre tablas con integridad referencial.
- **DeleteBehavior Restrict / SetNull:** qué hace la BD ante el borrado del principal (bloquear / anular FK).
- **Query filter global:** condición aplicada automáticamente a todas las consultas de una entidad.
- **AsNoTracking:** consulta de solo lectura sin rastreo de cambios (más rápida).
- **Proyección:** traer solo las columnas necesarias en lugar de la entidad completa.
- **Paginación:** traer resultados por páginas (`Skip`/`Take`).
- **Subconsulta / set-based:** resolver en una sola sentencia SQL en vez de iterar en memoria.
- **Transacción:** conjunto de operaciones atómicas (todo o nada).
- **Nivel de aislamiento:** cuánto se aíslan las transacciones concurrentes (ReadCommitted, Serializable, etc.).
- **Serializeble:** el nivel más estricto; evita anomalías de concurrencia a costa de bloqueos.
- **Deadlock:** bloqueo mutuo entre transacciones; el motor mata una.
- **Retry / backoff:** reintentar con espera creciente ante fallos transitorios.
- **Concurrencia optimista:** detectar conflictos al guardar, no bloqueando al leer.
- **RowVersion / token de concurrencia:** campo que cambia en cada update y detecta ediciones simultáneas.
- **Cifrado en reposo:** datos cifrados cuando están guardados (at-rest).
- **ValueConverter:** traducción transparente al leer/escribir una propiedad (cifrado).
- **PII (Personally Identifiable Information):** datos personales identificables (DNI, email, teléfono).
- **Soft delete selectivo:** aplicar baja lógica solo a ciertas entidades, conservando el historial clínico.
- **Query filter + IgnoreQueryFilters:** aplicar/omitir el filtro global puntualmente.
- **Read replica:** copia de solo lectura de la base para descargar reportes.
- **Particionado (partitioning):** dividir una tabla grande en segmentos manejables.

### Seguridad
- **Autenticación (authn):** verificar quién es el usuario.
- **Autorización (authz):** verificar qué puede hacer.
- **Identity:** framework de .NET para usuarios, roles, claims y login.
- **Rol:** agrupación de permisos (Admin, Kinesiólogo, Asistente).
- **Claim:** dato sobre el usuario dentro del token (p. ej. ProfessionalId).
- **Política (policy):** regla de autorización reutilizable.
- **Lockout:** bloquear la cuenta tras varios intentos fallidos.
- **Hash de contraseña:** guardar la contraseña transformada, no en claro.
- **Cookie HttpOnly:** cookie no accesible por JavaScript (mitiga XSS).
- **SameSite:** política de envío de cookies entre sitios (mitiga CSRF).
- **Secure flag:** cookie solo viaja por HTTPS.
- **CSRF:** ataque que fuerza acciones autenticadas desde otro sitio.
- **Antiforgery token:** token que valida que el POST vino del propio formulario.
- **XSS:** inyección de scripts en páginas vistas por otros.
- **Inyección SQL:** insertar SQL malicioso en entradas.
- **IDOR:** acceder a datos ajenos manipulando el id; prevención: scoping por rol (anti-IDOR).
- **MFA / 2FA:** segundo factor de autenticación.
- **Rate limiting:** limitar la cantidad de peticiones por tiempo (anti fuerza bruta).
- **DataProtection:** API de .NET para cifrar datos y firmar tokens.
- **Key ring:** conjunto de claves de cifrado de DataProtection.
- **HSTS:** forzar al navegador a usar solo HTTPS.
- **HTTPS redirection:** redirigir HTTP a HTTPS.
- **Fail-fast:** abortar el arranque ante configuración insegura.
- **Defensa en profundidad (defense in depth):** varias capas de seguridad superpuestas.
- **Principio de menor privilegio:** otorgar el mínimo permiso necesario.
- **Búsqueda del secreto (secret scanning):** detectar claves filtradas en el repo.
- **CSP (Content Security Policy):** política que restringe orígenes de scripts/estilos.

### Mensajería y resiliencia
- **Outbox pattern:** guardar el mensaje en la misma BD antes de enviarlo (confiabilidad).
- **Cola (queue):** estructura de trabajos pendientes de procesar.
- **Worker / background service:** proceso que consume la cola en segundo plano.
- **Idempotencia:** repetir la operación no genera efectos extra.
- **Deduplicación (dedup):** evitar procesar/crear duplicados (hash + índice único).
- **Payload / payload hash:** contenido del mensaje / su huella para dedup.
- **Lease:** concesión temporal de un trabajo a un worker.
- **Poison message:** mensaje que siempre falla.
- **DLQ (dead-letter queue):** cola de mensajes que no pudieron procesarse.
- **Backoff exponencial:** aumentar la espera entre reintentos.
- **Circuit breaker:** cortar llamadas a un servicio que falla para no colapsar.
- **Broker (RabbitMQ/Redis/Azure Service Bus):** infraestructura dedicada de mensajería.
- **Competing consumers:** varios workers compitiendo por la misma cola (escala horizontal).
- **Retención (data retention):** política de cuánto tiempo se guardan los datos.

### Rendimiento y observabilidad
- **Latencia:** tiempo que tarda una operación.
- **Throughput:** cantidad de operaciones por unidad de tiempo.
- **P50 / P95 / P99 (percentiles):** el valor por debajo del cual cae el 50/95/99% de las mediciones.
- **Cuello de botella:** punto que limita el rendimiento global.
- **Caché:** guardar resultados para no recalcular.
- **TTL (time-to-live):** tiempo de vida de una entrada de caché.
- **Invalidación de caché:** descartar entradas cuando los datos cambian.
- **Cache stampede:** muchas peticiones recalculando lo mismo a la vez; se evita con locks por clave.
- **Caché distribuida:** caché compartida entre nodos (Redis).
- **Warmup:** precalentar la caché al iniciar.
- **Observabilidad:** capacidad de entender el estado interno del sistema desde afuera.
- **Tracing / trazas:** seguir una petición a través del sistema.
- **Métricas:** números medibles del comportamiento en el tiempo.
- **Log estructurado:** logs en formato parseable (clave-valor/JSON).
- **APM (Application Performance Monitoring):** herramienta de monitoreo de performance.
- **OpenTelemetry:** estándar abierto para trazas, métricas y logs.
- **Health check:** endpoint que reporta si el sistema está sano.
- **Liveness vs. Readiness:** proceso vivo vs. listo para recibir tráfico.
- **SLO / SLA:** objetivo interno de servicio / acuerdo contractual de nivel de servicio.
- **Middleware (pipeline):** etapas por las que pasa cada request.
- **Dashboard:** panel visual de métricas.

### Testing y calidad
- **Test unitario:** prueba una unidad aislada (sin BD/red).
- **Test de integración:** prueba varias partes juntas, incluida infraestructura real.
- **Test end-to-end (e2e):** prueba el flujo completo como un usuario real.
- **Mock / stub:** doble de prueba que simula una dependencia.
- **Fixture:** datos/entorno predefinido para un test.
- **Base de datos efímera:** BD que se crea y destruye por test.
- **Cobertura (coverage):** porcentaje de código ejecutado por los tests.
- **Quality gate:** umbral de calidad que debe pasar el pipeline.
- **Regresión:** un cambio rompe algo que antes funcionaba.
- **CI (Continuous Integration):** integrar y validar cada cambio automáticamente.
- **CD (Continuous Delivery/Deployment):** entregar/desplegar automáticamente.
- **SAST:** análisis estático de seguridad del código.
- **Smoke test:** prueba rápida de que lo esencial funciona tras desplegar.
- **Green / Red:** suites que pasan / fallan.

### Frontend, UX y accesibilidad
- **UX (User Experience):** experiencia de uso.
- **UI (User Interface):** interfaz visual.
- **Responsive:** se adapta a distintos tamaños de pantalla.
- **Mobile-first:** diseñar primero para móvil.
- **Layout / grid / spacing / tipografía:** estructura, rejilla, espaciado y fuentes.
- **Design tokens:** variables de diseño (colores, tamaños) reutilizables.
- **Design system:** conjunto de componentes y reglas visuales.
- **Dark mode:** tema oscuro.
- **Feedback / estado de carga (loading):** indicar al usuario que algo está pasando.
- **Toast / snackbar:** notificación breve y temporal.
- **Empty state:** mensaje cuando no hay datos.
- **Paginación / filtros persistentes:** navegar resultados y recordar criterios.
- **Transición / microinteracción:** animación corta que da respuesta visual.
- **Accesibilidad (a11y):** que el sistema sea usable por todos (contraste, teclado, lectores).
- **WCAG:** estándar internacional de accesibilidad.
- **prefers-reduced-motion:** preferencia del SO de reducir animaciones (hay que respetarla).
- **Contraste:** diferencia de luminancia entre texto y fondo.
- **Server-rendered vs. SPA:** HTML armado en el servidor vs. aplicación en el cliente.
- **Tiempo real / WebSocket:** actualización en vivo sin recargar.
- **i18n / l10n:** internacionalización (preparar para idiomas) / localización (traducir).

### DevOps e infraestructura
- **Docker / imagen / contenedor:** empaquetar la app con sus dependencias para correr en cualquier lado.
- **Multi-stage build:** construir en una etapa e incluir solo lo necesario en la imagen final.
- **docker-compose:** orquestar varios contenedores localmente.
- **Volumen:** almacenamiento persistente entre reinicios del contenedor.
- **Usuario no root:** correr el contenedor con permisos mínimos.
- **Registry (GHCR):** repositorio de imágenes de contenedor.
- **Variable de entorno:** configuración externa al código.
- **Orquestador (K8s/ACA):** plataforma que escala y administra contenedores.
- **Escala horizontal:** agregar más instancias para soportar más carga.
- **IaC (Infrastructure as Code):** infraestructura definida y versionada como código.
- **Staging / Preview environment:** entorno previo a producción por PR.
- **Backup / Disaster Recovery:** copias de seguridad y plan de recuperación.
- **Rollback:** volver a la versión anterior.
- **Multi-tenant:** una instancia sirve a varias organizaciones (sedes).
- **Blue-green / Canary:** estrategias de despliegue sin downtime.

### Gestión del proyecto y defensa (oratoria)
- **Elevator pitch:** resumen de 30 segundos que engancha.
- **Abstract / resumen:** síntesis del trabajo en pocas líneas.
- **Alcance (scope):** qué entra y qué no en el trabajo.
- **Limitación:** lo que el trabajo no cubre o cubre parcialmente.
- **Trabajo futuro:** líneas de continuación concretas.
- **Metodología:** cómo se construyó y validó el trabajo.
- **Evidencia empírica:** resultados medidos, no opiniones.
- **Benchmark:** medición comparativa contra una referencia.
- **Antes/después (before/after):** comparación que muestra la mejora (49→53, 51→61).
- **Cuantificar:** expresar con números (porcentajes, tiempos, cantidades).
- **Roadmap:** plan de evolución por horizontes.
- **MVP (Minimum Viable Product):** versión mínima que aporta valor.
- **Stakeholder:** interesado en el proyecto (jurado, usuario, cliente).
- **MVP vs. producto:** lo mínimo vs. lo completo.
- **Argumento / tesis:** afirmación central que defendés.
- **Objeción:** pregunta crítica del jurado; se responde con datos.
- **Concesión (en oratoria):** reconocer un límite para ganar credibilidad.
- **Storytelling técnico:** contar el proyecto como un problema → solución → resultado.
- **Mensaje clave (takeaway):** lo que el jurado debe recordar.
- **Cierre / call to action:** cierre que propone el siguiente paso.


## Preguntas típicas de jurado (respuesta corta)

1. **¿Cómo evitan doble asignación de sesiones?** Conflicto en servicio + chequeo en repositorio + transacción `Serializable` con retry + índice único `(TreatmentId, NroSesion)`.
2. **¿Trazabilidad médico-legal?** Auditoría automática en `SaveChanges` (actor, fecha, acción, diff JSON); datos clínicos cifrados y redactados en el log.
3. **¿Cómo aseguran que no hubo regresiones?** Suites automatizadas (114/114) + tests de integración contra SQL real en CI.
4. **¿Estrategia de escalado?** Monolito modular optimizado; luego externalizar notificaciones, key ring compartido y cola a broker; multitenant por sede.
5. **¿Qué quedó pendiente?** Modelo de pagos (`Payment`), reportes PDF, portal del paciente, broker distribuido / DLQ.
6. **¿Por qué no usaron un broker de mensajería desde el inicio?** Con un solo nodo, el outbox en SQL da la misma garantía de confiabilidad sin sumar infraestructura; migrar a broker es el paso natural al escalar horizontalmente.
7. **¿Qué pasa si se cae la base de datos?** El sistema no arranca "ready" (readiness = DB + cola), el pool tiene reintentos (`EnableRetryOnFailure`) y el health check lo reporta; no hay pérdida de datos porque los cambios y los mensajes se persisten en la misma transacción de negocio.
8. **¿Cómo garantizan que los datos clínicos no queden expuestos?** Cifrado en reposo de evolución/notas/PII, redacción en la auditoría (`"[encriptado]"`), cookies HttpOnly, autorización por rol y anti-IDOR.
9. **¿Cómo escalarían a varias sedes?** Partición lógica por tenant (columna/tenant id), caché distribuida, cola a broker y key ring compartido; la arquitectura ya lo habilita sin reescribir el dominio.
10. **¿Qué hace que esto sea un aporte y no un CRUD?** Reglas clínicas verificables, integridad transaccional, cifrado + auditoría médico-legal, mensajería confiable con observabilidad, y mejoras de performance medibles con evidencia antes/después.
11. **¿Cuál es la mayor deuda técnica y cómo la encararían?** Caché por proceso y cola single-node; se resuelven con caché distribuida y broker con DLQ, sin tocar las reglas de negocio.
