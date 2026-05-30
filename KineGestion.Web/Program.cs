using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using KineGestion.Web.Middleware;
using KineGestion.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

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
// Para el contexto de esta aplicación (instancia única) el comportamiento por defecto es
// suficiente y no representa una vulnerabilidad.
var sqlMaxRetryCount = Math.Max(0, builder.Configuration.GetValue<int?>("SqlResilience:MaxRetryCount") ?? 5);
var sqlMaxRetryDelaySeconds = Math.Max(1, builder.Configuration.GetValue<int?>("SqlResilience:MaxRetryDelaySeconds") ?? 10);

builder.Services.AddDbContextPool<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: sqlMaxRetryCount,
            maxRetryDelay: TimeSpan.FromSeconds(sqlMaxRetryDelaySeconds),
            errorNumbersToAdd: null)));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICurrentUserProvider, HttpContextCurrentUserProvider>();

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
    var conflictWindow = builder.Configuration.GetValue<int?>("Scheduling:ProfessionalConflictWindowMinutes") ?? 45;
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
});

var app = builder.Build();

var defaultCultureCode = builder.Configuration["Localization:DefaultCulture"] ?? "es";
var supportedCultureCodes = new[] { "es", "en" };
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

app.UseMiddleware<GlobalExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRequestLocalization(requestLocalizationOptions);
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

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

