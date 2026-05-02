using KineGestion.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KineGestion.Data.Context
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Professional> Professionals { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Treatment> Treatments { get; set; }
        public DbSet<Office> Offices { get; set; }
        public DbSet<Equipment> Equipments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ─── GLOBAL QUERY FILTERS (Soft Delete) ──────────────────────────────
            // Office no tiene dependencias inversas requeridas, filtro global seguro.
            // Professional y Patient filtran IsActivo en sus repositorios
            // para evitar NullRef en las navigation properties de Session.
            modelBuilder.Entity<Office>().HasQueryFilter(o => o.IsActive);

            ConfigureAuditableEntity<Patient>(modelBuilder);
            ConfigureAuditableEntity<Professional>(modelBuilder);
            ConfigureAuditableEntity<Treatment>(modelBuilder);
            ConfigureAuditableEntity<Session>(modelBuilder);
            ConfigureAuditableEntity<Office>(modelBuilder);
            ConfigureAuditableEntity<Equipment>(modelBuilder);

            modelBuilder.Entity<Patient>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.DNI).IsRequired().HasMaxLength(8);
                entity.HasIndex(p => p.DNI).IsUnique();
                entity.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Apellido).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Professional>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Apellido).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Matricula).IsRequired().HasMaxLength(20);
                entity.Property(p => p.IsActivo).IsRequired();
                entity.HasIndex(p => p.Matricula).IsUnique();
            });

            modelBuilder.Entity<Treatment>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Descripcion).IsRequired().HasMaxLength(200);

                entity.HasOne(t => t.Patient)
                    .WithMany(p => p.Tratamientos)
                    .HasForeignKey(t => t.PatientId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Session>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Evolution).HasMaxLength(4000);
                entity.Property(s => s.InternalNotes).HasMaxLength(2000);
                entity.Property(s => s.Status).IsRequired();
                entity.Property(s => s.PaymentStatus).IsRequired();

                // Índices para los patrones de consulta más frecuentes
                entity.HasIndex(s => s.ProfessionalId);
                entity.HasIndex(s => s.PatientId);
                entity.HasIndex(s => s.TreatmentId);
                entity.HasIndex(s => new { s.ProfessionalId, s.FechaHora });
                entity.HasIndex(s => new { s.Status, s.FechaHora });

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

                entity.HasOne(s => s.Office)
                    .WithMany(o => o.Sesiones)
                    .HasForeignKey(s => s.OfficeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Office>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Name).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Equipment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

                entity.HasOne(e => e.Office)
                    .WithMany(o => o.Equipments)
                    .HasForeignKey(e => e.OfficeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }

        public override int SaveChanges()
        {
            ApplyAuditInfo();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyAuditInfo();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditInfo();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyAuditInfo();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private static void ConfigureAuditableEntity<TEntity>(ModelBuilder modelBuilder)
            where TEntity : BaseEntity
        {
            modelBuilder.Entity<TEntity>().Property(e => e.CreatedAt).IsRequired();
            modelBuilder.Entity<TEntity>().Property(e => e.UpdatedAt).IsRequired();
        }

        private void ApplyAuditInfo()
        {
            var now = DateTime.UtcNow;

            var entries = ChangeTracker
                .Entries<BaseEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UpdatedAt = now;

                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = now;
                else
                    entry.Property(p => p.CreatedAt).IsModified = false;
            }
        }
    }
}
