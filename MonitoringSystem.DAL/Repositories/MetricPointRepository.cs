using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Repositories;

public class MetricPointRepository(MonitoringDbContext context) : BaseRepository<MetricPointEntity>(context), IMetricPointRepository
{
}
