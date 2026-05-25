using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.BLL.Interfaces.Services;
using MonitoringSystem.BLL.Models.Services;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.BLL.Services;

public class ServicesService(IServicesRepository servicesRepository) : IServicesService
{
    public async Task<Service> CreateAsync(ApiKeyEntity apiKey, CreateServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new Exception("Ім'я сервісу обов'язкове");

        var name = request.Name.Trim();
        var existing = await servicesRepository.FindByApiKeyAndNameAsync(apiKey.Id, name);
        if (existing != null)
            throw new Exception($"Сервіс '{name}' уже зареєстровано під цим ключем");

        var entity = new ServiceEntity
        {
            Name = name,
            ApiKeyId = apiKey.Id
        };
        await servicesRepository.AddAsync(entity);

        return new Service { Id = entity.Id, Name = entity.Name };
    }

    public async Task<List<Service>> GetAllAsync(ApiKeyEntity apiKey)
    {
        var services = await servicesRepository.GetByApiKeyAsync(apiKey.Id);
        return services
            .Select(s => new Service { Id = s.Id, Name = s.Name })
            .ToList();
    }

    public async Task DeleteAsync(ApiKeyEntity apiKey, int id)
    {
        var entity = await servicesRepository.GetByIdAsync(id);
        if (entity == null || entity.ApiKeyId != apiKey.Id)
            throw new Exception("Сервіс не знайдено");

        await servicesRepository.DeleteAsync(entity);
    }

    public async Task<ServiceEntity> GetOrCreateAsync(ApiKeyEntity apiKey, string name)
    {
        var trimmed = name.Trim();
        var existing = await servicesRepository.FindByApiKeyAndNameAsync(apiKey.Id, trimmed);
        if (existing != null)
            return existing;

        var entity = new ServiceEntity
        {
            Name = trimmed,
            ApiKeyId = apiKey.Id
        };
        await servicesRepository.AddAsync(entity);
        return entity;
    }
}
