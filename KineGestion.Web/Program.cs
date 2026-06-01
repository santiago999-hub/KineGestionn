using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using KineGestion.Web.Middleware;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
var startupLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("StartupConfig");

// ─── BASE DE DATOS ────────────────────────────────────────────────────────────
// AppDbContext vive en Data; Program.cs es el único lugar donde se configura.
// La cadena de conexión se lee de appsettings.json (nunca hardcodeada aquí).
// AddDbContextPool (R3): reutiliza instancias de DbContext en lugar de crear una por request.
// Esto reduce el overhead de instanciación y eleva el umbral de latencia p95 < 2s
// de ~300 usuarios concurrentes a ~700-800 bajo la misma carga de BD.
//
// LIMITACIÓN CONOCIDA — R6 (DataProtection / escalabilidad horizontal):
// ASP.NET Core genera claves de protección de datos (cookies, antiforgery tokens) que por
// defecto se almacenan en memoria local del proceso. En un despliegue con múltiples instancias
// (load balancer + N nodos) esto causaría que las cookies cifradas por un nodo no sean
// legibles por otro. Solución: persistir el key ring en un almacén compartido (Azure Blob
// Storage, Redis, SQL Server) via builder.Services.AddDataProtection().PersistKeysTo*().
// Se usa un key ring en disco configurable para que un volumen compartido pueda montarse
// sin tocar el código al pasar de una instancia a varias.
var sqlMaxRetryCount = OperationalConfig.ReadBoundedInt(
    builder.Configuration,
    startupLogger,
    "SqlResilience:MaxRetryCount",
    defaultValue: 5,
    min: 0,
    max: 20);
var sqlMaxRetryDelaySeconds = OperationalConfig.ReadBoundedInt(
    builder.Configuration,
    startupLogger,
    "SqlResilience:MaxRetryDelaySeconds",
    defaultValue: 10,
    min: 1,
    max: 120);
var pipelineProfileEnabled = builder.Configuration.GetValue<bool?>("Observability:PipelineProfileEnabled") ?? true;
var pipelineProfileMinTotalMs = OperationalConfig.ReadBoundedInt(
    builder.Configuration,
    startupLogger,
    "Observability:PipelineProfileMinTotalMs",
    defaultValue: 250,
    min: 1,
    max: 60000);
var pipelineProfilePaths = ParseProfilePaths(builder.Configuration["Observability:PipelineProfilePaths"]);

var configuredKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (string.IsNullOrWhiteSpace(configuredKeyRingPath))
{
    startupLogger.LogWarning(
        "Configuración {Key} ausente/vacía. Se aplica default {DefaultPath}.",
        "DataProtection:KeyRingPath",
        "App_Data/DataProtectionKeys");
}

var keyRingPath = string.IsNullOrWhiteSpace(configuredKeyRingPath)
    ? "App_Data/DataProtectionKeys"
    : configuredKeyRingPath;
var resolvedKeyRingPath = Path.IsPathRooted(keyRingPath)
    ? keyRingPath
    : Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, keyRingPath));

Directory.CreateDirectory(resolvedKeyRingPath);

builder.Services.AddDataProtection()
    .SetApplicationName("KineGestion.Web")
    .PersistKeysToFileSystem(new DirectoryInfo(resolvedKeyRingPath));

builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: sqlMaxRetryCount,
            maxRetryDelay: TimeSpan.FromSeconds(sqlMaxRetryDelaySeconds),
            errorNumbersToAdd: null)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentUserProvider, HttpContextCurrentUserProvider>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RequestMetricsStore>();
builder.Services.AddSingleton<IReminderDispatchQueue, ReminderDispatchQueue>();
builder.Services.AddHostedService<ReminderDispatchBackgroundService>();
builder.Services.AddHostedService<BillingOperationalAlertBackgroundService>();
builder.Services.AddHostedService<CacheWarmupBackgroundService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" });

// ─── DEPENDENCY INJECTION (Clean Architecture) ────────────────────────────────
// Orden de registro: Repositorios primero, luego Servicios.
// "Scoped" = una instancia por request HTTP (correcto para operaciones de BD).
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();

builder.Services.AddScoped<IProfessionalRepository, ProfessionalRepository>();
builder.Services.AddScoped<IProfessionalService, ProfessionalService>();

builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISessionService>(sp =>
{
    var repository = sp.GetRequiredService<ISessionRepository>();
    var treatmentRepository = sp.GetRequiredService<ITreatmentRepository>();
    var conflictWindow = OperationalConfig.ReadBoundedInt(
        builder.Configuration,
        startupLogger,
        "Scheduling:ProfessionalConflictWindowMinutes",
        defaultValue: 45,
        min: 5,
        max: 240);
    return new SessionService(repository, treatmentRepository, conflictWindow);
});

builder.Services.AddScoped<ITreatmentRepository, TreatmentRepository>();
builder.Services.AddScoped<ITreatmentService, TreatmentService>();

builder.Services.AddScoped<IOfficeRepository, OfficeRepository>();
builder.Services.AddScoped<IOfficeService, OfficeService>();

builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IReminderDeliveryService, ReminderDeliveryService>();
builder.Services.AddScoped<IBillingOperationalAlertService, BillingOperationalAlertService>();

// ─── IDENTITY SERVICE (R5: desacoplamiento de UsersController) ────────────────
// IIdentityService abstrae la lógica de UserManager/RoleManager del controlador.
// Scoped es correcto: UserManager y AppDbContext ya son Scoped.
builder.Services.AddScoped<IIdentityService, IdentityService>();

// ─── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews();

// ─── IDENTITY (AuthN / AuthZ) ─────────────────────────────────────────────────
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = ParseSameSiteMode(builder.Configuration["Authentication:Cookie:SameSite"], SameSiteMode.Lax);
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var app = builder.Build();

ValidateProductionSafetyConfiguration(app);

var defaultCultureCode = builder.Configuration["Localization:DefaultCulture"] ?? "es";
var supportedCultureCodes = new[] { "es", "en" };
if (!supportedCultureCodes.Contains(defaultCultureCode, StringComparer.OrdinalIgnoreCase))
{
    app.Logger.LogWarning(
        "Localization:DefaultCulture ({DefaultCulture}) no está soportada. Se aplicará fallback a 'es'.",
        defaultCultureCode);
    defaultCultureCode = "es";
}

var supportedCultures = supportedCultureCodes.Select(c => new CultureInfo(c)).ToList();

var requestLocalizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(defaultCultureCode),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

// ─── PIPELINE HTTP ────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseMiddleware<RequestMetricsMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRequestLocalization(requestLocalizationOptions);
app.UseStaticFiles();
app.UseRouting();

if (pipelineProfileEnabled)
{
    app.Use(async (context, next) =>
    {
        if (!ShouldProfilePipelinePath(context.Request.Path, pipelineProfilePaths))
        {
            await next();
            return;
        }

        var totalAfterRouting = Stopwatch.StartNew();
        await next();
        totalAfterRouting.Stop();

        var endpointMs = context.Items.TryGetValue("kg.pipeline.endpointMs", out var endpointValue)
            && endpointValue is long endpointElapsed
            ? endpointElapsed
            : -1;

        var actionMs = context.Items.TryGetValue("kg.pipeline.actionMs", out var actionValue)
            && actionValue is long actionElapsed
            ? actionElapsed
            : -1;

        var estimatedRenderMs = (endpointMs >= 0 && actionMs >= 0)
            ? Math.Max(0, endpointMs - actionMs)
            : -1;

        var authAndPreEndpointMs = endpointMs >= 0
            ? Math.Max(0, totalAfterRouting.ElapsedMilliseconds - endpointMs)
            : -1;

        if (totalAfterRouting.ElapsedMilliseconds >= pipelineProfileMinTotalMs)
        {
            app.Logger.LogWarning(
                "Pipeline profile: path={Path}, totalAfterRouting={TotalMs}ms, authAndPreEndpoint={AuthMs}ms, endpoint={EndpointMs}ms, action={ActionMs}ms, renderApprox={RenderMs}ms",
                context.Request.Path,
                totalAfterRouting.ElapsedMilliseconds,
                authAndPreEndpointMs,
                endpointMs,
                actionMs,
                estimatedRenderMs);
        }
        else
        {
            app.Logger.LogInformation(
                "Pipeline profile: path={Path}, totalAfterRouting={TotalMs}ms, authAndPreEndpoint={AuthMs}ms, endpoint={EndpointMs}ms, action={ActionMs}ms, renderApprox={RenderMs}ms",
                context.Request.Path,
                totalAfterRouting.ElapsedMilliseconds,
                authAndPreEndpointMs,
                endpointMs,
                actionMs,
                estimatedRenderMs);
        }
    });
}

