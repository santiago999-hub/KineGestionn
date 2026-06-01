# Checklist diaria de 20 minutos para estabilizar p95

## Objetivo
Mantener control diario de latencia en Inicio y Sesiones, detectar desvíos temprano y tomar acciones simples sin frenar operación.

## Duración total
20 minutos

## Preparación (2 min)
1. Confirmar que la app local o entorno objetivo esté levantado.
2. Confirmar credenciales admin disponibles para benchmark autenticado.
3. Definir etiqueta del día en formato AAAA-MM-DD para registrar resultados.

## Medición principal (6 min)
1. Ejecutar corrida 1:
PowerShell:
.\KineGestion.Web\ops\Measure-P95-Authenticated.ps1 -BaseUrl "http://localhost:5138" -Iterations 20 -Routes "/","/Sessions"
2. Ejecutar corrida 2 con el mismo comando.
3. Registrar para cada endpoint:
- ColdMs
- WarmP50Ms
- WarmP95Ms
- ErrorRatePct

## Validación de estabilidad (4 min)
1. Comparar corrida 1 vs corrida 2.
2. Si WarmP95 cambia más de 20% entre corridas, marcar como posible variabilidad transitoria.
3. Si ErrorRatePct es mayor que 0, marcar alerta prioritaria.

## Regla semafórica (3 min)
1. Verde: WarmP95 <= baseline estable + 10%.
2. Amarillo: WarmP95 > baseline + 10% y <= baseline + 20%.
3. Rojo: WarmP95 > baseline + 20% o ErrorRatePct > 0 en forma sostenida.

## Acción rápida según resultado (3 min)
1. Verde:
- No tocar código.
- Registrar resultado y seguir monitoreo.
2. Amarillo:
- Revisar endpoint afectado y última modificación reciente.
- Repetir 1 corrida adicional para confirmar tendencia.
3. Rojo:
- Abrir incidente de performance.
- Priorizar revisión de consulta/índice del endpoint afectado.
- Definir rollback si el rojo persiste por 2 mediciones consecutivas.

## Registro diario (2 min)
Completar esta tabla por día:

| Fecha | Endpoint | Cold ms | Warm p50 ms | Warm p95 ms | Error % | Semáforo | Observaciones |
|---|---:|---:|---:|---:|---:|---|---|
| 2026-05-31 | / | 79.66 | 78.36 | 89.47 | 0 | Verde | Post-fix de concurrencia en Home; corrida de confirmación focalizada estable en rango baseline. |
| 2026-05-31 | /Sessions | 191.70 | 67.82 | 75.96 | 0 | Verde | Corrida 2 consistente (Warm p95 72.52); dentro de umbral de baseline estable. |

## Baseline recomendado actual
- Inicio (/): Warm p95 estable de referencia 89.08 ms
- Sesiones (/Sessions): Warm p95 estable de referencia 71.52 ms

## Criterio de cierre semanal
1. Logrado: 5 de 7 días en verde para ambos endpoints.
2. En progreso: al menos 5 de 7 días sin errores y sin rojo sostenido.
3. Requiere intervención: 2 o más días en rojo para el mismo endpoint.

| 2026-06-01 | / | 88.6 | 86.66 | 364.65 | 0 | Rojo | Corrida2; variación vs corrida1: 218.08%; posible variabilidad transitoria (>20%) |
| 2026-06-01 | /Sessions | 74.38 | 65.1 | 99.08 | 0 | Rojo | Corrida2; variación vs corrida1: 18.62% |
| 2026-06-01 | / | 105.02 | 85.8 | 459.43 | 0 | Rojo | Tercera corrida manual post-instrumentación; p95 elevado sostenido en Home. |
| 2026-06-01 | /Sessions | 211.07 | 68.51 | 79.59 | 0 | Amarillo | Tercera corrida manual post-instrumentación; mejora frente a corrida previa (99.08 ms). |
| 2026-06-01 | / | 122.27 | 88.42 | 130.78 | 0 | Rojo | Corrida focal con profiling de pipeline; costo dominante en endpoint/render, auth bajo. |
| 2026-06-01 | /Sessions | 264.07 | 73.00 | 84.30 | 0 | Amarillo | Corrida focal con profiling de pipeline; mantiene mejora y sin errores. |
| 2026-06-01 | / | 106.99 | 95.71 | 422.46 | 0 | Rojo | Corrida final con profiling acción/render; pico en primer hit (action ~135 ms, render ~178 ms, auth ~20 ms). |
| 2026-06-01 | /Sessions | 246.87 | 94.18 | 212.58 | 0 | Rojo | Corrida final con profiling acción/render; alta variabilidad por outlier inicial de endpoint (~145 ms, render ~108 ms). |
| 2026-06-01 | / | 103.25 | 83.40 | 322.09 | 0 | Rojo | Corrida aislada en puerto 5140 (pre-opt); persiste outlier de primer hit en endpoint/render. |
| 2026-06-01 | /Sessions | 218.30 | 72.30 | 87.12 | 0 | Rojo | Corrida aislada en puerto 5140 (pre-opt); warm estable pero p95 aún sobre baseline+20%. |
| 2026-06-01 | / | 111.90 | 80.78 | 130.13 | 0 | Rojo | Corrida aislada en 5140 post-opt de Home (conteo diario paginado); caída fuerte vs 322.09 ms. |
| 2026-06-01 | /Sessions | 466.82 | 69.85 | 77.63 | 0 | Verde | Corrida aislada en 5140 post-opt; p95 vuelve a zona verde, cold alto por primer hit aislado. |
| 2026-06-01 | / | 82.83 | 84.98 | 123.24 | 0 | Rojo | Corrida aislada en 5140 con warmup explícito (1 pasada): baja ruido de cold y mantiene p95 estable. |
| 2026-06-01 | /Sessions | 76.77 | 70.68 | 74.06 | 0 | Amarillo | Corrida aislada en 5140 con warmup explícito (1 pasada): p95 casi baseline, sin outlier de primer hit. |
| 2026-06-01 | / | 85.91 | 80.75 | 292.65 | 0 | Rojo | Corrida A (warmup=2); outlier puntual en warm p95 de Home. |
| 2026-06-01 | /Sessions | 62.58 | 68.73 | 76.31 | 0 | Amarillo | Corrida A (warmup=2); comportamiento estable y sin errores. |
| 2026-06-01 | / | 81.70 | 84.41 | 121.78 | 0 | Rojo | Corrida B (warmup=2); normaliza fuerte vs Corrida A (delta p95 -58.39%). |
| 2026-06-01 | /Sessions | 72.85 | 70.90 | 77.06 | 0 | Amarillo | Corrida B (warmup=2); variación mínima vs Corrida A (delta p95 +0.98%). |

| 2026-06-01 | / | 48.4 | 56.62 | 58.89 | 0 | Verde | Corrida2; variación vs corrida1: -9.43% |
| 2026-06-01 | /Sessions | 36.73 | 42.44 | 44.54 | 0 | Verde | Corrida2; variación vs corrida1: -0.65% |

| 2026-06-01 | / | 63.43 | 53.92 | 55.61 | 0 | Verde | Corrida2; variación vs corrida1: 2.41%; server run2 avg~1.62ms max=306ms req=8; server run1 avg~1.75ms |
| 2026-06-01 | /Sessions | 41.35 | 41.35 | 52.64 | 0 | Verde | Corrida2; variación vs corrida1: -87.63%; posible variabilidad transitoria (>20%); server run2 avg~1.71ms max=136ms req=7; server run1 avg~5.43ms |
