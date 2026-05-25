using Microsoft.EntityFrameworkCore;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Repositories;

public class ServicesRepository(MonitoringDbContext context)
    : BaseRepository<ServiceEntity>(context), IServicesRepository
{
    public Task<ServiceEntity?> FindByApiKeyAndNameAsync(int apiKeyId, string name) =>
        Set.FirstOrDefaultAsync(x => x.ApiKeyId == apiKeyId && x.Name == name);

    public Task<List<ServiceEntity>> GetByApiKeyAsync(int apiKeyId) =>
        Set.Where(x => x.ApiKeyId == apiKeyId)
            .OrderBy(x => x.Name)
            .ToListAsync();
}
