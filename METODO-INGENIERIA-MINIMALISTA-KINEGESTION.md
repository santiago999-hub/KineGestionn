# Metodo de Ingenieria Minimalista para KineGestion

Fecha de inicio: 2026-06-01
Estado: Activo

## Objetivo
Aplicar un ciclo simple y repetible para mejorar el sistema sin agregar complejidad innecesaria.

Principio base:
- Menos piezas, mas claridad.
- Primero simplificar, despues acelerar.
- Automatizar al final, cuando el flujo ya es estable.

## Ciclo oficial (5 pasos)

1. Cuestionar requisitos
Pregunta obligatoria antes de construir:
- Quien dijo que esto debe ser asi?
- Que problema real resuelve?
- Se puede cumplir con algo ya existente?

Salida esperada:
- Requisito en 1 frase.
- Criterio de exito medible.
- Riesgo si no se implementa.

2. Eliminar partes o procesos
Regla:
- Si nunca hubo que re-agregar nada, no eliminamos lo suficiente.

Checklist de eliminacion:
- Este paso agrega valor al usuario final?
- Este paso existe por historia o por necesidad actual?
- Esta validacion ya la cubre otra capa?

Salida esperada:
- Lista de cosas removidas.
- Impacto estimado en complejidad (bajo/medio/alto).

3. Optimizar solo lo que quedo
Regla:
- Optimizar lo esencial, no la complejidad.

Orden de optimizacion:
1) Flujos de usuario criticos.
2) Consultas de datos frecuentes.
3) Procesos de fondo de alto volumen.

Salida esperada:
- 1 a 3 optimizaciones concretas.
- Metricas antes/despues.

4. Acelerar el ritmo de produccion
Regla:
- Acelerar despues de depurar.

Practicas:
- Cambios pequenos por iteracion.
- Validacion rapida con tests focales.
- Entrega frecuente sin esperar megareleases.

Salida esperada:
- Tiempo de ciclo menor.
- Menos retrabajo por lote.

5. Automatizar al final
Regla:
- No automatizar basura.

Candidatos tipicos:
- Scripts de medicion (p95, errores, disponibilidad).
- Jobs operativos recurrentes (recordatorios, cierres, reportes).
- Verificaciones de calidad pre-merge.

Salida esperada:
- Menos tareas manuales repetitivas.
- Menor variabilidad operativa.

## Definicion de listo (DoL) para cada mejora

Toda propuesta nueva debe incluir:
1. Problema real en una frase.
2. KPI objetivo o metrica tecnica objetivo.
3. Lista de eliminaciones posibles (minimo una).
4. Plan de prueba corto (como validamos).
5. Criterio de rollback simple.

## Definicion de hecho (DoD) minimalista

Una mejora se considera cerrada cuando:
1. Cumple requisito funcional definido.
2. No agrega complejidad estructural innecesaria.
3. Mantiene o mejora p95 en endpoints afectados.
4. Tiene evidencia (test, benchmark o registro operativo).
5. Deja documentada la decision en 5 lineas maximo.

## Politica de decision (semáforo)

Verde:
- Menos complejidad y mejor metrica.
- Se integra.

Amarillo:
- Mejora metrica pero agrega complejidad moderada.
- Se integra con fecha de simplificacion.

Rojo:
- Agrega complejidad y no mejora metrica.
- No se integra.

## Aplicacion concreta a KineGestion (inmediata)

### Frente 1: Sesiones
Cuestionar:
- Cada filtro y cada join del listado admin sigue siendo necesario?
Eliminar:
- Campos no usados en pantallas de listado.
Optimizar:
- Priorizar consultas paginadas por DTO.
Acelerar:
- Cambios chicos por endpoint.
Automatizar:
- Benchmark diario autenticado y registro de tendencia.

### Frente 2: Cobranza
Cuestionar:
- Que pasos son realmente necesarios para marcar pagos?
Eliminar:
- Clicks redundantes en acciones masivas.
Optimizar:
- Conteos directos por estado/pago y rango.
Acelerar:
- Entregas semanales con metricas de aging.
Automatizar:
- Recordatorios D+1 y reportes de pendientes.

### Frente 3: Recordatorios
Cuestionar:
- Todas las ventanas de envio generan valor?
Eliminar:
- Envio no relevante o redundante.
Optimizar:
- Priorizacion por sessions pendientes cercanas.
Acelerar:
- Ajustes por ventana sin tocar arquitectura.
Automatizar:
- Cola de envio y trazabilidad de resultado.

## Cadencia de ejecucion sugerida

Ritmo semanal:
1. Lunes (15 min): cuestionar y eliminar.
2. Martes-Miercoles: optimizar.
3. Jueves: acelerar con cambios pequenos.
4. Viernes: automatizar lo estable y medir.

## Tablero de control minimo

Registrar por semana:
1. Cantidad de pasos eliminados.
2. p95 de endpoints criticos.
3. Error rate.
4. Tiempo medio de entrega por cambio.
5. Automatizaciones nuevas efectivas.

## Regla de oro
Si una mejora no simplifica, no acelera o no automatiza algo util, no entra en sprint.
