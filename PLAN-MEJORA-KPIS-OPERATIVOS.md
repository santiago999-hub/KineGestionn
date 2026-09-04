# Plan de mejora de KPIs operativos (KineGestion)

## Objetivo
Mejorar en 6 a 8 semanas:
- Tasa de cobranza
- Cumplimiento del dia
- Tasa de cancelacion

Este plan esta pensado para ejecutar sobre lo que ya existe en KineGestion (Home, Sessions, Billing y Reminders), sin rehacer arquitectura.

## Definiciones KPI (cerrar antes de implementar)
1. Tasa de cobranza
- Formula recomendada: sesiones pagadas / sesiones completadas (mismo periodo)
- Nota: no conviene dividir por total de sesiones porque mezcla canceladas y distorsiona gestion de caja.

2. Cumplimiento del dia
- Formula recomendada: sesiones completadas hoy / sesiones agendadas hoy
- Opcional: separar no-show de canceladas para ver calidad de agenda.

3. Tasa de cancelacion
- Formula recomendada: sesiones canceladas / sesiones agendadas (mismo periodo)
- Separar cancelacion temprana vs tardia (ej. menos de 24h).

## Priorizacion (impacto x esfuerzo)

### P0 (esta semana) - impacto alto, bajo riesgo
1. Operativa de cobranza diaria
- Meta: bajar pendientes de cobro de sesiones completadas.
- Cambio funcional:
  - Vista de cobros con foco en "completadas + pendientes".
  - Corte por antiguedad: 0-2 dias, 3-7 dias, +7 dias.
- Apalanca:
  - BillingController y Dashboard de cobranzas existentes.

2. Doble recordatorio para cumplimiento
- Meta: subir asistencia del dia.
- Cambio funcional:
  - Mantener recordatorio 24h + agregar recordatorio 3h antes.
  - Priorizar sesiones "sin confirmar" en ventana de hoy.
- Apalanca:
  - RemindersController + ReminderDispatchQueue.

3. Accion diaria en dashboard
- Meta: hacer que el primer click operativo sea sobre riesgo real.
- Cambio funcional:
  - Tarjeta/CTA principal para:
    - Cobros pendientes del dia
    - Sesiones sin confirmar del dia
    - Canceladas del dia (seguimiento/reprogramacion)
- Apalanca:
  - HomeController y Home/Index.

### P1 (proxima semana) - alto impacto, cambio de negocio
1. Motivo de cancelacion obligatorio
- Meta: reducir cancelacion con causa accionable.
- Cambio funcional:
  - Al cancelar, pedir motivo (lista cerrada + observacion libre).
  - Reporte semanal por motivo y franja horaria.
- Requiere:
  - Extender modelo de Session o agregar entidad de eventos de cancelacion.

2. Flujo de recupero inmediato (reprogramacion)
- Meta: convertir cancelacion en reprogramacion.
- Cambio funcional:
  - Luego de cancelar, sugerir 2-3 turnos alternativos en el acto.
  - KPI nuevo: recaptura = reprogramadas / canceladas.

3. Cobranza post-sesion automatizada
- Meta: mejorar cobro en D+1.
- Cambio funcional:
  - Campana de notificacion para completadas pendientes de pago.
  - Mensaje distinto segun antiguedad.

### P2 (semana 3-4) - optimizacion y escalado
1. Segmentacion por profesional y franja
- Identificar donde se fuga cumplimiento/cobranza.

2. Politica de cancelacion tardia
- Definir regla interna y mensaje estandar.

3. Experimentacion de mensajes
- A/B de plantillas de recordatorio y cobranza.

## Backlog tecnico sugerido (ordenado)

### Sprint A (P0)
1. Cobranza enfocada en recupero
- Ajustar dashboard de cobranzas para mostrar:
  - completadas pendientes
  - aging buckets
  - accion masiva por lote (marcar pagadas)

2. Campana 24h + 3h
- Extender Reminders para dos ventanas (o jobs separados).
- Registrar trazabilidad por cada envio y resultado.

3. Dashboard operativo
- Reordenar tarjetas segun urgencia diaria.
- Mostrar metas del dia y brecha actual.

### Sprint B (P1)
1. Modelo de cancelacion
- Agregar motivo, categoria y timestamp de cancelacion.
- Exponer filtros y metricas por motivo.

2. Reprogramacion asistida
- Endpoint para sugerencia de turnos por profesional.
- CTA directo desde sesion cancelada.

3. Cobranza D+1
- Lista automatica de seguimiento + recordatorio configurable.

## Metas numericas recomendadas
- Cobranza (4 semanas): +10 a +15 puntos
- Cumplimiento hoy (4 semanas): +8 a +12 puntos
- Cancelacion (6 semanas): -20% relativo
- Recaptura de canceladas (4 semanas): >30%

