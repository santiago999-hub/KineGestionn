using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KineGestion.Core.Entities;

namespace KineGestion.Core.Interfaces
{
    public interface IDispatchEventRepository
    {
        Task AddAsync(DispatchEvent entity);

        Task<IReadOnlyList<DispatchEvent>> GetByTypeAsync(string dispatchType, DateTime? dateFrom, DateTime? dateTo);

        Task<IReadOnlyList<DispatchEvent>> GetByTypePrefixAsync(string typePrefix, DateTime? dateFrom, DateTime? dateTo);

        Task<int> CountByTypeAsync(string dispatchType, DateTime? dateFrom, DateTime? dateTo);
    }
}