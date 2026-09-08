using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.DTOs;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Data.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly AppDbContext _context;

        public AuditLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedAsync(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo,
            int page,
            int pageSize)
        {
            var query = BuildQuery(entityName, entityId, changedBy, action, dateFrom, dateTo);

            query = query.OrderByDescending(a => a.ChangedAt).ThenByDescending(a => a.Id);

            var totalCount = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            return (items, totalCount);
        }

        public async Task<IEnumerable<AuditLog>> GetAllAsync(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var query = BuildQuery(entityName, entityId, changedBy, action, dateFrom, dateTo)
                .OrderByDescending(a => a.ChangedAt)
                .ThenByDescending(a => a.Id);

            return await query.ToListAsync();
        }

        public async Task<AuditAnalyticsData> GetAnalyticsAsync(DateTime? dateFrom, DateTime? dateTo)
        {
            var (from, toExclusive) = NormalizeDateRange(dateFrom, dateTo);
            var query = _context.AuditLogs.AsNoTracking()
                .Where(a => a.ChangedAt >= from && a.ChangedAt < toExclusive);

            var totalCount = await query.CountAsync();

            var byAction = await query
                .GroupBy(a => a.Action)
                .Select(g => new AuditMetricItem { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .ToListAsync();

            var byEntity = await query
                .GroupBy(a => a.EntityName)
                .Select(g => new AuditMetricItem { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .ToListAsync();

            var byUser = await query
                .GroupBy(a => a.ChangedBy)
                .Select(g => new AuditMetricItem { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .Take(25)
                .ToListAsync();

            var dailyTrend = await query
                .GroupBy(a => a.ChangedAt.Date)
                .Select(g => new AuditDailyPoint { DateUtc = g.Key, Count = g.Count() })
                .OrderBy(x => x.DateUtc)
                .ToListAsync();

            return new AuditAnalyticsData
            {
                FromUtc = from,
                ToUtc = toExclusive.AddDays(-1),
                TotalCount = totalCount,
                ByAction = byAction,
                ByEntity = byEntity,
                ByUser = byUser,
                DailyTrend = dailyTrend
            };
        }

        public async Task<AuditLog> AddAsync(AuditLog auditLog)
        {
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
            return auditLog;
        }

        private IQueryable<AuditLog> BuildQuery(
            string? entityName,
            string? entityId,
            string? changedBy,
            string? action,
            DateTime? dateFrom,
            DateTime? dateTo)
        {
            var query = _context.AuditLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(entityName))
                query = query.Where(a => a.EntityName == entityName);

            if (!string.IsNullOrWhiteSpace(entityId))
                query = query.Where(a => a.EntityId == entityId);

            if (!string.IsNullOrWhiteSpace(changedBy))
                query = query.Where(a => a.ChangedBy.Contains(changedBy));

            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);

            if (dateFrom.HasValue)
            {
                var from = dateFrom.Value.Date;
                query = query.Where(a => a.ChangedAt >= from);
            }

            if (dateTo.HasValue)
            {
                var toExclusive = dateTo.Value.Date.AddDays(1);
                query = query.Where(a => a.ChangedAt < toExclusive);
            }

            return query;
        }

        private static (DateTime From, DateTime ToExclusive) NormalizeDateRange(DateTime? dateFrom, DateTime? dateTo)
        {
            var from = (dateFrom ?? DateTime.UtcNow.AddDays(-90)).Date;
            var toExclusive = (dateTo ?? DateTime.UtcNow).Date.AddDays(1);

            if (from >= toExclusive)
            {
                from = toExclusive.AddDays(-1);
            }

            return (from, toExclusive);
        }
    }
}