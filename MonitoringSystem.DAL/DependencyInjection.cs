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
        services.AddScoped<IServicesRepository, ServicesRepository>();

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
                await SeedAsync(context);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<MonitoringDbContext>>();
            logger.LogError(ex, "Помилка при ініціалізації або міграції бази даних: {Message}", ex.Message);
        }
    }

    private static async Task SeedAsync(MonitoringDbContext context)
    {
        // Демо-користувач: один ключ + три зареєстровані сервіси під ним.
        var devKey = new ApiKeyEntity
        {
            Key = "mk_dev_demo_user_key_0000000000000001",
            Owner = "dev-team",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            Services = new List<ServiceEntity>
            {
                new() { Name = "OrderService" },
                new() { Name = "PaymentService" },
                new() { Name = "UserService" }
            }
        };

        context.ApiKeys.Add(devKey);
        await context.SaveChangesAsync();
    }
}
