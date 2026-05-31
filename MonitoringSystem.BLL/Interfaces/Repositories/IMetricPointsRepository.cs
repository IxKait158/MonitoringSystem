using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IMetricPointsRepository : IRepository<MetricPointEntity>
{
    Task<List<string>> GetMetricsNames(List<int> serviceIds);
}
