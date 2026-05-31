# Mini tablero operativo (p95 y errores)

## Objetivo

Verificar rapidamente salud post-release en endpoints criticos y en SQL.

## Endpoints criticos

- /Users
- /Sessions
- /Sessions/MyAgenda

## Metricas minimas

- p50 y p95 por endpoint.
- max latency por endpoint.
- tasa de 5xx.
- bloqueos SQL activos.

## Ejecucion HTTP (PowerShell)

Script: KineGestion.Web/ops/Measure-P95.ps1

Para endpoints protegidos (recomendado en este proyecto):

Script: KineGestion.Web/ops/Measure-P95-Authenticated.ps1

Ejemplo:

```powershell
./KineGestion.Web/ops/Measure-P95.ps1 -BaseUrl "https://tu-dominio.com" -Iterations 30 -PauseMs 100

./KineGestion.Web/ops/Measure-P95-Authenticated.ps1 -BaseUrl "http://localhost:5138" -Iterations 30 -Routes "/","/Sessions"
```

Interpretacion rapida:

- Verde: p95 <= baseline + 10%
- Amarillo: p95 > baseline + 10% y <= baseline + 20%
- Rojo: p95 > baseline + 20% (evaluar rollback)

## Ejecucion SQL

Script: KineGestion.Data/SQL-METRICS-DASHBOARD.sql

Incluye:

- Requests activos mas costosos.
- Top waits acumulados.
- Sesiones bloqueadas.
- Estado de indices de ronda 3.

## Decision operativa

- Continuar: sin 5xx sostenidos y p95 en verde/amarillo estable.
- Rollback: p95 en rojo sostenido 10 min o 5xx > 2% sostenido.
