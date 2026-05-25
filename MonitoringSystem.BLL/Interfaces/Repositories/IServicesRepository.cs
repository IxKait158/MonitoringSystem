using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IServicesRepository : IRepository<ServiceEntity>
{
    Task<ServiceEntity?> FindByApiKeyAndNameAsync(int apiKeyId, string name);
    Task<List<ServiceEntity>> GetByApiKeyAsync(int apiKeyId);
}
