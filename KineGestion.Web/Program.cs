using KineGestion.Core.Interfaces;
using KineGestion.Core.Services;
using KineGestion.Data.Context;
using KineGestion.Data.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── BASE DE DATOS ────────────────────────────────────────────────────────────
// AppDbContext vive en Data; Program.cs es el único lugar donde se configura.
// La cadena de conexión se lee de appsettings.json (nunca hardcodeada aquí).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── DEPENDENCY INJECTION (Clean Architecture) ────────────────────────────────
// Orden de registro: Repositorios primero, luego Servicios.
// "Scoped" = una instancia por request HTTP (correcto para operaciones de BD).
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();

// ─── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

var app = builder.Build();

// ─── PIPELINE HTTP ────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

