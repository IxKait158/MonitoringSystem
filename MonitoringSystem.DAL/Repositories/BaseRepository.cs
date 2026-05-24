using Microsoft.EntityFrameworkCore;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.Domain.Interfaces;

namespace MonitoringSystem.DAL.Repositories;

public class BaseRepository<T>(MonitoringDbContext context) : IRepository<T>
    where T : class, IEntity
{
    protected MonitoringDbContext Context { get; } = context;
    protected DbSet<T> Set { get; } = context.Set<T>();

    public async Task<int> AddAsync(T t)
    {
        await Set.AddAsync(t);
        return await Context.SaveChangesAsync();
    }

    public async Task<int> UpdateAsync(T t)
    {
        Set.Update(t);
        return await Context.SaveChangesAsync();
    }

    public async Task<int> DeleteAsync(T t)
    {
        Set.Remove(t);
        return await Context.SaveChangesAsync();
    }

    public IEnumerable<T> GetAll()
    {
        return Set.AsEnumerable();
    }

    public IEnumerable<T> GetAllNoTracking()
    {
        return Set.AsNoTracking();
    }

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await Set.FirstOrDefaultAsync(x => x.Id == id);
    }
}
