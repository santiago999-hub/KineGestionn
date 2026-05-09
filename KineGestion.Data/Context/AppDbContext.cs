using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KineGestion.Data.Context
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        private sealed class FallbackCurrentUserProvider : ICurrentUserProvider
        {
            public string GetAuditIdentifier() => "system";
        }

        private readonly ICurrentUserProvider _currentUserProvider;

        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserProvider? currentUserProvider = null) : base(options)
        {
            _currentUserProvider = currentUserProvider ?? new FallbackCurrentUserProvider();
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Professional> Professionals { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Treatment> Treatments { get; set; }
        public DbSet<Office> Offices { get; set; }
        public DbSet<Equipment> Equipments { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }

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
                entity.HasIndex(p => new { p.IsActivo, p.Apellido, p.Nombre });
                entity.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Apellido).IsRequired().HasMaxLength(100);

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Patients_FechaNacimiento_Past", "[FechaNacimiento] < CONVERT(date, GETDATE())");
                    t.HasCheckConstraint("CK_Patients_DNI_OnlyDigits", "[DNI] NOT LIKE '%[^0-9]%' AND LEN([DNI]) BETWEEN 7 AND 8");
                });
            });

            modelBuilder.Entity<Professional>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Nombre).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Apellido).IsRequired().HasMaxLength(100);
                entity.Property(p => p.Matricula).IsRequired().HasMaxLength(20);
                entity.Property(p => p.IsActivo).IsRequired();
                entity.HasIndex(p => p.Matricula).IsUnique();
                entity.HasIndex(p => new { p.IsActivo, p.Apellido, p.Nombre });
            });

            modelBuilder.Entity<Treatment>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Descripcion).IsRequired().HasMaxLength(200);
                entity.HasIndex(t => new { t.PatientId, t.FechaInicio });

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Treatments_CantidadSesionesTotales_Positive", "[CantidadSesionesTotales] >= 1");
                });

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

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Sessions_Status_Valid", "[Status] IN (0, 1, 2)");
                    t.HasCheckConstraint("CK_Sessions_PaymentStatus_Valid", "[PaymentStatus] IN (0, 1)");
                    t.HasCheckConstraint("CK_Sessions_NroSesionEnTratamiento_Positive", "[NroSesionEnTratamiento] >= 1");
                });

                // Índices para los patrones de consulta más frecuentes
                entity.HasIndex(s => s.ProfessionalId);
                entity.HasIndex(s => s.PatientId);
                entity.HasIndex(s => s.TreatmentId);
                entity.HasIndex(s => new { s.ProfessionalId, s.FechaHora });
                entity.HasIndex(s => new { s.TreatmentId, s.NroSesionEnTratamiento }).IsUnique();
                entity.HasIndex(s => new { s.Status, s.FechaHora });
                entity.HasIndex(s => new { s.PaymentStatus, s.FechaHora });

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
                entity.HasIndex(o => o.Name).IsUnique();
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

            modelBuilder.Entity<IdentityUser>(entity =>
            {
                entity.HasIndex(u => u.Email);
            });

            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(a => a.EntityId).IsRequired().HasMaxLength(64);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(20);
                entity.Property(a => a.ChangedBy).IsRequired().HasMaxLength(256);
                entity.Property(a => a.ChangedAt).IsRequired();
                entity.HasIndex(a => new { a.EntityName, a.EntityId, a.ChangedAt });
                entity.HasIndex(a => a.ChangedAt);
            });
        }

        public override int SaveChanges()
        {
            return SaveChanges(true);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            PrepareAuditEntries();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return SaveChangesAsync(true, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            PrepareAuditEntries();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void PrepareAuditEntries()
        {
            ApplyAuditInfo();
            AddAuditLogs();
        }

        private static void ConfigureAuditableEntity<TEntity>(ModelBuilder modelBuilder)
            where TEntity : BaseEntity
        {
            modelBuilder.Entity<TEntity>().Property(e => e.CreatedAt).IsRequired();
            modelBuilder.Entity<TEntity>().Property(e => e.UpdatedAt).IsRequired();
            modelBuilder.Entity<TEntity>().Property(e => e.CreatedBy).IsRequired().HasMaxLength(256).HasDefaultValue("system");
            modelBuilder.Entity<TEntity>().Property(e => e.UpdatedBy).IsRequired().HasMaxLength(256).HasDefaultValue("system");
        }

        private void ApplyAuditInfo()
        {
            var now = DateTime.UtcNow;
            var actor = _currentUserProvider.GetAuditIdentifier();

            var entries = ChangeTracker
                .Entries<BaseEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = actor;

                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = actor;
                }
                else
                {
                    entry.Property(p => p.CreatedAt).IsModified = false;
                    entry.Property(p => p.CreatedBy).IsModified = false;
                }
            }
        }

        private void AddAuditLogs()
        {
            var actor = _currentUserProvider.GetAuditIdentifier();
            var now = DateTime.UtcNow;

            var auditEntries = ChangeTracker
                .Entries()
                .Where(e => e.Entity is not AuditLog
                            && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                            && !(e.Entity is IdentityUser)
                            && !e.Metadata.IsOwned())
                .Select(CreateAuditLog)
                .Where(a => a is not null)
                .Select(a => a!)
                .ToList();

            foreach (var auditEntry in auditEntries)
            {
                auditEntry.ChangedBy = actor;
                auditEntry.ChangedAt = now;
            }

            if (auditEntries.Count > 0)
                AuditLogs.AddRange(auditEntries);
        }

        private static AuditLog? CreateAuditLog(EntityEntry entry)
        {
            var entityName = entry.Metadata.ClrType.Name;
            var entityId = GetPrimaryKeyValue(entry);
            var oldValues = new Dictionary<string, object?>();
            var newValues = new Dictionary<string, object?>();

            string action;
            if (entry.State == EntityState.Added)
            {
                action = "Create";
                foreach (var property in entry.Properties.Where(ShouldAuditProperty))
                    newValues[property.Metadata.Name] = property.CurrentValue;
            }
            else if (entry.State == EntityState.Deleted)
            {
                action = "Delete";
                foreach (var property in entry.Properties.Where(ShouldAuditProperty))
                    oldValues[property.Metadata.Name] = property.OriginalValue;
            }
            else
            {
                var isSoftDelete = entry.Properties.Any(p => p.Metadata.Name == nameof(Patient.IsActivo)
                                                             && p.IsModified
                                                             && Equals(p.OriginalValue, true)
                                                             && Equals(p.CurrentValue, false));
                action = isSoftDelete ? "Delete" : "Update";

                foreach (var property in entry.Properties.Where(ShouldAuditProperty).Where(p => p.IsModified))
                {
                    oldValues[property.Metadata.Name] = property.OriginalValue;
                    newValues[property.Metadata.Name] = property.CurrentValue;
                }

                if (oldValues.Count == 0 && newValues.Count == 0)
                    return null;
            }

            return new AuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                Action = action,
                OldValuesJson = oldValues.Count == 0 ? null : JsonSerializer.Serialize(oldValues),
                NewValuesJson = newValues.Count == 0 ? null : JsonSerializer.Serialize(newValues)
            };
        }

        private static string GetPrimaryKeyValue(EntityEntry entry)
        {
            var primaryKey = entry.Metadata.FindPrimaryKey();
            if (primaryKey is null)
                return string.Empty;

            var values = primaryKey.Properties
                .Select(p => entry.Property(p.Name).CurrentValue ?? entry.Property(p.Name).OriginalValue)
                .Where(v => v is not null)
                .Select(v => v!.ToString())
                .ToArray();

            return values.Length == 0 ? string.Empty : string.Join("-", values!);
        }

        private static bool ShouldAuditProperty(PropertyEntry property)
        {
            var name = property.Metadata.Name;
            return !property.Metadata.IsPrimaryKey()
                   && !property.Metadata.IsForeignKey()
                   && name != nameof(AuditLog.Id);
        }
    }
}
