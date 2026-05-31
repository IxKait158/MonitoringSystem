using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IAnomaliesRepository : IRepository<AnomalyEntity>
{
    Task<List<AnomalyEntity>> GetAllRecentAnomaliesAsync(IEnumerable<int> serviceIds, int count);
    Task<List<AnomalyEntity>> GetRecentAnomaliesByMetricAsync(List<int> serviceIds, string metricName, int count);
}
