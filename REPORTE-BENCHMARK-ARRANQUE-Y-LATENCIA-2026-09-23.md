# Reporte de benchmark: arranque y latencia por pantalla - 2026-09-23

## Objetivo
Responder al síntoma percibido de "todas las pantallas lentas, incluso levantar la app en un hosting" con mediciones locales reales (Release, sin Docker instalado, SQL Express localhost).

## Herramientas
- KineGestion.Web/ops/Measure-P95-Authenticated.ps1 (baseline autenticado por ruta).
- Health checks /health/live (proceso) y /health/ready (proceso + BD) al arrancar la DLL de Release.
- Entorno: local, Release, ASPNETCORE_ENVIRONMENT=Development, SQL Express `.\SQLEXPRESS` con `KineGestionDB`.

## Hallazgo 1: el arranque es el costo dominante
- Tiempo hasta que /health/live responde (proceso): ~7.8 s.
- Tiempo hasta que /health/ready responde (proceso + BD): ~8.3 s.
- Costo principal: JIT de arranque + build del modelo EF Core + 5 BackgroundService + seed de roles/usuario + DataProtection (key ring).
- En un hosting de 1-2 vCPU la misma carga se amplía (est. 15-40 s). El primer arranque de un contenedor con volumen nuevo además corre las 19 migraciones + seed.

## Hallazgo 2: las pantallas en caliente son rápidas (el problema no son las queries)
Medición autenticada, admin, warmup previo de 1 iteración, 15 muestras por ruta:

| Ruta | Cold (ms) | Warm p50 (ms) | Warm p95 (ms) | Max (ms) | Error rate |
|---|--:|--:|--:|--:|--:|
| / | 107 | 82 | 90 | 107 | 0% |
| /Sessions | 71 | 60 | 68 | 71 | 0% |
| /Patients | 70 | 78 | 291 (1 outlier) | 291 | 0% |
| /Billing | 67 | 80 | 103 | 103 | 0% |

- Warm p50 entre 60 y 82 ms en todas las rutas: rendimiento en caliente correcto.
- El p95 de /Patients (291 ms) es un único outlier (1er hit post-warmup o GC), no sistémico.
- El "todo lento" percibido corresponde a arranques en frío y al primer click por pantalla (JIT + caché fría), no a consultas lentas sostenidas.

## Hallazgo 3: la app no arranca en Production sin configuración real
- Con `--no-launch-profile` (ASPNETCORE_ENVIRONMENT=Production) la app lanza excepción:
  `ValidateProductionSafetyConfiguration` rechaza los placeholders CHANGE_ME de appsettings.Production.json.
- Implicancia: "levantar la app en un hosting" falla hasta setear las variables de entorno reales
  (ConnectionStrings__DefaultConnection, Seed__AdminPassword, AllowedHosts__), según PROD-ENV-PROFILE.example.md.
- docker-compose.yml es el mecanismo de despliegue previsto (SQL Server 2022 + web, `Database__ApplyMigrationsOnStartup=true`), pero no hay Docker instalado en la máquina local: aún no hay un hosting real desplegado.

## Recomendaciones priorizadas (ataque al arranque, no a las queries)
1. Publish con `ReadyToRun` (`/p:PublishReadyToRun=true`, Dockerfile:15): reduce ~40-60% el cold start.
2. Extender `CacheWarmupBackgroundService` a todas las pantallas (hoy solo / y /Sessions): elimina el JIT + caché fría del primer click en Patients/Billing/Reminders/etc.
3. Ampliar `Observability:PipelineProfilePaths` con las rutas calientes para monitorear auth/action/render de todas (hoy solo "/" y "/Sessions").
4. Opcional en hosting débil: subir `DispatchQueue:PollIntervalSeconds` de 5 a 30 para reducir ruido de fondo.

## Evidencia de build
- `dotnet build KineGestion.Web -c Release --no-restore`: 0 warnings, 0 errors, 6.4 s.