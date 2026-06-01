# Resumen profesional de arquitectura y funcionamiento - KineGestion

Fecha: 01/06/2026

## 1) Mapa general del sistema

KineGestion esta implementado con una arquitectura en capas tipo Clean Architecture / N-layer, con separacion clara de responsabilidades:

1. Capa Web (KineGestion.Web)
- ASP.NET Core MVC
- Controllers, ViewModels, Vistas Razor, middleware, seguridad e i18n.

2. Capa Core (KineGestion.Core)
- Reglas de negocio, servicios de dominio, DTOs, interfaces, excepciones.
- No depende de Web ni de Data.

3. Capa Data (KineGestion.Data)
- Persistencia con Entity Framework Core + SQL Server.
- AppDbContext, configuracion de entidades, repositorios y transacciones.

4. Capa de pruebas
- KineGestion.Tests (Core/Data)
- KineGestion.Web.Tests (Web)

Este desacople permite evolucionar UI, reglas y acceso a datos sin reescribir todo el sistema.

## 2) Arquitectura por capas: que hace cada una y como se conectan

### 2.1 Web Layer
Responsabilidad:
- Recibir requests HTTP.
- Validar entrada de usuario.
- Llamar a servicios de Core.
- Devolver vistas y mensajes.

Caracteristicas importantes:
- Seguridad por roles (Admin, Kinesiologo).
- Localizacion ES/EN.
- Endpoints operativos (/health/live, /health/ready, /ops/metrics).
- Middleware para excepciones globales y metricas de request.

Ejemplo de composicion en startup:
~~~csharp
builder.Services.AddDbContextPool<AppDbContext>(...);
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddIdentity<IdentityUser, IdentityRole>(...);
app.MapHealthChecks("/health/live", ...);
app.MapHealthChecks("/health/ready", ...);
app.MapGet("/ops/metrics", ...).RequireAuthorization(...);
~~~

### 2.2 Core Layer
Responsabilidad:
- Aplicar reglas de negocio.
- Aislar casos de uso de detalles de infraestructura.

Ejemplos de reglas reales:
- No permitir turnos superpuestos para el mismo profesional en una ventana configurable.
- No exceder sesiones totales de un tratamiento.
- Bloquear modificacion de evolucion clinica firmada.
- Invalidad caches tras operaciones de escritura.

Ejemplo simplificado de regla de conflicto horario:
~~~csharp
bool hasConflict = await _repository.ExistsProfessionalConflictAsync(
    professionalId,
    fechaHora,
    windowInMinutes: _professionalConflictWindowMinutes,
    excludeSessionId: excludeSessionId);

if (hasConflict)
    throw new BusinessValidationException("El profesional ya tiene una sesion...", nameof(Session.FechaHora));
~~~

### 2.3 Data Layer
Responsabilidad:
- Traducir operaciones del dominio a SQL via EF Core.
- Definir constraints e indices para integridad y performance.
- Garantizar consistencia bajo concurrencia.

Puntos tecnicos clave:
- Indice unico en Sessions(TreatmentId, NroSesionEnTratamiento).
- Check constraints para enums y validaciones criticas.
- DeleteBehavior restrictivo para proteger historial clinico.
- Insercion de Session con transaccion Serializable + retry ante deadlocks.

Ejemplo de insercion concurrente segura:
~~~csharp
await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

int sesionesActuales = await _context.Sessions.CountAsync(s => s.TreatmentId == session.TreatmentId);
session.NroSesionEnTratamiento = sesionesActuales + 1;

_context.Sessions.Add(session);
await _context.SaveChangesAsync();
await tx.CommitAsync();
~~~

## 3) Modelo de dominio (entidades y criterio funcional)

1. Patient
- Identidad clinica y administrativa del paciente.
- DNI unico, fecha de nacimiento validada, soft-delete por IsActivo.

2. Professional
- Kinesiologo con matricula unica.
- Controla asignacion de agenda y conflictos.

3. Treatment
- Plan terapeutico del paciente.
- Define limite maximo de sesiones.

