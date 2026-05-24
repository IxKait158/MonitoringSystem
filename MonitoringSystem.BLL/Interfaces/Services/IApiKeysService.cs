using MonitoringSystem.BLL.Models.ApiKeys;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IApiKeysService
{
    Task<string> CreateAsync(CreateApiKeyRequest request);
    
    List<ApiKey> GetAll();
    
    Task<ApiKey> DeactivateApiKeyAsync(int id);
}