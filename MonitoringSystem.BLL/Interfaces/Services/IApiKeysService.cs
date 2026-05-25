using MonitoringSystem.BLL.Models.ApiKeys;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Interfaces.Services;

public interface IApiKeysService
{
    Task<string> CreateAsync(CreateApiKeyRequest request);

    ApiKeyDTO GetCurrent(ApiKeyEntity apiKey);

    Task<ApiKeyDTO> DeactivateApiKeyAsync(int id);
}
