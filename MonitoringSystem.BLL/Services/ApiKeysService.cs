using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.ApiKeys;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Services;

public class ApiKeysService(IApiKeysRepository apiKeysRepository) : IApiKeysService
{
    public async Task<string> CreateAsync(CreateApiKeyRequest request)
    {
        var key = $"mk_{Guid.NewGuid():N}{Guid.NewGuid():N}"[..36];

        var apiKey = new ApiKeyEntity
        {
            Key = key,
            Owner = string.IsNullOrWhiteSpace(request.Owner) ? "unknown" : request.Owner.Trim(),
            IsActive = true
        };

        await apiKeysRepository.AddAsync(apiKey);

        return key;
    }

    public ApiKeyDTO GetCurrent(ApiKeyEntity apiKey) =>
        new()
        {
            Id = apiKey.Id,
            Key = apiKey.Key[..8] + "...",
            Owner = apiKey.Owner,
            IsActive = apiKey.IsActive,
            CreatedAt = apiKey.CreatedAt,
            LastUsedAt = apiKey.LastUsedAt,
            ServiceCount = apiKey.Services?.Count ?? 0
        };

    public async Task<ApiKeyDTO> DeactivateApiKeyAsync(int id)
    {
        var key = await apiKeysRepository.GetByIdAsync(id);
        if (key == null)
            throw new Exception("Ключ не знайдено");

        key.IsActive = false;
        await apiKeysRepository.UpdateAsync(key);

        return new ApiKeyDTO
        {
            Id = key.Id,
            Key = key.Key[..8] + "...",
            Owner = key.Owner,
            IsActive = key.IsActive,
            CreatedAt = key.CreatedAt,
            LastUsedAt = key.LastUsedAt
        };
    }
}