4. Session
- Nucleo operativo del sistema.
- Fecha/hora, estado asistencial, estado de pago, evolucion clinica, bloqueo de evolucion.

5. Office
- Consultorio fisico.
- Puede tener equipamiento asociado y sesiones vinculadas.

6. Equipment
- Recurso fisico del consultorio.
- Actualmente modelado en dominio y BD, con exposicion parcial en UI (ver seccion de incompletos).

7. AuditLog
- Trazabilidad completa: entidad, accion, usuario, fecha, valores anteriores/nuevos en JSON.

## 4) Flujos funcionales completos (end-to-end)

### Flujo A: alta de sesion
1. Usuario (Admin) envia formulario en Sessions/Create.
2. Controller valida ModelState y deriva a SessionService.
3. SessionService valida conflicto de agenda y limite de tratamiento.
4. SessionRepository persiste con control transaccional.
5. AppDbContext genera auditoria (Create) automaticamente.
6. Se invalida cache para evitar datos viejos.

### Flujo B: agenda del kinesiolgo
1. Usuario con rol Kinesiologo entra a Sessions/MyAgenda.
2. Controller obtiene ProfessionalId desde claims.
3. Servicio devuelve solo sesiones de ese profesional.
4. Se conserva filtro en cookie HttpOnly para continuidad operativa.

### Flujo C: dashboard operativo
1. HomeController pide contadores de pacientes, sesiones y KPIs.
2. Se ejecutan consultas de forma secuencial (evita conflictos de DbContext compartido).
3. Se cachea resultado breve en memoria para acelerar lecturas sucesivas.
4. Se alimentan tarjetas operativas (hoy, cobranzas pendientes, cancelaciones).

### Flujo D: recordatorios y respuesta paciente
1. RemindersController obtiene candidatos en ventanas operativas (24h y 3h).
2. Encola envios en background service.
3. El paciente recibe links firmados (DataProtection token) para confirmar/cancelar.
4. Al responder, SessionService actualiza estado y registra nota interna.

## 5) Seguridad y cumplimiento

1. Autenticacion y autorizacion
- Identity con roles Admin/Kinesiologo.
- Lockout configurado por intentos fallidos.
- Cookies de sesion y rutas de acceso denegado.

2. Seguridad de transporte y plataforma
- HSTS y HTTPS en entornos no desarrollo.
- DataProtection con key ring persistido en filesystem (base para escalar multi-instancia).

3. Seguridad de formularios
- Anti-forgery tokens en POST.
- Razor y EF mitigan XSS e inyeccion SQL por defecto.

4. Trazabilidad
- Auditoria transversal en SaveChanges/SaveChangesAsync.
- Incluye usuario actor (CreatedBy/UpdatedBy) y diff old/new JSON.

## 6) Rendimiento y observabilidad

Optimizaciones ya implementadas:
1. DbContext Pooling para menor costo por request.
2. Query projections (SessionListDto) para evitar cargas pesadas por Includes innecesarios.
3. AsNoTracking en consultas de lectura.
4. Cache in-memory en consultas frecuentes (contadores/listados).
5. Warmup de cache en background al iniciar.
6. Middleware de metricas y endpoint protegido /ops/metrics.
7. Health checks live/ready para integracion con orquestadores.

Resultado operativo documentado en reportes del repositorio:
- Mejora de latencias en / y /Sessions en mediciones autenticadas.

## 7) Evidencia de calidad (tests)

Cobertura funcional representativa:
1. SessionServiceTests
- Conflicto de agenda.
- Ventana configurable.
- Limite de sesiones por tratamiento.
- Bloqueo de evolucion.
- Caching en listados paginados.

2. SessionRepositoryIntegrationTests
- Conteo por estado y fecha.
- Conflicto de indice unico en numeracion de sesion.

3. HomeControllerTests
- Carga de KPIs completos.
- Resiliencia parcial cuando falla una metrica.
- Reuso de cache.

4. AuditControllerTests
- Filtros, paginacion, normalizacion de entradas.
- Export CSV y Excel.
- Opciones de entidades/acciones permitidas.

