using System.Linq.Expressions;
using MonitoringSystem.Domain.Interfaces;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IRepository<T> where T : class, IEntity
{
    Task SaveChangesAsync();
    
    Task<int> AddAsync(T t);
    Task<int> UpdateAsync(T t);
    Task<int> DeleteAsync(T t);
    
    IEnumerable<T> GetAll(Expression<Func<T, bool>>? predicate = null);
    IEnumerable<T> GetAllNoTracking(Expression<Func<T, bool>>? predicate = null);
    
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<T?> GetByIdAsync(int id);
}