using Microsoft.EntityFrameworkCore;
using KineGestion.Core.Entities;

namespace KineGestion.Data.Context
{
    /// <summary>
    /// Unidad de trabajo (Unit of Work) de Entity Framework Core.
    /// Es el único punto de contacto entre la aplicación y la base de datos.
    /// Solo existe en KineGestion.Data, nunca en Core ni en Web directamente.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSets — representan cada tabla de la base de datos
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Professional> Professionals { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Treatment> Treatments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Patient
            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.DNI).IsRequired().HasMaxLength(8);
                entity.HasIndex(p => p.DNI).IsUnique(); // DNI único en la BD
                entity.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Apellido).IsRequired().HasMaxLength(100);
            });

            // Professional
            modelBuilder.Entity<Professional>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Matricula).IsRequired().HasMaxLength(20);
                entity.HasIndex(p => p.Matricula).IsUnique();
                entity.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
            });

            // Treatment
            modelBuilder.Entity<Treatment>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Descripcion).IsRequired().HasMaxLength(200);
            });

            // Session y sus relaciones
            modelBuilder.Entity<Session>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.HasOne(s => s.Patient)
                      .WithMany(p => p.Sesiones)
                      .HasForeignKey(s => s.PatientId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Professional)
                      .WithMany(p => p.Sesiones)
                      .HasForeignKey(s => s.ProfessionalId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(s => s.Treatment)
                      .WithMany(t => t.Sesiones)
                      .HasForeignKey(s => s.TreatmentId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
