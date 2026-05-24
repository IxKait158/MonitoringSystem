using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IAnomalyRepository : IRepository<AnomalyEntity>
{
    IEnumerable<AnomalyEntity> GetRecentAnomalies(int count);
}
