USE KineGestionDB;
GO

SET XACT_ABORT ON;
BEGIN TRAN;

UPDATE Patients SET Apellido = N'Gómez',     Nombre = N'Martina',   DNI = N'38472615', Telefono = N'1145678921' WHERE DNI = N'46666666';
UPDATE Patients SET Apellido = N'Fernández', Nombre = N'Lucas',     DNI = N'41258963', Telefono = N'1145896321' WHERE DNI = N'99999999';
UPDATE Patients SET Apellido = N'Rodríguez', Nombre = N'Camila',    DNI = N'36987145', Telefono = N'2234456789' WHERE DNI = N'99999998';
UPDATE Patients SET Apellido = N'Benítez',   Nombre = N'Nicolás',   DNI = N'40123578', Telefono = N'2268456123' WHERE DNI = N'4669630';
UPDATE Patients SET Apellido = N'Sosa',      Nombre = N'Valentina', DNI = N'42789156', Telefono = N'1136987452' WHERE DNI = N'23456464';
UPDATE Patients SET Apellido = N'Acosta',    Nombre = N'Joaquín',   DNI = N'39512648', Telefono = N'2235678912' WHERE DNI = N'35789456';

UPDATE Professionals SET Apellido = N'Suárez',    Nombre = N'Mariana',   Matricula = N'4521' WHERE Matricula = N'1234567890';
UPDATE Professionals SET Apellido = N'Domínguez', Nombre = N'Fernando', Matricula = N'3874' WHERE Matricula = N'12345678';

UPDATE Professionals SET Especialidad = N'Kinesiología general'    WHERE Matricula = N'4521';
UPDATE Professionals SET Especialidad = N'Kinesiología deportiva' WHERE Matricula = N'3874';

UPDATE AuditLogs SET NewValuesJson = REPLACE(REPLACE(REPLACE(REPLACE(NewValuesJson,
    N'"Apellido":"rinozo"', N'"Apellido":"Domínguez"'),
    N'"Nombre":"mario"', N'"Nombre":"Fernando"'),
    N'"Matricula":"12345678"', N'"Matricula":"3874"'),
    N'"Especialidad":"kineciologia y lecciones en futbol"', N'"Especialidad":"Kinesiología deportiva"')
WHERE EntityName = N'Professional' AND Action = N'Create' AND EntityId = 2;

COMMIT;
GO
