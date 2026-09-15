using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IBillingBatchEventRepository
    {
        Task<IReadOnlyList<BillingBatchEvent>> GetByDateRangeAsync(DateTime? dateFrom, DateTime? dateTo);
        Task AddAsync(BillingBatchEvent entity);
    }
}