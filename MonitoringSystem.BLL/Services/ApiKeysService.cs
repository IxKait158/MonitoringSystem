using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.ApiKeys;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Services;

public class ApiKeysService(IApiKeysRepository apiKeysRepository) : IApiKeysService
{
    public async Task<string> CreateAsync(CreateApiKeyRequest request)
    {
        if (string.IsNullOrEmpty(request.ServiceName))
            throw new Exception("Ім'я сервісу обов'язкове");

        var key = $"mk_{Guid.NewGuid():N}{Guid.NewGuid():N}"[..36];

        var apiKey = new ApiKeyEntity
        {
            Key = key,
            ServiceName = request.ServiceName,
            Owner = string.IsNullOrEmpty(request.Owner) ? "unknown" : request.Owner,
            IsActive = true
        };
        
        await apiKeysRepository.AddAsync(apiKey);

        return key;
    }

    public List<ApiKey> GetAll()
    {
        var keys = apiKeysRepository
            .GetAllNoTracking()
            .Select(x => new ApiKey
            {
                Id = x.Id,
                Key = x.Key[..8] + "...",
                ServiceName = x.ServiceName,
                Owner = x.Owner,
                IsActive = x.IsActive,
                CreatedAt = x.CreatedAt,
                LastUsedAt = x.LastUsedAt
            })
            .ToList();

        return keys;
    }

    public async Task<ApiKey> DeactivateApiKeyAsync(int id)
    {
        var key = await apiKeysRepository.GetByIdAsync(id);
        if (key == null)
            throw new Exception("Ключ не знайдено");
        
        key.IsActive = false;
        await apiKeysRepository.UpdateAsync(key);

        return new ApiKey
        {
            Id = key.Id,
            Key = key.Key[..8] + "...",
            ServiceName = key.ServiceName,
            Owner = key.Owner,
            IsActive = key.IsActive,
            CreatedAt = key.CreatedAt,
            LastUsedAt = key.LastUsedAt
        };
    }
}