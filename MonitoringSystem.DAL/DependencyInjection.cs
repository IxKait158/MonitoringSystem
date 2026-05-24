using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonitoringSystem.BLL.Interfaces.Repositories;
using MonitoringSystem.DAL.Data;
using MonitoringSystem.DAL.Repositories;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL;

public static class DependencyInjection
{
    public static IServiceCollection AddDAL(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<MonitoringDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IMetricPointRepository, MetricPointRepository>();
        services.AddScoped<IAnomalyRepository, AnomalyRepository>();
        services.AddScoped<IApiKeysRepository, ApiKeysesRepository>();

        return services;
    }

    public static async Task ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();

        try
        {
            await using var context = scope.ServiceProvider.GetRequiredService<MonitoringDbContext>();
            await context.Database.MigrateAsync();

            if (!await context.ApiKeys.AnyAsync())
                await AddTestsApiKeys(context);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<MonitoringDbContext>>();
            logger.LogError(ex, $"Помилка при ініціалізації або міграції бази даних: {ex.Message}");
        }
    }

    private static async Task AddTestsApiKeys(MonitoringDbContext context)
    {
        context.ApiKeys.AddRange(
            new ApiKeyEntity
            {
                Key = "mk_dev_order_service_key_001", ServiceName = "OrderService", Owner = "dev-team", IsActive = true
            },
            new ApiKeyEntity
            {
                Key = "mk_dev_payment_service_key_002", ServiceName = "PaymentService", Owner = "dev-team", IsActive = true
            },
            new ApiKeyEntity
            {
                Key = "mk_dev_user_service_key_003", ServiceName = "UserService", Owner = "dev-team", IsActive = true
            });
        await context.SaveChangesAsync();
    }
}