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

        services.AddScoped<IMetricPointsRepository, MetricPointsRepository>();
        services.AddScoped<IAnomaliesRepository, AnomaliesRepository>();
        services.AddScoped<IApiKeysRepository, ApiKeysRepository>();
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
        const string devKeyValue = "mk_dev_demo_user_key_0000000000000001";
        var devServiceNames = new[] { "OrderService", "PaymentService", "UserService" };

        // 1. Демо-ключ — створюємо, якщо ще немає.
        var devKey = await context.ApiKeys.FirstOrDefaultAsync(k => k.Key == devKeyValue);
        if (devKey == null)
        {
            devKey = new ApiKeyEntity
            {
                Key = devKeyValue,
                Owner = "dev-team",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.ApiKeys.Add(devKey);
            await context.SaveChangesAsync();
        }

        // 2. Три демо-сервіси під цим ключем — додаємо лише ті, яких ще нема.
        var existingNames = await context.Services
            .Where(s => s.ApiKeyId == devKey.Id)
            .Select(s => s.Name)
            .ToListAsync();

        var missing = devServiceNames
            .Where(n => !existingNames.Contains(n))
            .Select(n => new ServiceEntity { Name = n, ApiKeyId = devKey.Id })
            .ToList();

        if (missing.Count > 0)
        {
            context.Services.AddRange(missing);
            await context.SaveChangesAsync();
        }
    }
}
