using Microsoft.EntityFrameworkCore;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Repositories;

public class AnomalyRepository(MonitoringDbContext context)
    : BaseRepository<AnomalyEntity>(context), IAnomalyRepository
{
    public async Task<List<AnomalyEntity>> GetAllRecentAnomaliesAsync(IEnumerable<int> serviceIds, int count)
    {
        var ids = serviceIds.ToList();
        if (ids.Count == 0)
            return await Task.FromResult(new List<AnomalyEntity>());

        return await Context.Anomalies
            .Include(a => a.Service)
            .Where(a => ids.Contains(a.ServiceId))
            .OrderByDescending(a => a.DetectedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<AnomalyEntity>> GetRecentAnomaliesByMetricAsync(List<int> serviceIds, string metricName, int count)
    {
        var ids = serviceIds.ToList();
        if (ids.Count == 0)
            return await Task.FromResult(new List<AnomalyEntity>());

        return await Context.Anomalies
            .Include(a => a.Service)
            .Where(a => ids.Contains(a.ServiceId) && a.MetricName == metricName)
            .OrderByDescending(a => a.DetectedAt)
            .Take(count)
            .ToListAsync();
    }
}