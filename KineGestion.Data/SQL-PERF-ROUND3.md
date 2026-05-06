# Ronda 3 - Perfilado SQL e indices recomendados

Fecha: 06/05/2026

## Objetivo

Reducir latencia en consultas de listado y agenda bajo volumen medio/alto, alineando indices a filtros y ordenamientos reales del codigo.

## Patrones detectados

1. Pacientes
- Filtro frecuente: IsActivo = true.
- Orden frecuente: Apellido, Nombre.
- Busqueda: DNI, ObraSocial, nombre completo.

2. Profesionales
- Filtro frecuente: IsActivo = true.
- Orden frecuente: Apellido, Nombre.
- Busqueda: Matricula, Especialidad, nombre completo.

3. Sesiones
- Filtros frecuentes: ProfessionalId, Status, PaymentStatus, fechas.
- Orden frecuente: FechaHora desc/asc.
- Validacion de conflicto: ProfessionalId + rango FechaHora.

4. Usuarios Identity
- Listado admin: orden y busqueda por Email.
- Join de roles por tabla intermedia UserRoles.

## Cambios aplicados en modelo EF

Se agregaron indices en AppDbContext para:

- Patients(IsActivo, Apellido, Nombre)
- Professionals(IsActivo, Apellido, Nombre)
- Treatments(PatientId, FechaInicio)
- Sessions(PaymentStatus, FechaHora)
- AspNetUsers(Email)

Archivo: KineGestion.Data/Context/AppDbContext.cs

## Script SQL sugerido para bases existentes

Si no se aplican migraciones EF de inmediato, usar este script controlado:

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

## Recomendaciones de medicion

- Comparar antes/despues con STATISTICS IO,TIME en consultas de listados.
- Medir p95 de:
  - Users/Index
  - Sessions/Index
  - Sessions/MyAgenda
- Revisar fragmentation mensual y actualizar estadisticas.