## 8) Partes incompletas y como cerrarlas bien

### 8.1 Equipamiento (Equipment)
Estado actual:
- Existe entidad y relacion con Office.
- Se muestra en perfil clinico de consultorio.
- No existe modulo CRUD dedicado (controller/vistas/servicio especifico de Equipment).

Plan de cierre recomendado:
1. Agregar IEquipmentRepository + EquipmentRepository.
2. Agregar IEquipmentService + EquipmentService con validaciones.
3. Crear EquipmentsController (Admin) con CRUD y filtros por office.
4. Agregar vistas Index/Create/Edit/Delete/Details.
5. Tests unitarios y web para autorizacion y validaciones.

### 8.2 Escalado de procesamiento en lote
Estado actual:
- MarkPaidBatch procesa en loop secuencial.

Mejora:
1. Exponer metodo SetPaymentStatusBatchAsync en servicio/repositorio.
2. Ejecutar actualizacion por lote en transaccion (set-based) para reducir roundtrips.
3. Mantener logs de auditoria por item o por lote con detalle.

### 8.3 Colas y mensajeria
Estado actual:
- Cola interna in-memory para recordatorios.

Mejora para produccion multi-instancia:
1. Migrar queue a backend persistente (ej. Redis/RabbitMQ/Azure Service Bus).
2. Añadir idempotencia por SessionId+window para no duplicar envios.
3. Añadir reintentos con backoff y dead-letter queue.

## 9) Escalabilidad futura (tecnica y de producto)

### 9.1 Escalabilidad tecnica
Horizonte 1 (corto plazo)
1. Mantener arquitectura monolitica modular actual.
2. Afinar indices segun planes de consulta reales.
3. Formalizar politicas de cache por TTL y invalidacion.
4. Centralizar dashboards de observabilidad (latencia, errores, throughput).

Horizonte 2 (mediano plazo)
1. Extraer modulo de notificaciones como servicio independiente.
2. Persistir key ring de DataProtection en almacenamiento compartido real.
3. Aplicar outbox pattern para eventos confiables de auditoria/notificacion.

Horizonte 3 (si crece la red de clinicas)
1. Multi-sede con particion logica por tenant.
2. Escalado horizontal de Web con balanceador.
3. Read replicas para reportes intensivos.

### 9.2 Escalabilidad funcional
1. Motivos de cancelacion obligatorios y analitica por causa.
2. Flujo de recaptura con sugerencia de reprogramacion inmediata.
3. Automatizacion D+1 de cobranzas pendientes.
4. Segmentacion de KPIs por profesional/franja/obra social.

## 10) Como defender este proyecto en tesis (mensaje profesional)

Mensaje recomendado:
- El sistema no es solo CRUD: implementa reglas clinicas, integridad transaccional, seguridad por capas y trazabilidad.
- Se optimizo rendimiento con estrategias medibles (pooling, projection, cache, warmup).
- Tiene observabilidad y readiness para operar en entornos reales.
- La arquitectura ya habilita escalado incremental sin ruptura.
- Se identificaron gaps de producto y hay roadmap de cierre concreto (especialmente Equipment y automatizacion operativa).

Preguntas tipicas del jurado y respuesta corta:
1. Como evitan doble asignacion de sesiones?
- Regla de conflicto en servicio + chequeo en repositorio + control de concurrencia transaccional.

2. Como garantizan trazabilidad medico-legal?
- Auditoria automatica en SaveChanges con actor, fecha, accion y diff de campos.

3. Que estrategia de escalado propusieron?
- Monolito modular optimizado ahora; luego externalizar notificaciones y persistencia compartida de claves/colas para horizontalizar.

4. Que quedo pendiente?
- CRUD completo de Equipment, batch set-based de cobranzas y cola distribuida para recordatorios.

## 11) Cierre

KineGestion presenta una base tecnica madura para entorno clinico: arquitectura limpia, reglas de negocio consistentes, seguridad, auditoria y mejoras de performance verificables. Las partes pendientes estan acotadas y con plan concreto, lo que fortalece la viabilidad de evolucion del proyecto.
