using MonitoringSystem.BLL.Models.Services;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IServicesService
{
    Task<Service> CreateAsync(ApiKeyEntity apiKey, CreateServiceRequest request);

    Task<List<Service>> GetAllAsync(ApiKeyEntity apiKey);

    Task DeleteAsync(ApiKeyEntity apiKey, int id);

    Task<ServiceEntity> GetOrCreateAsync(ApiKeyEntity apiKey, string name);
}
