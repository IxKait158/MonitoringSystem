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
    public DbSet<ServiceEntity> Services => Set<ServiceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetricPointEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ServiceId, x.MetricName, x.Timestamp });

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

            e.HasOne(x => x.Service)
                .WithMany(x => x.MetricPoints)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnomalyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ServiceId, x.DetectedAt });

            e.HasOne(x => x.Service)
                .WithMany(x => x.Anomalies)
                .HasForeignKey(x => x.ServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiKeyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
        });

        modelBuilder.Entity<ServiceEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ApiKeyId, x.Name }).IsUnique();

            e.HasOne(x => x.ApiKey)
                .WithMany(x => x.Services)
                .HasForeignKey(x => x.ApiKeyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
