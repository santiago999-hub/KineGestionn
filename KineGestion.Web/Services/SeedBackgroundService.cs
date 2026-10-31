using KineGestion.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KineGestion.Web.Services;

/// <summary>
/// Ejecuta el seed de roles y usuario admin DESPUÉS de que Kestrel ya esté escuchando,
/// para no bloquear el arranque del puerto HTTP con operaciones de base de datos.
/// </summary>
public sealed class SeedBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedBackgroundService> _logger;
    private readonly TimeSpan _startupDelay;

    public SeedBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SeedBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
        _startupDelay = TimeSpan.FromMilliseconds(
            OperationalConfig.ReadBoundedInt(
                configuration,
                logger,
                "Seed:StartupDelayMs",
                defaultValue: 2000,
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
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            string[] roles = ["Admin", "Kinesiologo", "Asistente"];
            foreach (var role in roles)
            {
                if (stoppingToken.IsCancellationRequested)
                    return;
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            var adminEmail = _configuration["Seed:AdminEmail"] ?? "admin@kinegestion.com";
            var adminPassword = _configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                if (_configuration.GetValue<string>("ASPNETCORE_ENVIRONMENT") == "Development")
                    adminPassword = "Admin1234";
                else
                {
                    _logger.LogError("SEED: Seed:AdminPassword no está configurado. Definirlo vía la variable de entorno Seed__AdminPassword.");
                    return;
                }
            }

            var resetAdminPasswordOnStartup = _configuration.GetValue<bool>("Seed:ResetAdminPasswordOnStartup");
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
                if (resetAdminPasswordOnStartup)
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(existingAdmin);
                    var resetResult = await userManager.ResetPasswordAsync(existingAdmin, token, adminPassword);
                    if (!resetResult.Succeeded)
                        _logger.LogError("SEED: fallo reset contraseña admin: {Errors}", string.Join(", ", resetResult.Errors.Select(e => e.Description)));
                    else
                        _logger.LogInformation("SEED: contraseña admin reseteada correctamente para {Email}", adminEmail);
                    await userManager.SetLockoutEndDateAsync(existingAdmin, null);
                    await userManager.ResetAccessFailedCountAsync(existingAdmin);
                }

                if (!await userManager.IsInRoleAsync(existingAdmin, "Admin"))
                    await userManager.AddToRoleAsync(existingAdmin, "Admin");
            }

            _logger.LogInformation("SEED: roles y usuario de admin verificados.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando el seed de roles y administrador en segundo plano.");
        }
    }
}