## Tablero semanal minimo
1. Cobranza = pagadas / completadas (periodo)
2. Cumplimiento hoy = completadas hoy / agendadas hoy
3. Cancelacion = canceladas / agendadas (periodo)
4. Recaptura = reprogramadas / canceladas
5. Aging de cobro pendiente: 0-2, 3-7, +7 dias
6. Top 5 motivos de cancelacion

## Riesgos y mitigacion
1. Cambiar formula de KPI sin comunicar
- Mitigar: versionar definicion en dashboard (tooltip/metadato).

2. Recordatorios excesivos
- Mitigar: limite por paciente y ventana minima entre envios.

3. Carga operativa en recepcion
- Mitigar: checklist diario de 15 minutos con tareas priorizadas.

## Siguiente paso recomendado
1. Ejecutar Sprint A completo (P0) y medir 2 semanas.
2. Con esos resultados, avanzar Sprint B con motivos + recaptura.
3. Revisar metas cada viernes con corte semanal fijo.

## Marco de ejecucion adoptado (Ingenieria Minimalista)
Desde 2026-06-01, este plan se ejecuta con el ciclo:
1. Cuestionar requisitos.
2. Eliminar partes/procesos sin valor.
3. Optimizar solo lo que queda.
4. Acelerar ritmo de entrega.
5. Automatizar al final.

Documento operativo:
- METODO-INGENIERIA-MINIMALISTA-KINEGESTION.md

Regla de aplicacion por item de backlog:
1. Cada tarea nueva debe incluir problema real, metrica objetivo y al menos una eliminacion posible.
2. No se automatiza un flujo inestable o innecesario.
3. Si una mejora agrega complejidad sin mejorar KPI o latencia, queda fuera del sprint.

## Pendiente abierto: optimizacion de tiempos
Para la siguiente iteracion, queda como pendiente explicito afinar tiempos de respuesta con foco en primera carga y estabilidad de p95.

1. Mantener benchmark autenticado diario sobre `/` y `/Sessions`.
2. Revisar variabilidad de p95 en frio vs caliente y registrar outliers.
3. Priorizar mejoras de consulta/indices donde p95 supere +20% del baseline estable.
4. Revalidar impacto luego de cada ajuste con la misma metodologia de medicion.

## Registro de avance P2-1: Segmentacion por profesional y franja (2026-09-03)
Estado: IMPLEMENTADO y commiteado (commit 17cf5ea). Falta solo verificar funcional en navegador.

Que se hizo:
- Panel "Segmentacion KPIs" (solo Admin) en /Segmentacion: desglosa cobranza, cumplimiento y cancelacion por profesional y por franja horaria, con rango de fechas.
- 2 consultas de agregacion unica (no N consultas): GetKpiSegmentsByProfessionalAsync y GetKpiSegmentsByTimeSlotAsync, con QueryCache de 10s.
- DTO KpiSegmentDto con KPIs calculados (CumplimientoPct, CobranzaPct, CancelacionPct).
- Tests: 4 unit (DTO) + 2 integracion (repositorio). Suites verdes: Core 97, Web 134.

Siguientes pasos al retomar:
1. Verificar /Segmentacion autenticado en navegador (la app se levanto OK, puerto 5138, pero no se cerro la verificacion visual completa).
2. Filtro por profesional seria opcional de mejora (hoy muestra todos agrupados).
3. Despues de P2-1: P2-2 politica de cancelacion tardia o P2-3 experimentacion de mensajes.

## Registro de avance P2-2: Politica de cancelacion tardia (2026-09-03)
Estado: IMPLEMENTADO y verificado en navegador. Commit realizado el 2026-09-04 (db32079).

Que se hizo:
- Clasificacion CancellationTiming (Early/Late) en SessionService.GetCancellationTiming; tardia = menos de 24h de antelacion.
- Banner en /Sessions/Cancel (warning si tardia, info si temprana) con mensaje estandar en SessionsController.CancellationPolicyMessage.
- Metricas de tardias (conteo y % en ultimos 30d) en Home dashboard.
- CountLateCancellationsInRangeAsync en repositorio con clasificacion en SQL (forma traducible por EF: CancelledAt > FechaHora - 24h), sin materializar en memoria.
- Paso extra de warmup para el conteo de tardias en CacheWarmupBackgroundService.
- Tests: unit (GetCancellationTiming) + Web (mensaje) + integracion (count). Suites verdes: Core 102, Web 137.

## Registro revision P95 (2026-09-04)
Estado: REVISADO, sin cambios de codigo de rendimiento pendientes; outliers transitorios conocidos. Registro completo en CHECKLIST-DIARIA-P95-20MIN.md.

