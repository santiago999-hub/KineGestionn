/* ============================================================================
   Seed de DATOS DE DEMO multi-sede para KineGestionDB
   Crea 3 sedes con pacientes/profesionales/tratamientos/sesiones distintos,
   con KPIs variados, para verificar el filtro por sede del dashboard.

   - Idempotente: elimina los datos de demo (por marca en Observaciones / DNI)
     antes de recrearlos. NO toca datos reales.
   - Los campos Telefono/Email de Patient se dejan NULL a proposito: la app los
     guarda ENCRIPTADOS con DataProtection y un valor plano romperia el descifrado.
   - Fechas: el dashboard usa UTC. Se generan sesiones de "hoy" (UTC) con horas
     variadas y varias en los ultimos 30 dias para las tasas.

   Uso:
     sqlcmd -S .\SQLEXPRESS -d KineGestionDB -E -i Seed-MultiSede.sql
   ============================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRAN;

DECLARE @markerTag   NVARCHAR(50) = N'DEMO-MULTISEDE';
DECLARE @nowUtc      DATETIME2    = SYSUTCDATETIME();
DECLARE @todayUtc    DATE         = CAST(@nowUtc AS DATE);

/* ── 1. Limpieza de datos de demo previos ─────────────────────────────────── */
DELETE s FROM Sessions s
 WHERE s.Observaciones LIKE N'%' + @markerTag + N'%'
    OR s.TreatmentId IN (SELECT t.Id FROM Treatments t
                          INNER JOIN Patients p ON t.PatientId = p.Id
                          WHERE p.DNI LIKE '99%')
    OR s.PatientId IN (SELECT Id FROM Patients WHERE DNI LIKE '99%');

DELETE t FROM Treatments t
INNER JOIN Patients p ON t.PatientId = p.Id
 WHERE p.DNI LIKE '99%' OR t.Descripcion LIKE N'%' + @markerTag + N'%';

DELETE FROM Professionals WHERE Matricula LIKE N'DEMO%';
DELETE FROM Patients WHERE DNI LIKE '99%';
DELETE FROM Equipments WHERE Name LIKE N'%' + @markerTag + N'%';
DELETE FROM Offices WHERE Name LIKE N'%' + @markerTag + N'%';

