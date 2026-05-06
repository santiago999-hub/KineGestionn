# Runbook de despliegue en produccion (30 min)

Fecha: 06/05/2026
Objetivo: aplicar mejoras de performance con riesgo controlado, verificacion de p95 y rollback inmediato.

Antes de iniciar, completar el perfil en: KineGestion.Data/PROD-ENV-PROFILE.example.md

## Alcance

- Aplicar indices recomendados de la ronda 3.
- Verificar salud funcional basica.
- Medir impacto rapido en endpoints criticos.
- Tener criterio de rollback claro y ejecutable.

## Roles sugeridos

- Operador A: ejecuta SQL y comandos.
- Operador B: monitorea aplicacion, logs y tiempos.

## Ventana total

- Duracion objetivo: 30 minutos.
- Ventana sugerida: baja concurrencia.

## Criterios de exito

- Sin errores 5xx nuevos sostenidos.
- p95 de endpoints criticos igual o mejor que baseline.
- Sin bloqueo prolongado en base de datos.

## Criterios de rollback

- p95 empeora mas de 20% durante 10 minutos.
- Error rate 5xx supera 2% sostenido.
- Bloqueos/deadlocks recurrentes post-cambio.

---

## 0) Preparacion (T-10 a T-0)

1. Confirmar backup reciente de la base (max 24h).
2. Confirmar acceso de administrador SQL y permisos para CREATE INDEX/DROP INDEX.
3. Congelar cambios de aplicacion durante la ventana.
4. Registrar baseline rapido (ultimos 15 min):
   - p95 Users/Index
   - p95 Sessions/Index
   - p95 Sessions/MyAgenda
   - tasa de 5xx
5. Definir valores de trabajo:
    - [[SQL_SERVER]]
    - [[SQL_DATABASE]]
    - [[WEB_DOMAIN]]

Si falta alguno de los 4 puntos: no comenzar.

---

## 1) Minuto 0-5: prechecks SQL

Ejecutar:

```sql
SELECT @@VERSION AS SqlVersion;
SELECT DB_NAME() AS CurrentDatabase;

SELECT name, state_desc
FROM sys.databases
WHERE name = DB_NAME();
```

Conexion sugerida:

```bash
sqlcmd -S [[SQL_SERVER]] -d [[SQL_DATABASE]] -E
```

Validacion:

- Base correcta.
- Estado ONLINE.

---

## 2) Minuto 5-15: aplicar indices

Opcion recomendada (migracion versionada, solo cambio nuevo):

```sql
:r KineGestion.Data/Migrations/Deploy_AddRound3QueryIndexes_Only.sql
```

Opcion manual (si se requiere control fino), ejecutar en este orden:

```sql
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Patients_IsActivo_Apellido_Nombre' AND object_id = OBJECT_ID('dbo.Patients'))
    CREATE INDEX IX_Patients_IsActivo_Apellido_Nombre ON dbo.Patients (IsActivo, Apellido, Nombre);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Professionals_IsActivo_Apellido_Nombre' AND object_id = OBJECT_ID('dbo.Professionals'))
    CREATE INDEX IX_Professionals_IsActivo_Apellido_Nombre ON dbo.Professionals (IsActivo, Apellido, Nombre);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Treatments_PatientId_FechaInicio' AND object_id = OBJECT_ID('dbo.Treatments'))
    CREATE INDEX IX_Treatments_PatientId_FechaInicio ON dbo.Treatments (PatientId, FechaInicio);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sessions_PaymentStatus_FechaHora' AND object_id = OBJECT_ID('dbo.Sessions'))
    CREATE INDEX IX_Sessions_PaymentStatus_FechaHora ON dbo.Sessions (PaymentStatus, FechaHora);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_Email' AND object_id = OBJECT_ID('dbo.AspNetUsers'))
    CREATE INDEX IX_AspNetUsers_Email ON dbo.AspNetUsers (Email);
```

Verificar creacion:

