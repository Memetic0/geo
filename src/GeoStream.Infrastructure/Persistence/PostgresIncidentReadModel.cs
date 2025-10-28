using GeoStream.Application.Abstractions;
using GeoStream.Application.Queries.Incidents;
using GeoStream.Domain.Aggregates;
using GeoStream.Domain.Enums;
using GeoStream.Domain.ValueObjects;
using GeoStream.Infrastructure.Caching;
using GeoStream.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeoStream.Infrastructure.Persistence;

public sealed class PostgresIncidentReadModel(
    ReadModelDbContext context,
    IncidentCache cache,
    ILogger<PostgresIncidentReadModel> logger
) : IIncidentReadModel
{
    public async Task<IncidentSummaryDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var cached = await cache.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var entity = await context
            .Incidents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        var summary = Map(entity);
        try
        {
            await cache.SetAsync(summary, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache incident {IncidentId}", id);
        }

        return summary;
    }

    public async Task<IReadOnlyCollection<IncidentSummaryDto>> ListActiveAsync(
        CancellationToken cancellationToken = default
    )
    {
        var entities = await context
            .Incidents
            .AsNoTracking()
            .Where(x => x.State != "Resolved")
            .OrderByDescending(x => x.RaisedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(Map).ToArray();
    }

    private static IncidentSummaryDto Map(IncidentReadModelEntity entity)
    {
        var type = string.IsNullOrWhiteSpace(entity.Type)
            ? IncidentType.StreetFlooding
            : Enum.Parse<IncidentType>(entity.Type);
        var state = Enum.Parse<IncidentState>(entity.State);
        var severity = Enum.Parse<IncidentSeverity>(entity.Severity);

        return new IncidentSummaryDto(
            entity.Id,
            type,
            state,
            severity,
            entity.Latitude,
            entity.Longitude,
            entity.SensorStationId,
            entity.AssignedResponderId,
            entity.RaisedAt
        );
    }
}
