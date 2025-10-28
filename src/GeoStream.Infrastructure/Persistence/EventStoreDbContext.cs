using GeoStream.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeoStream.Infrastructure.Persistence;

public sealed class EventStoreDbContext(DbContextOptions<EventStoreDbContext> options)
    : DbContext(options)
{
    public DbSet<IncidentEventEntity> Events => Set<IncidentEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var incidentEvents = modelBuilder.Entity<IncidentEventEntity>();
        incidentEvents.ToTable("IncidentEvents");
        incidentEvents.HasKey(e => e.Id);
        incidentEvents.Property(e => e.AggregateType).HasMaxLength(128);
        incidentEvents.Property(e => e.EventType).HasMaxLength(256);
        incidentEvents.Property(e => e.Data).HasColumnType("nvarchar(max)");
        incidentEvents.HasIndex(e => new { e.AggregateId, e.Version }).IsUnique();
    }
}