- /ops/metrics p50 35.77ms / p95 49.01ms: el SQL no es el cuello (control con consultas reales).
- / p50 estable 72-89ms (baseline 89.08): corrida 4 (40 iter, warmup5) con p95 102ms verde; corridas 1-3 con 1-2 outliers aislados (~280-358ms) que arrastran p95.
- /Sessions p50 estable 68-76ms (baseline 71.52): outliers transitorios aislados igual que /.
- 0 errores en todas las corridas. Patron identico al documentado en 06-01 y 09-03: outliers de primer hit no sostenidos.
- Accion: mantener benchmark diario segun checklist; no se abre incidente (sin errores y sin rojo sostenido en 2 mediciones consecutivas para el mismo endpoint).

Nota infraestructura:
- El 2026-09-03 tambien se commiteo (af7d43b) la baja de logs de requests normales a Debug para reducir overhead de logging en hot path (REQUEST-METRICS + pipeline profile).

## Registro de avance P2-2: Politica de cancelacion tardia (2026-09-04)
Estado: IMPLEMENTADO y VERIFICADO en app. Suites verdes. Falta solo commit.

Regla interna definida:
- Cancelacion TARDIA = cancelada con menos de 24h de antelacion respecto al turno (FechaHora - CancelledAt < 24h).
- Cancelacion TEMPRANA = 24h o mas de antelacion.

Que se hizo:
- Enum `CancellationTiming { Early, Late }` en Core/Enums.cs.
- `GetCancellationTiming(Session)` en ISessionService/SessionService: clasifica segun antelacion usando CancelledAt (por defecto Early si no es cancelada o falta timestamp).
- `CountLateCancellationsInRangeAsync` (servicio + repositorio): consulta SQL en una sola pasada que cuenta canceladas con <24h de antelacion en un rango.
- Vista Cancel: banner de politica con mensaje estandar segun clasificacion (aviso si tardia, informativo si temprana), sugiere ofrecer reprogramacion en el acto.
- Home/Index del dashboard: nueva metrica "tardias: X% (n)" junto a la tasa de cancelacion de 30 dias.
- Tests: 3 unit (GetCancellationTiming) + 2 Web (mensaje estandar) + 1 integracion (CountLateCancellations). Suites verdes: Core 101, Web 137.
- Correccion de diseno detectada en verificacion: GetCancellationTiming para sesion sin cancelar usaba el timestamp registrado; se ajusto a "cancelar ahora" (UtcNow) cuando CancelledAt es null, asi el banner refleja la antelacion real de la accion. Tests actualizados a 4 unit. Suites finales verdes: Core 102, Web 137.

Siguientes pasos al retomar:
1. Commit del avance P2-2 (cambios sin commitear).
2. Luego falta P2-3 experimentacion de mensajes (A/B de plantillas de recordatorio y cobranza).

Actualizacion 2026-09-04:
- Commit realizado (db32079) incluyendo P2-2 + mejoras de "Quienes Somos" + fix SQL CountLateCancellations + registro P95.
- Sesiones de prueba creadas para verificar banners (ids 1 y 2) ELIMINADAS de KineGestionDB. DB de sesiones queda en 0.
- Push a origin/main (15faa91..db32079).
- Baseline p95 post-limpieza registrado en CHECKLIST (corrida 2: / p95 98.68 amarillo, /Sessions p95 55.7 verde, 0 errores).

## Registro de avance P2-3 paso 1: Embudo de recordatorios (2026-09-04)
Estado: IMPLEMENTADO y verificado en navegador (/ReminderFunnel). Suites verdes: Core 105, Web 138. Pendiente de commit.

Que se hizo (instrumento de baseline para el A/B; sin tocar plantillas todavia):
- Reporte /ReminderFunnel (solo Admin) que muestra el embudo de recordatorios por rango de fecha de envio:
  Enviados (AuditLogs ReminderDispatch con EmailSent/WhatsAppSent y DispatchType=PatientReminder) ->
  Confirmados (nota CONFIRMADA_PACIENTE) -> Asistidos (Status Completed), mas Cancelados (CANCELADA_PACIENTE o canceladas).
- DTO ReminderFunnelDto (con rates Confirmation/Attendance/Cancellation) y SessionFunnelOutcomeDto.
- GetSessionFunnelOutcomesAsync en repositorio: proyeccion SQL limitada a sesiones enviadas (evita Memory Bomb);
  InternalNotes se lee descifrada por EF y la clasificacion se hace en memoria sobre volumen acotado.
- BuildReminderFunnelAsync en servicio; ReminderFunnelController orquesta AuditLog+JSON+sesiones.
- Vista con tarjetas de KPI y barra de embudo; link en la navegacion (seccion Seguridad y Control).
- Tests: 2 unit (BuildReminderFunnel) + 1 integracion (GetSessionFunnelOutcomes) + 1 Web (controller).

Siguientes pasos al retomar (P2-3 paso 2):
1. Commit de este avance.
2. Cuando haya volumen real de recordatorios, usar /ReminderFunnel como baseline de tasa de confirmacion/asistencia.
3. Solo entonces: variantes de plantilla (email/WhatsApp) con seleccion manual y comparacion contra el baseline.
