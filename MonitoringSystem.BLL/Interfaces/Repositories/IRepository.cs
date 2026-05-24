using MonitoringSystem.Domain.Interfaces;

namespace MonitoringSystem.BLL.Interfaces.Repositories;

public interface IRepository<T> where T : class, IEntity
{
    Task<int> AddAsync(T t);
    Task<int> UpdateAsync(T t);
    Task<int> DeleteAsync(T t);
    
    IEnumerable<T> GetAll();
    IEnumerable<T> GetAllNoTracking();
    
    Task<T?> GetByIdAsync(Guid id);
}