using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MonitoringSystem.Domain.Entities;

namespace MonitoringSystem.DAL.Data;

public class MonitoringDbContext(DbContextOptions<MonitoringDbContext> options) : DbContext(options)
{
    public DbSet<MetricPointEntity> MetricPoints => Set<MetricPointEntity>();
    public DbSet<AnomalyEntity> Anomalies => Set<AnomalyEntity>();
    public DbSet<ApiKeyEntity> ApiKeys => Set<ApiKeyEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetricPointEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ServiceName, x.InstanceId, x.MetricName, x.Timestamp });
            
            var tagsComparer = new ValueComparer<Dictionary<string, string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            );
            
            e.Property(x => x.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new()
                )
                .Metadata.SetValueComparer(tagsComparer);
        });

        modelBuilder.Entity<AnomalyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ServiceName, x.InstanceId, x.DetectedAt });
        });

        modelBuilder.Entity<ApiKeyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
        });
        
        base.OnModelCreating(modelBuilder);
    }
}
