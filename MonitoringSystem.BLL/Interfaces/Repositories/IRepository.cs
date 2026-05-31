using System.Linq.Expressions;
using MonitoringSystem.BLL.Interfaces.Entities;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IRepository<T> where T : class, IEntity
{
    Task SaveChangesAsync();
    
    Task<int> AddAsync(T t);
    Task AddRangeAsync(IEnumerable<T> entities);
    Task<int> UpdateAsync(T t);
    Task<int> DeleteAsync(T t);
    
    IQueryable<T> GetAll(Expression<Func<T, bool>>? predicate = null);
    IQueryable<T> GetAllNoTracking(Expression<Func<T, bool>>? predicate = null);
    
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<T?> GetByIdAsync(int id);
}