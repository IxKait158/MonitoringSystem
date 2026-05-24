using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.DAL.Repositories;

namespace MonitoringSystem.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDAL(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MonitoringDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IMetricPointRepository, MetricPointRepository>();
        services.AddScoped<IAnomalyRepository, AnomalyRepository>();

        return services;
    }

    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        
        using var context = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
        
        context.Database.Migrate();
    }
}
