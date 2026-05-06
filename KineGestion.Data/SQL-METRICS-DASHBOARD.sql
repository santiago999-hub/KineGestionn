-- Mini tablero SQL: salud de consultas y bloqueos
-- Ejecutar en SQL Server (db de KineGestion)

-- 1) Requests activos mas costosos
SELECT TOP 20
    r.session_id,
    r.status,
    r.cpu_time,
    r.total_elapsed_time,
    r.logical_reads,
    r.wait_type,
    DB_NAME(r.database_id) AS database_name,
    SUBSTRING(t.text, (r.statement_start_offset/2)+1,
      ((CASE r.statement_end_offset WHEN -1 THEN DATALENGTH(t.text)
      ELSE r.statement_end_offset END - r.statement_start_offset)/2) + 1) AS statement_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
WHERE r.session_id <> @@SPID
ORDER BY r.total_elapsed_time DESC;

-- 2) Top waits acumulados (salud general)
SELECT TOP 15
    wait_type,
    waiting_tasks_count,
    wait_time_ms,
    signal_wait_time_ms
FROM sys.dm_os_wait_stats
WHERE wait_type NOT LIKE 'SLEEP%'
  AND wait_type NOT IN ('CLR_SEMAPHORE','LAZYWRITER_SLEEP','RESOURCE_QUEUE','XE_TIMER_EVENT','XE_DISPATCHER_WAIT')
ORDER BY wait_time_ms DESC;

-- 3) Sesiones bloqueadas ahora
SELECT
    wt.session_id,
    wt.blocking_session_id,
    wt.wait_duration_ms,
    wt.wait_type,
    es.host_name,
    es.program_name,
    es.login_name
FROM sys.dm_os_waiting_tasks wt
JOIN sys.dm_exec_sessions es ON wt.session_id = es.session_id
WHERE wt.blocking_session_id IS NOT NULL
  AND wt.blocking_session_id <> 0
ORDER BY wt.wait_duration_ms DESC;

-- 4) Estado rapido de indices de ronda 3
SELECT
    OBJECT_NAME(i.object_id) AS table_name,
    i.name AS index_name,
    i.is_disabled,
    i.fill_factor
FROM sys.indexes i
WHERE i.name IN (
    'IX_Patients_IsActivo_Apellido_Nombre',
    'IX_Professionals_IsActivo_Apellido_Nombre',
    'IX_Treatments_PatientId_FechaInicio',
    'IX_Sessions_PaymentStatus_FechaHora',
    'IX_AspNetUsers_Email'
)
ORDER BY table_name, index_name;
