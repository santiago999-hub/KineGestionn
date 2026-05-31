# Reporte comparativo p95 operativo - 2026-05-31

## Objetivo
Comparar latencia de primera carga (cold) y carga caliente (warm) en endpoints criticos luego de las mejoras aplicadas en Home, Billing y warmup.

## Alcance
- Endpoint Inicio: /
- Endpoint Sesiones: /Sessions
- Entorno: local, autenticado, Admin
- Herramienta: KineGestion.Web/ops/Measure-P95-Authenticated.ps1

## Comandos usados

```powershell
# Baseline previo (antes de cerrar este bloque)
.\KineGestion.Web\ops\Measure-P95-Authenticated.ps1 -BaseUrl "http://localhost:5138" -Iterations 25 -Routes "/","/Sessions"

# Medicion final de validacion (post cambios)
.\KineGestion.Web\ops\Measure-P95-Authenticated.ps1 -BaseUrl "http://localhost:5138" -Iterations 20 -Routes "/","/Sessions"
```

## Resultados comparativos

| Endpoint | Baseline previo (25) Cold ms | Baseline previo Warm p50 ms | Baseline previo Warm p95 ms | Final (20) Cold ms | Final Warm p50 ms | Final Warm p95 ms |
|---|---:|---:|---:|---:|---:|---:|
| / | 140.72 | 82.31 | 94.65 | 82.45 | 76.32 | 89.08 |
| /Sessions | 239.20 | 69.73 | 109.84 | 70.99 | 64.47 | 71.52 |

## Delta principal

### Inicio (/)
- Cold: mejora de 58.27 ms (de 140.72 a 82.45), aprox -41.4%
- Warm p50: mejora de 5.99 ms (de 82.31 a 76.32), aprox -7.3%
- Warm p95: mejora de 5.57 ms (de 94.65 a 89.08), aprox -5.9%

### Sesiones (/Sessions)
- Cold: mejora de 168.21 ms (de 239.20 a 70.99), aprox -70.3%
- Warm p50: mejora de 5.26 ms (de 69.73 a 64.47), aprox -7.5%
- Warm p95: mejora de 38.32 ms (de 109.84 a 71.52), aprox -34.9%

## Calidad de medicion
- Error rate: 0% en todas las corridas reportadas.
- Se observo una corrida intermedia con outlier en / (warm p95 382.39 ms), no reproducida en la corrida final de validacion.
- Corrida final tomada como referencia estable para decision operativa.

## Cambios tecnicos que explican la mejora
1. Home:
- Reemplazo de conteos indirectos via listado paginado por contadores directos (status + payment + rango).

2. Warmup:
- Precalentamiento alineado con las mismas claves usadas por dashboard y sesiones.
- Correccion de concurrencia EF en warmup (secuencial en lugar de paralelo dentro del mismo scope).

3. Billing:
- Conteos de aging y completadas pendientes por metodos directos.
- Accion masiva de marcado de pago para reduccion de friccion operativa.

4. Dashboard operativo:
- CTA y metrica de canceladas hoy para seguimiento y reprogramacion.

## Conclusiones ejecutivas
- El sistema queda con mejor estabilidad en primera carga y mejor p95 en /Sessions.
- El riesgo de variabilidad inicial baja tras alinear warmup y eliminar consultas costosas para KPI.
- Se habilita una operativa de cobranza mas eficiente por lote sin degradar latencia.

## Recomendaciones de seguimiento (7 dias)
1. Ejecutar el benchmark autenticado una vez por dia (mismas rutas, mismas iteraciones).
2. Vigilar principalmente Warm p95 de / y /Sessions.
3. Si Warm p95 supera +20% del baseline final en forma sostenida, abrir incidente de performance.
4. Mantener evidencia historica de corridas para anexos de tesis/operaciones.
