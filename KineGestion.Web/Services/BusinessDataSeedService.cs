using KineGestion.Core;
using KineGestion.Core.Entities;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KineGestion.Web.Services;

/// <summary>
/// Asegura que existan datos de negocio suficientes (oficinas, profesionales, pacientes,
/// tratamientos y sesiones en los últimos 30 días) para que los dashboards y gráficos de
/// cobranzas muestren información relevante. Es totalmente idempotente: si ya hay datos
/// recientes, no inserta nada.
/// </summary>
public sealed class BusinessDataSeedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BusinessDataSeedService> _logger;
    private readonly TimeSpan _startupDelay;

    public BusinessDataSeedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<BusinessDataSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _startupDelay = TimeSpan.FromMilliseconds(
            OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Seed:StartupDelayMs",
                defaultValue: 4000,
                min: 0,
                max: 300000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_startupDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(_startupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (stoppingToken.IsCancellationRequested) return;

            // ─── SI YA HAY SESIONES RECIENTES, NO HACER NADA ─────────────────────
            var today = DateTime.UtcNow.Date;
            var from = today.AddDays(-29);
            var recentCount = await context.Sessions
                .Where(s => s.FechaHora >= from && s.FechaHora < today.AddDays(1))
                .CountAsync(stoppingToken);

            if (recentCount >= 20)
            {
                _logger.LogInformation("SEED: ya existen {Count} sesiones recientes. Datos de negocio ya cargados.", recentCount);
                return;
            }

            // ─── 1. OFICINAS ─────────────────────────────────────────────────────
            if (!await context.Offices.AnyAsync(stoppingToken))
            {
                var offices = new List<Office>
                {
                    new Office { Name = "Consultorio 1", IsActive = true },
                    new Office { Name = "Consultorio 2", IsActive = true }
                };
                context.Offices.AddRange(offices);
                await context.SaveChangesAsync(stoppingToken);
            }

            var officesList = await context.Offices.ToListAsync(stoppingToken);

            // ─── 2. PROFESIONALES ────────────────────────────────────────────────
            if (!await context.Professionals.AnyAsync(stoppingToken))
            {
                var professionals = new List<Professional>
                {
                    new Professional { Nombre = "Carla", Apellido = "Ríos", Matricula = "K-1001", Especialidad = "Kinesiología Clínica", IsActivo = true },
                    new Professional { Nombre = "Mariana", Apellido = "Suárez", Matricula = "K-1002", Especialidad = "Rehabilitación Deportiva", IsActivo = true },
                    new Professional { Nombre = "Fernando", Apellido = "Domínguez", Matricula = "K-1003", Especialidad = "Kinesiología Respiratoria", IsActivo = true },
                    new Professional { Nombre = "Bruno", Apellido = "Pérez", Matricula = "K-1004", Especialidad = "Traumatología", IsActivo = true },
                    new Professional { Nombre = "Ana", Apellido = "Gómez", Matricula = "K-1005", Especialidad = "Neurología", IsActivo = true },
                    new Professional { Nombre = "Lucas", Apellido = "Fernández", Matricula = "K-1006", Especialidad = "Gerontología", IsActivo = true }
                };
                context.Professionals.AddRange(professionals);
                await context.SaveChangesAsync(stoppingToken);
            }

            var professionalsList = await context.Professionals.Where(p => p.IsActivo).ToListAsync(stoppingToken);

            // ─── 3. PACIENTES ────────────────────────────────────────────────────
            if (!await context.Patients.AnyAsync(stoppingToken))
            {
                var patients = new List<Patient>
                {
                    new Patient { Nombre = "Sofía", Apellido = "Díaz", DNI = "40123456", FechaNacimiento = new DateTime(1988, 5, 14), ObraSocial = "OSDE", Telefono = "11-3333-4444", Email = "sofia.diaz@mail.com", IsActivo = true },
                    new Patient { Nombre = "Valentina", Apellido = "Sosa", DNI = "39876543", FechaNacimiento = new DateTime(1992, 9, 22), ObraSocial = "Swiss Medical", Telefono = "11-4444-5555", Email = "valentina.sosa@mail.com", IsActivo = true },
                    new Patient { Nombre = "Pablo", Apellido = "Vega", DNI = "38112233", FechaNacimiento = new DateTime(1975, 3, 8), ObraSocial = "Galeno", Telefono = "11-5555-6666", Email = "pablo.vega@mail.com", IsActivo = true },
                    new Patient { Nombre = "Diego", Apellido = "Romero", DNI = "41223344", FechaNacimiento = new DateTime(1983, 11, 30), ObraSocial = "OSDE", Telefono = "11-6666-7777", Email = "diego.romero@mail.com", IsActivo = true },
                    new Patient { Nombre = "Martina", Apellido = "Gómez", DNI = "42334455", FechaNacimiento = new DateTime(2001, 7, 17), ObraSocial = "Swiss Medical", Telefono = "11-7777-8888", Email = "martina.gomez@mail.com", IsActivo = true },
                    new Patient { Nombre = "Nicolás", Apellido = "Benítez", DNI = "43445566", FechaNacimiento = new DateTime(1990, 1, 5), ObraSocial = "Galeno", Telefono = "11-8888-9999", Email = "nicolas.benitez@mail.com", IsActivo = true },
                    new Patient { Nombre = "Camila", Apellido = "Rodríguez", DNI = "44556677", FechaNacimiento = new DateTime(1987, 6, 25), ObraSocial = "OSDE", Telefono = "11-2222-3333", Email = "camila.rodriguez@mail.com", IsActivo = true },
                    new Patient { Nombre = "Lucas", Apellido = "Fernández", DNI = "45667788", FechaNacimiento = new DateTime(1980, 12, 12), ObraSocial = "Swiss Medical", Telefono = "11-9999-0000", Email = "lucas.fernandez@mail.com", IsActivo = true }
                };
                context.Patients.AddRange(patients);
                await context.SaveChangesAsync(stoppingToken);
            }

            var patientsList = await context.Patients.Where(p => p.IsActivo).ToListAsync(stoppingToken);

            // ─── 4. TRATAMIENTOS ─────────────────────────────────────────────────
            if (!await context.Treatments.AnyAsync(stoppingToken))
            {
                var treatments = new List<Treatment>();
                int treatmentCounter = 1;
                foreach (var patient in patientsList)
                {
                    int count = patient.Id % 3 + 1;
                    for (int i = 0; i < count; i++)
                    {
                        treatments.Add(new Treatment
                        {
                            Descripcion = $"Tratamiento {treatmentCounter} - {patient.Nombre} {patient.Apellido}",
                            CantidadSesionesTotales = 10 + (i * 2),
                            FechaInicio = DateTime.UtcNow.AddDays(-60 - i * 10),
                            PatientId = patient.Id
                        });
                        treatmentCounter++;
                    }
                }
                context.Treatments.AddRange(treatments);
                await context.SaveChangesAsync(stoppingToken);
            }

            // ─── 5. SESIONES RECIENTES ───────────────────────────────────────────
            var treatmentsByPatient = await context.Treatments
                .Where(t => patientsList.Select(p => p.Id).Contains(t.PatientId))
                .ToListAsync(stoppingToken);

            // Nro de sesión actual por tratamiento (máximo existente + 1)
            var existingSessions = await context.Sessions
                .GroupBy(s => s.TreatmentId)
                .Select(g => new { TreatmentId = g.Key, MaxNro = g.Max(s => s.NroSesionEnTratamiento) })
                .ToListAsync(stoppingToken);
            var nroSesionByTreatment = existingSessions.ToDictionary(e => e.TreatmentId, e => (int)(e.MaxNro + 1));

            var sessions = new List<Session>();
            Random random = new Random(42);

            for (int dayOffset = 29; dayOffset >= 0; dayOffset--)
            {
                DateTime baseDate = today.AddDays(-dayOffset);
                int sessionsToday = random.Next(2, 7);

                for (int s = 0; s < sessionsToday; s++)
                {
                    var patient = patientsList[random.Next(patientsList.Count)];
                    var professional = professionalsList[random.Next(professionalsList.Count)];
                    var patientTreatments = treatmentsByPatient.Where(t => t.PatientId == patient.Id).ToList();
                    if (patientTreatments.Count == 0) continue;

                    var treatment = patientTreatments[random.Next(patientTreatments.Count)];
                    var office = officesList[random.Next(officesList.Count)];

                    if (!nroSesionByTreatment.TryGetValue(treatment.Id, out int nroSesion))
                        nroSesion = 1;

                    if (nroSesion > treatment.CantidadSesionesTotales) continue;

                    nroSesionByTreatment[treatment.Id] = nroSesion + 1;

                    int statusRoll = random.Next(100);
                    SessionStatus status = statusRoll < 70 ? SessionStatus.Completed :
                                           statusRoll < 90 ? SessionStatus.Pending :
                                                             SessionStatus.Canceled;

                    PaymentStatus paymentStatus;
                    if (status == SessionStatus.Completed)
                    {
                        paymentStatus = random.Next(100) < 60 ? PaymentStatus.Paid : PaymentStatus.Pending;
                    }
                    else
                    {
                        paymentStatus = PaymentStatus.Pending;
                    }

                    int hour = random.Next(8, 18);
                    int minute = random.Next(0, 60);
                    DateTime fechaHora = baseDate.AddHours(hour).AddMinutes(minute);

                    sessions.Add(new Session
                    {
                        FechaHora = fechaHora,
                        Status = status,
                        PaymentStatus = paymentStatus,
                        PatientId = patient.Id,
                        ProfessionalId = professional.Id,
                        TreatmentId = treatment.Id,
                        OfficeId = office.Id,
                        NroSesionEnTratamiento = nroSesion,
                        Observaciones = $"Sesión {nroSesion} de control - observación de prueba",
                        InternalNotes = "Nota interna de prueba",
                        CreatedBy = "system",
                        UpdatedBy = "system"
                    });
                }
            }

            context.Sessions.AddRange(sessions);
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("SEED: datos de negocio insertados - {SessionCount} sesiones recientes.", sessions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando el seed de datos de negocio.");
        }
    }
}
