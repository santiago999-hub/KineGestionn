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