/* ── 2. Sedes ─────────────────────────────────────────────────────────────── */
INSERT INTO Offices (Name, IsActive, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (N'Central '   + @markerTag, 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Norte '     + @markerTag, 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Belgrano '  + @markerTag, 1, N'demo', N'demo', @nowUtc, @nowUtc);

DECLARE @centralId INT = (SELECT Id FROM Offices WHERE Name = N'Central '  + @markerTag);
DECLARE @norteId   INT = (SELECT Id FROM Offices WHERE Name = N'Norte '    + @markerTag);
DECLARE @belgranoId INT = (SELECT Id FROM Offices WHERE Name = N'Belgrano ' + @markerTag);

/* ── 3. Profesionales ─────────────────────────────────────────────────────── */
INSERT INTO Professionals (Nombre, Apellido, Matricula, Especialidad, IsActivo, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (N'Ana',   N'Gomez',   N'DEMO-001', N'Kinesiologia Deportiva', 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Bruno', N'Perez',   N'DEMO-002', N'Rehabilitacion',        1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Carla', N'Rios',    N'DEMO-003', N'Respiratoria',          1, N'demo', N'demo', @nowUtc, @nowUtc);

DECLARE @prof1 INT = (SELECT Id FROM Professionals WHERE Matricula = N'DEMO-001');
DECLARE @prof2 INT = (SELECT Id FROM Professionals WHERE Matricula = N'DEMO-002');
DECLARE @prof3 INT = (SELECT Id FROM Professionals WHERE Matricula = N'DEMO-003');

/* ── 4. Equipamiento por sede ─────────────────────────────────────────────── */
INSERT INTO Equipments (Name, OfficeId, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (N'Camilla Central '  + @markerTag, @centralId,  N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Magnetoterapia '   + @markerTag, @norteId,    N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Bicicleta fija '   + @markerTag, @belgranoId, N'demo', N'demo', @nowUtc, @nowUtc);

/* ── 5. Pacientes (DNI 99xxxxxx para identificarlos) ──────────────────────── */
INSERT INTO Patients (Nombre, Apellido, DNI, FechaNacimiento, ObraSocial, Telefono, Email, IsActivo, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (N'Lucia',  N'Fernandez', '99000001', '1985-03-12', N'OSDE',  NULL, NULL, 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Martin', N'Lopez',     '99000002', '1978-07-25', N'Swiss', NULL, NULL, 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Sofia',  N'Diaz',      '99000003', '1992-11-02', N'Galeno',NULL, NULL, 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Diego',  N'Romero',    '99000004', '1969-01-19', N'IOSFA', NULL, NULL, 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Elena',  N'Sosa',      '99000005', '2000-05-30', NULL,     NULL, NULL, 1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Pablo',  N'Vega',      '99000006', '1988-09-08', N'OSDE',  NULL, NULL, 1, N'demo', N'demo', @nowUtc, @nowUtc);

DECLARE @pat1 INT = (SELECT Id FROM Patients WHERE DNI = '99000001');
DECLARE @pat2 INT = (SELECT Id FROM Patients WHERE DNI = '99000002');
DECLARE @pat3 INT = (SELECT Id FROM Patients WHERE DNI = '99000003');
DECLARE @pat4 INT = (SELECT Id FROM Patients WHERE DNI = '99000004');
DECLARE @pat5 INT = (SELECT Id FROM Patients WHERE DNI = '99000005');
DECLARE @pat6 INT = (SELECT Id FROM Patients WHERE DNI = '99000006');

/* ── 6. Tratamientos ──────────────────────────────────────────────────────── */
INSERT INTO Treatments (Descripcion, CantidadSesionesTotales, FechaInicio, PatientId, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (N'Lumbalgia '        + @markerTag, 10, DATEADD(DAY, -20, @todayUtc), @pat1, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Rodilla post-op '  + @markerTag, 12, DATEADD(DAY, -15, @todayUtc), @pat2, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Rehab hombro '     + @markerTag,  8, DATEADD(DAY, -10, @todayUtc), @pat3, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Kinesio respirat ' + @markerTag,  6, DATEADD(DAY, -5,  @todayUtc), @pat4, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Cervicalgia '      + @markerTag,  8, DATEADD(DAY, -8,  @todayUtc), @pat5, N'demo', N'demo', @nowUtc, @nowUtc),
    (N'Rehab tobillo '    + @markerTag,  6, DATEADD(DAY, -3,  @todayUtc), @pat6, N'demo', N'demo', @nowUtc, @nowUtc);

DECLARE @tr1 INT = (SELECT Id FROM Treatments WHERE Descripcion = N'Lumbalgia '        + @markerTag);
DECLARE @tr2 INT = (SELECT Id FROM Treatments WHERE Descripcion = N'Rodilla post-op '  + @markerTag);
DECLARE @tr3 INT = (SELECT Id FROM Treatments WHERE Descripcion = N'Rehab hombro '     + @markerTag);
DECLARE @tr4 INT = (SELECT Id FROM Treatments WHERE Descripcion = N'Kinesio respirat ' + @markerTag);
DECLARE @tr5 INT = (SELECT Id FROM Treatments WHERE Descripcion = N'Cervicalgia '      + @markerTag);
DECLARE @tr6 INT = (SELECT Id FROM Treatments WHERE Descripcion = N'Rehab tobillo '    + @markerTag);

/* ── 7. Sesiones (Status: 0=Pending 1=Completed 2=Canceled | Payment: 0=Pending 1=Paid)
      NroSesionEnTratamiento unico por tratamiento.
      Horas UTC; @todayUtc 12:00 UTC = 09:00 AR.                               ── */

/* CENTRAL: tratamiento activo, buena cobranza, una cancelacion temprana */
INSERT INTO Sessions (FechaHora, Observaciones, Status, PaymentStatus, NroSesionEnTratamiento, PatientId, ProfessionalId, TreatmentId, OfficeId, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (DATEADD(HOUR, 12, CAST(@todayUtc AS DATETIME2)), @markerTag, 1, 1, 1, @pat1, @prof1, @tr1, @centralId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(HOUR, 14, CAST(@todayUtc AS DATETIME2)), @markerTag, 1, 1, 2, @pat1, @prof1, @tr1, @centralId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(HOUR, 16, CAST(@todayUtc AS DATETIME2)), @markerTag, 0, 0, 3, @pat1, @prof1, @tr1, @centralId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(DAY, -5, DATEADD(HOUR, 13, CAST(@todayUtc AS DATETIME2))), @markerTag, 1, 1, 1, @pat2, @prof2, @tr2, @centralId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(DAY, -3, DATEADD(HOUR, 13, CAST(@todayUtc AS DATETIME2))), @markerTag, 2, 0, 2, @pat2, @prof2, @tr2, @centralId, N'demo', N'demo', @nowUtc, @nowUtc);

UPDATE Sessions SET CancellationReason = 0, CancellationObs = N'Paciente aviso con antelacion', CancelledAt = DATEADD(DAY, -4, @nowUtc)
WHERE Observaciones = @markerTag AND Status = 2 AND TreatmentId = @tr2 AND NroSesionEnTratamiento = 2;

/* NORTE: mucha actividad de hoy, mezcla pagos pendientes */
INSERT INTO Sessions (FechaHora, Observaciones, Status, PaymentStatus, NroSesionEnTratamiento, PatientId, ProfessionalId, TreatmentId, OfficeId, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (DATEADD(HOUR, 11, CAST(@todayUtc AS DATETIME2)), @markerTag, 1, 0, 1, @pat3, @prof3, @tr3, @norteId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(HOUR, 13, CAST(@todayUtc AS DATETIME2)), @markerTag, 1, 0, 2, @pat3, @prof3, @tr3, @norteId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(HOUR, 15, CAST(@todayUtc AS DATETIME2)), @markerTag, 0, 0, 3, @pat3, @prof3, @tr3, @norteId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(HOUR, 17, CAST(@todayUtc AS DATETIME2)), @markerTag, 0, 0, 4, @pat3, @prof3, @tr3, @norteId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(DAY, -7, DATEADD(HOUR, 10, CAST(@todayUtc AS DATETIME2))), @markerTag, 1, 1, 1, @pat4, @prof3, @tr4, @norteId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(DAY, -2, DATEADD(HOUR, 10, CAST(@todayUtc AS DATETIME2))), @markerTag, 1, 0, 2, @pat4, @prof3, @tr4, @norteId, N'demo', N'demo', @nowUtc, @nowUtc);

/* BELGRANO: baja actividad, varias cancelaciones tardias */
INSERT INTO Sessions (FechaHora, Observaciones, Status, PaymentStatus, NroSesionEnTratamiento, PatientId, ProfessionalId, TreatmentId, OfficeId, CreatedBy, UpdatedBy, CreatedAt, UpdatedAt)
VALUES
    (DATEADD(HOUR, 18, CAST(@todayUtc AS DATETIME2)), @markerTag, 0, 0, 1, @pat5, @prof2, @tr5, @belgranoId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(DAY, -6, DATEADD(HOUR, 15, CAST(@todayUtc AS DATETIME2))), @markerTag, 2, 0, 2, @pat5, @prof2, @tr5, @belgranoId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(DAY, -4, DATEADD(HOUR, 15, CAST(@todayUtc AS DATETIME2))), @markerTag, 2, 0, 3, @pat5, @prof2, @tr5, @belgranoId, N'demo', N'demo', @nowUtc, @nowUtc),
    (DATEADD(DAY, -1, DATEADD(HOUR, 16, CAST(@todayUtc AS DATETIME2))), @markerTag, 1, 0, 1, @pat6, @prof2, @tr6, @belgranoId, N'demo', N'demo', @nowUtc, @nowUtc);

/* Cancelaciones tardias (poco margen) para la tasa de cancelacion tardia */
UPDATE Sessions SET CancellationReason = 1, CancellationObs = N'Impuntualidad', CancelledAt = DATEADD(HOUR, 15, CAST(DATEADD(DAY, -6, @todayUtc) AS DATETIME2))
WHERE Observaciones = @markerTag AND TreatmentId = @tr5 AND NroSesionEnTratamiento = 2;

UPDATE Sessions SET CancellationReason = 7, CancellationObs = N'Cierre de consultorio', CancelledAt = DATEADD(HOUR, 15, CAST(DATEADD(DAY, -4, @todayUtc) AS DATETIME2))
WHERE Observaciones = @markerTag AND TreatmentId = @tr5 AND NroSesionEnTratamiento = 3;

COMMIT TRAN;

SELECT 'Sedes'         AS Entidad, COUNT(*) AS Filas FROM Offices WHERE Name LIKE N'%DEMO-MULTISEDE%'
UNION ALL SELECT 'Profesionales', COUNT(*) FROM Professionals WHERE Matricula LIKE N'DEMO%'
UNION ALL SELECT 'Pacientes',     COUNT(*) FROM Patients WHERE DNI LIKE '99%'
UNION ALL SELECT 'Tratamientos',  COUNT(*) FROM Treatments WHERE Descripcion LIKE N'%DEMO-MULTISEDE%'
UNION ALL SELECT 'Sesiones',      COUNT(*) FROM Sessions WHERE Observaciones = N'DEMO-MULTISEDE'
UNION ALL SELECT 'Equipos',       COUNT(*) FROM Equipments WHERE Name LIKE N'%DEMO-MULTISEDE%';
