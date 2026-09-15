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
    public class BillingBatchEventRepository : IBillingBatchEventRepository
    {
        private readonly AppDbContext _context;

        public BillingBatchEventRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<BillingBatchEvent>> GetByDateRangeAsync(DateTime? dateFrom, DateTime? dateTo)
        {
            var query = _context.BillingBatchEvents.AsNoTracking().AsQueryable();

            if (dateFrom.HasValue)
                query = query.Where(e => e.CreatedAtUtc >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                query = query.Where(e => e.CreatedAtUtc < dateTo.Value.Date.AddDays(1));

            return await query
                .OrderByDescending(e => e.CreatedAtUtc)
                .ThenByDescending(e => e.Id)
                .ToListAsync();
        }

        public async Task AddAsync(BillingBatchEvent entity)
        {
            _context.BillingBatchEvents.Add(entity);
            await _context.SaveChangesAsync();
        }
    }
}