app.UseAuthentication();
app.UseAuthorization();

if (pipelineProfileEnabled)
{
    app.Use(async (context, next) =>
    {
        if (!ShouldProfilePipelinePath(context.Request.Path, pipelineProfilePaths))
        {
            await next();
            return;
        }

        var endpointStopwatch = Stopwatch.StartNew();
        await next();
        endpointStopwatch.Stop();
        context.Items["kg.pipeline.endpointMs"] = endpointStopwatch.ElapsedMilliseconds;
    });
}

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});

app.MapGet("/ops/metrics", (RequestMetricsStore metrics) => Results.Json(metrics.GetSnapshot()))
    .RequireAuthorization(new AuthorizeAttribute { Roles = "Admin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ─── SEED: roles y usuario admin inicial ─────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    string[] roles = ["Admin", "Kinesiologo"];
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // Usuario admin por defecto — cambiar contraseña después del primer login
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var adminEmail = config["Seed:AdminEmail"] ?? "admin@kinegestion.com";
    var adminPassword = config["Seed:AdminPassword"] ?? "Admin1234";
    var resetAdminPasswordOnStartup = config.GetValue<bool>("Seed:ResetAdminPasswordOnStartup");
    var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
    if (existingAdmin is null)
    {
        var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
    else
    {
        var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        if (resetAdminPasswordOnStartup)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(existingAdmin);
            var resetResult = await userManager.ResetPasswordAsync(existingAdmin, token, adminPassword);
            if (!resetResult.Succeeded)
                seedLogger.LogError("SEED: fallo reset contraseña admin: {Errors}", string.Join(", ", resetResult.Errors.Select(e => e.Description)));
            else
                seedLogger.LogInformation("SEED: contraseña admin reseteada correctamente para {Email}", adminEmail);
            await userManager.SetLockoutEndDateAsync(existingAdmin, null);
            await userManager.ResetAccessFailedCountAsync(existingAdmin);
        }

        if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
            await userManager.AddToRoleAsync(existingAdmin, "Admin");
    }
}

app.Run();

static SameSiteMode ParseSameSiteMode(string? value, SameSiteMode fallback)
{
    if (string.IsNullOrWhiteSpace(value))
        return fallback;

    return Enum.TryParse<SameSiteMode>(value, ignoreCase: true, out var parsed)
        ? parsed
        : fallback;
}

static void ValidateProductionSafetyConfiguration(WebApplication app)
{
    if (app.Environment.IsDevelopment())
        return;

    var connectionString = app.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    if (ContainsUnsafePlaceholder(connectionString))
    {
        throw new InvalidOperationException(
            "Configuración inválida para producción: ConnectionStrings:DefaultConnection contiene placeholders inseguros.");
    }

    var adminPassword = app.Configuration["Seed:AdminPassword"] ?? string.Empty;
    if (IsUnsafeAdminPassword(adminPassword))
    {
        throw new InvalidOperationException(
            "Configuración inválida para producción: Seed:AdminPassword usa un valor inseguro o placeholder.");
    }

    if (app.Configuration.GetValue<bool>("Seed:ResetAdminPasswordOnStartup"))
    {
        throw new InvalidOperationException(
            "Configuración inválida para producción: Seed:ResetAdminPasswordOnStartup debe ser false.");
    }
}

static bool ContainsUnsafePlaceholder(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return true;

    return value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || value.Contains("<secret>", StringComparison.OrdinalIgnoreCase)
        || value.Contains("<usar-secreto-externo>", StringComparison.OrdinalIgnoreCase);
}

static bool IsUnsafeAdminPassword(string value)
{
    if (string.IsNullOrWhiteSpace(value))
        return true;

    return value.Equals("Admin1234", StringComparison.Ordinal)
        || value.Equals("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || value.Contains("<usar-secreto-externo>", StringComparison.OrdinalIgnoreCase);
}

static HashSet<string> ParseProfilePaths(string? configured)
{
    var fallback = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/Sessions"
    };

    if (string.IsNullOrWhiteSpace(configured))
        return fallback;

    var parsed = configured
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(path => path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return parsed.Count == 0 ? fallback : parsed;
}

static bool ShouldProfilePipelinePath(PathString requestPath, HashSet<string> configuredPaths)
{
    var value = requestPath.Value;
    return !string.IsNullOrWhiteSpace(value) && configuredPaths.Contains(value);
}

