using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Repositories;

public class AnomalyRepository(MonitoringDbContext context) : BaseRepository<AnomalyEntity>(context), IAnomalyRepository
{
    public IEnumerable<AnomalyEntity> GetRecentAnomalies(int count)
    {
        return Context.Anomalies
            .OrderByDescending(a => a.DetectedAt)
            .Take(count);
    }
}
