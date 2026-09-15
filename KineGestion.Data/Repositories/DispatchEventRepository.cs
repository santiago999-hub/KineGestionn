using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KineGestion.Core.Entities;
using KineGestion.Core.Interfaces;
using KineGestion.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace KineGestion.Data.Repositories
{
    public class DispatchEventRepository : IDispatchEventRepository
    {
        private readonly AppDbContext _context;

        public DispatchEventRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(DispatchEvent entity)
        {
            _context.DispatchEvents.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<DispatchEvent>> GetByTypeAsync(string dispatchType, DateTime? dateFrom, DateTime? dateTo)
            => await ApplyDateFilters(_context.DispatchEvents.AsNoTracking().Where(e => e.DispatchType == dispatchType), dateFrom, dateTo)
                .OrderByDescending(e => e.SentAtUtc)
                .ThenByDescending(e => e.Id)
                .ToListAsync();

        public async Task<IReadOnlyList<DispatchEvent>> GetByTypePrefixAsync(string typePrefix, DateTime? dateFrom, DateTime? dateTo)
            => await ApplyDateFilters(_context.DispatchEvents.AsNoTracking().Where(e => e.DispatchType.StartsWith(typePrefix)), dateFrom, dateTo)
                .OrderByDescending(e => e.SentAtUtc)
                .ThenByDescending(e => e.Id)
                .ToListAsync();

        public async Task<int> CountByTypeAsync(string dispatchType, DateTime? dateFrom, DateTime? dateTo)
            => await ApplyDateFilters(_context.DispatchEvents.Where(e => e.DispatchType == dispatchType), dateFrom, dateTo)
                .CountAsync();

        private static IQueryable<DispatchEvent> ApplyDateFilters(IQueryable<DispatchEvent> query, DateTime? dateFrom, DateTime? dateTo)
        {
            if (dateFrom.HasValue)
                query = query.Where(e => e.SentAtUtc >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                query = query.Where(e => e.SentAtUtc < dateTo.Value.Date.AddDays(1));

            return query;
        }
    }
}