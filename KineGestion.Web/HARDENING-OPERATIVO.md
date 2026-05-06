# Hardening operativo recomendado (produccion)

Este documento resume ajustes recomendados para reducir riesgo operativo y mejorar estabilidad en despliegues productivos.

## 1) Configuracion recomendada de appsettings.Production.json

Ejemplo base:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=PROD-SQL;Database=KineGestionDB;User Id=app_user;Password=<secret>;TrustServerCertificate=False;Encrypt=True;"
  },
  "Seed": {
    "AdminEmail": "admin@tu-dominio.com",
    "AdminPassword": "<usar-secreto-externo>",
    "ResetAdminPasswordOnStartup": false
  },
  "Scheduling": {
    "ProfessionalConflictWindowMinutes": 45
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "tu-dominio.com"
}
```

## 2) Secretos y credenciales

- No guardar contraseñas reales en appsettings versionados.
- Usar Secret Manager solo en desarrollo.
- En produccion usar variables de entorno, Azure Key Vault o equivalente.
- Rotar password de admin inicial luego del primer acceso.

## 3) Cookies y autenticacion

Validar en produccion:

- Cookie SecurePolicy = Always.
- Cookie HttpOnly = true.
- Cookie SameSite = Lax o Strict segun flujos externos.
- Lockout habilitado y monitoreado.

## 4) Data Protection para multi-instancia

Si hay mas de una instancia web, persistir key ring compartido (Redis, SQL, Blob Storage). Evita invalidacion de cookies entre nodos.

## 5) Seed de admin

- Mantener ResetAdminPasswordOnStartup en false.
- Activarlo solo para recuperacion controlada.
- Registrar auditoria cuando se habilite temporalmente.

## 6) SQL y resiliencia

- Habilitar reintentos transitorios de SQL Server (EnableRetryOnFailure).
- Monitorear tiempos de consulta de listados con paginacion.
- Verificar indices de tablas de dominio y estadisticas de SQL actualizadas.

## 7) Logs y observabilidad

- Registrar errores de negocio y excepciones no controladas con correlacion.
- Evitar logging de datos sensibles (password, tokens, claims completos).
- Configurar alertas por picos de 401/403/500.

## 8) Checklist rapido pre-produccion

- Migraciones aplicadas y verificadas.
- HTTPS y certificado validos.
- Seed admin probado y desactivado para reset automatico.
- Backup y restore de base de datos ensayados.
- Pruebas de carga basicas sobre listados principales.
