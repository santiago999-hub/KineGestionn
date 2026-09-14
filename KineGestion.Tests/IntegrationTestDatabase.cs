using System.Threading.Tasks;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Tests
{
    /// <summary>
    /// Infraestructura común para los tests de integración sobre SQL Server.
    /// Cada test usa una BD efímera con nombre único (GUID) que se crea y elimina.
    /// </summary>
    internal static class IntegrationTestDatabase
    {
        public static DbContextOptions<AppDbContext> BuildOptions(string databaseName)
        {
            var connectionString = TestConnection.For(databaseName);
            return new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;
        }

        /// <summary>
        /// Crea la BD y el esquema con MigrateAsync (esquema real, incluye índices únicos filtrados).
        /// </summary>
        public static async Task<AppDbContext> CreateMigratedAsync(string databaseName)
        {
            var context = new AppDbContext(BuildOptions(databaseName));
            await context.Database.EnsureDeletedAsync();
            await context.Database.MigrateAsync();
            return context;
        }

        /// <summary>
        /// Crea la BD y el esquema con EnsureDeleted/EnsureCreated (rápido; no ejecuta migraciones).
        /// </summary>
        public static async Task<AppDbContext> CreateSchemaAsync(string databaseName)
        {
            var context = new AppDbContext(BuildOptions(databaseName));
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            return context;
        }
    }
}