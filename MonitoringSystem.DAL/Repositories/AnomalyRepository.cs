using Microsoft.EntityFrameworkCore;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Repositories;

public class AnomalyRepository(MonitoringDbContext context)
    : BaseRepository<AnomalyEntity>(context), IAnomalyRepository
{
    public Task<List<AnomalyEntity>> GetRecentAnomaliesAsync(IEnumerable<int> serviceIds, int count)
    {
        var ids = serviceIds.ToList();
        if (ids.Count == 0)
            return Task.FromResult(new List<AnomalyEntity>());

        return Context.Anomalies
            .Include(a => a.Service)
            .Where(a => ids.Contains(a.ServiceId))
            .OrderByDescending(a => a.DetectedAt)
            .Take(count)
            .ToListAsync();
    }
}
