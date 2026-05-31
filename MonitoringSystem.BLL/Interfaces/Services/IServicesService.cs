using MonitoringSystem.BLL.Models.Services;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IServicesService
{
    Task<ServiceDTO> CreateAsync(ApiKeyEntity apiKey, CreateServiceRequest request);

    Task<List<ServiceDTO>> GetAllAsync(ApiKeyEntity apiKey);

    Task DeleteAsync(ApiKeyEntity apiKey, int id);
}
