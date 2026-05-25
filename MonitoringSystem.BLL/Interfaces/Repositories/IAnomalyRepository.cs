using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IAnomalyRepository : IRepository<AnomalyEntity>
{
    Task<List<AnomalyEntity>> GetRecentAnomaliesAsync(IEnumerable<int> serviceIds, int count);
}
