using GeoStream.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeoStream.Infrastructure.Persistence;

public sealed class ReadModelDbContext(DbContextOptions<ReadModelDbContext> options)
    : DbContext(options)
{
    public DbSet<IncidentReadModelEntity> Incidents => Set<IncidentReadModelEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var incident = modelBuilder.Entity<IncidentReadModelEntity>();
        incident.ToTable("incident_summaries");
        incident.HasKey(x => x.Id);
        incident.Property(x => x.Type).HasMaxLength(64);
        incident.Property(x => x.State).HasMaxLength(32);
        incident.Property(x => x.Severity).HasMaxLength(32);
        incident.Property(x => x.SensorStationId).HasMaxLength(64);
        incident.Property(x => x.AssignedResponderId).HasMaxLength(128);
        incident.Property(x => x.Location).HasColumnType("geometry");
        incident.HasIndex(x => x.State);
        incident.HasIndex(x => x.Severity);
        incident.HasIndex(x => x.Type);
    }
}