```sql
SELECT t.name AS TableName, i.name AS IndexName, i.is_disabled, i.is_hypothetical
FROM sys.indexes i
JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name IN (
    'IX_Patients_IsActivo_Apellido_Nombre',
    'IX_Professionals_IsActivo_Apellido_Nombre',
    'IX_Treatments_PatientId_FechaInicio',
    'IX_Sessions_PaymentStatus_FechaHora',
    'IX_AspNetUsers_Email'
)
ORDER BY t.name, i.name;
```

---

## 3) Minuto 15-20: smoke test funcional

Checklist rapido en aplicacion:

1. Login admin.
2. Abrir https://[[WEB_DOMAIN]]/Users con filtro de email.
3. Abrir https://[[WEB_DOMAIN]]/Sessions con filtros de estado y pago.
4. Abrir https://[[WEB_DOMAIN]]/Sessions/MyAgenda para un kinesiologo.
5. Confirmar que no hay errores en pantalla ni redirecciones inesperadas.

---

## 4) Minuto 20-27: validacion de performance

Comparar contra baseline:

- p95 Users/Index
- p95 Sessions/Index
- p95 Sessions/MyAgenda
- tasa 5xx

Regla de decision:

- Si mejora o se mantiene dentro de +-10%, continuar.
- Si empeora >20% sostenido 10 min, iniciar rollback.

Consulta de apoyo (actividad y waits recientes):

```sql
SELECT TOP 20
    r.session_id,
    r.status,
    r.cpu_time,
    r.total_elapsed_time,
    r.logical_reads,
    r.wait_type,
    SUBSTRING(t.text, (r.statement_start_offset/2)+1,
      ((CASE r.statement_end_offset WHEN -1 THEN DATALENGTH(t.text)
      ELSE r.statement_end_offset END - r.statement_start_offset)/2) + 1) AS statement_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id <> @@SPID
ORDER BY r.total_elapsed_time DESC;
```

---

## 5) Minuto 27-30: cierre o rollback

### Si todo esta bien

1. Registrar resultados (antes/despues).
2. Cerrar ventana.
3. Monitoreo reforzado por 60 minutos.

### Si hay que hacer rollback

Ejecutar:

```sql
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Patients_IsActivo_Apellido_Nombre' AND object_id = OBJECT_ID('dbo.Patients'))
    DROP INDEX IX_Patients_IsActivo_Apellido_Nombre ON dbo.Patients;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Professionals_IsActivo_Apellido_Nombre' AND object_id = OBJECT_ID('dbo.Professionals'))
    DROP INDEX IX_Professionals_IsActivo_Apellido_Nombre ON dbo.Professionals;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Treatments_PatientId_FechaInicio' AND object_id = OBJECT_ID('dbo.Treatments'))
    DROP INDEX IX_Treatments_PatientId_FechaInicio ON dbo.Treatments;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sessions_PaymentStatus_FechaHora' AND object_id = OBJECT_ID('dbo.Sessions'))
    DROP INDEX IX_Sessions_PaymentStatus_FechaHora ON dbo.Sessions;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AspNetUsers_Email' AND object_id = OBJECT_ID('dbo.AspNetUsers'))
    DROP INDEX IX_AspNetUsers_Email ON dbo.AspNetUsers;
```

Luego:

1. Reiniciar monitoreo 15 minutos.
2. Confirmar vuelta a baseline.
3. Abrir incidencia para analisis detallado.

---

## Anexo: artefactos relacionados

- SQL de ronda 3: KineGestion.Data/SQL-PERF-ROUND3.md
- Hardening operativo: KineGestion.Web/HARDENING-OPERATIVO.md
- Config base prod: KineGestion.Web/appsettings.Production.json
- Perfil de entorno: KineGestion.Data/PROD-ENV-PROFILE.example.md
- Script SQL solo de esta migracion: KineGestion.Data/Migrations/Deploy_AddRound3QueryIndexes_Only.sql
