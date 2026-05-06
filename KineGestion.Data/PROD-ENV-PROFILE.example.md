# Perfil de entorno productivo (completar antes de ventana)

## Infraestructura

- EnvironmentName: Production
- SQL Server host: <completar>
- SQL Database: <completar>
- Dominio web: <completar>
- Monitoreo/observabilidad: <completar herramienta>

## Credenciales y secretos

- ConnectionString source: <Vault/EnvVar>
- Usuario SQL (app): <completar>
- Usuario SQL (admin despliegue): <completar>
- Seed:ResetAdminPasswordOnStartup: false

## Umbrales operativos

- p95 Users/Index baseline: <ms>
- p95 Sessions/Index baseline: <ms>
- p95 Sessions/MyAgenda baseline: <ms>
- Error rate 5xx max: 2%
- Regla rollback latencia: >20% durante 10 min

## Rutas criticas para smoke test

- /Account/Login
- /Users
- /Sessions
- /Sessions/MyAgenda

## Variables para runbook

- [[SQL_SERVER]]
- [[SQL_DATABASE]]
- [[WEB_DOMAIN]]
- [[P95_USERS_BASELINE_MS]]
- [[P95_SESSIONS_BASELINE_MS]]
- [[P95_AGENDA_BASELINE_MS]]
