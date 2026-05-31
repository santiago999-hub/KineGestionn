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

## Pendiente abierto: optimizacion de tiempos
Para la siguiente iteracion, queda como pendiente explicito afinar tiempos de respuesta con foco en primera carga y estabilidad de p95.

1. Mantener benchmark autenticado diario sobre `/` y `/Sessions`.
2. Revisar variabilidad de p95 en frio vs caliente y registrar outliers.
3. Priorizar mejoras de consulta/indices donde p95 supere +20% del baseline estable.
4. Revalidar impacto luego de cada ajuste con la misma metodologia de medicion.
