using MonitoringSystem.BLL.Models.ApiKeys;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IApiKeysService
{
    Task<string> CreateAsync(CreateApiKeyRequest request);

    ApiKey GetCurrent(ApiKeyEntity apiKey);

    Task<ApiKey> DeactivateApiKeyAsync(int id);
}
