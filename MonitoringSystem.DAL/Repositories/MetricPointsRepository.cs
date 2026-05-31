using Microsoft.EntityFrameworkCore;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Repositories;

public class MetricPointsRepository(MonitoringDbContext context) : BaseRepository<MetricPointEntity>(context), IMetricPointsRepository
{
    public async Task<List<string>> GetMetricsNames(List<int> serviceIds)
    {
        return await Context.MetricPoints
            .Where(p => serviceIds.Contains(p.ServiceId))
            .Select(p => p.MetricName)
            .Distinct()
            .ToListAsync();
    }
}
