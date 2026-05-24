using System.Linq.Expressions;
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

    public async Task SaveChangesAsync()
    {
        await Context.SaveChangesAsync();
    }

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

    public IEnumerable<T> GetAll(Expression<Func<T, bool>>? predicate = null)
    {
        IQueryable<T> query = Set;
        if (predicate != null)
            query = query.Where(predicate);
        
        return query.AsEnumerable();
    }

    public IEnumerable<T> GetAllNoTracking(Expression<Func<T, bool>>? predicate = null)
    {
        IQueryable<T> query = Set.AsNoTracking();
        if (predicate != null)
            query = query.Where(predicate);
        
        return query.AsEnumerable();
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await Set.FirstOrDefaultAsync(predicate);
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await Set.FirstOrDefaultAsync(x => x.Id == id);
    }
}
