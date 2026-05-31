using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Repositories;

public class MetricPointsRepository(MonitoringDbContext context) : BaseRepository<MetricPointEntity>(context), IMetricPointsRepository
{
}
