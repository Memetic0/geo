using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using GeoStream.Application.Abstractions;
using GeoStream.Application.Queries.Incidents;
using GeoStream.Domain.Events;
using GeoStream.Infrastructure.Caching;
using GeoStream.Infrastructure.Persistence;
using GeoStream.Infrastructure.Persistence.Entities;
using GeoStream.Infrastructure.RealTime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace GeoStream.Infrastructure.Messaging;

public sealed class ProjectionDomainEventPublisher(
    ReadModelDbContext readModelContext,
    IncidentCache cache,
    ElasticsearchClient elasticClient,
    IHubContext<IncidentHub> hubContext,
    ILogger<ProjectionDomainEventPublisher> logger
) : IDomainEventPublisher
{
    private const string IndexName = "geostream-incidents";

    public async Task PublishAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default
    )
    {
        var events = domainEvents.ToList();
        if (events.Count == 0)
        {
            return;
        }

        var affectedIncidentIds = new HashSet<Guid>();

        foreach (var domainEvent in events)
        {
            switch (domainEvent)
            {
                case IncidentRaised raised:
                    await ApplyAsync(raised, cancellationToken).ConfigureAwait(false);
                    affectedIncidentIds.Add(raised.IncidentId);
                    break;
                case IncidentStateAdvanced advanced:
                    await ApplyAsync(advanced, cancellationToken).ConfigureAwait(false);
                    affectedIncidentIds.Add(advanced.IncidentId);
                    break;
                case ResponderAssigned assigned:
                    await ApplyAsync(assigned, cancellationToken).ConfigureAwait(false);
                    affectedIncidentIds.Add(assigned.IncidentId);
                    break;
                case IncidentSeverityChanged severityChanged:
                    await ApplyAsync(severityChanged, cancellationToken).ConfigureAwait(false);
                    affectedIncidentIds.Add(severityChanged.IncidentId);
                    break;
            }
        }

        await readModelContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var incidentId in affectedIncidentIds)
        {
            var summary = await LoadSummaryAsync(incidentId, cancellationToken)
                .ConfigureAwait(false);
            if (summary is null)
            {
                continue;
            }

            await cache.SetAsync(summary, cancellationToken).ConfigureAwait(false);
            await IndexAsync(summary, cancellationToken).ConfigureAwait(false);
            await hubContext
                .Clients.All.SendAsync("incidentUpdated", summary, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task ApplyAsync(IncidentRaised raised, CancellationToken cancellationToken)
    {
        var entity = await readModelContext
            .Incidents.FirstOrDefaultAsync(x => x.Id == raised.IncidentId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            entity = new IncidentReadModelEntity { Id = raised.IncidentId };
            readModelContext.Incidents.Add(entity);
        }

        entity.Type = raised.Type.ToString();
        entity.State = GeoStream.Domain.Aggregates.IncidentState.Detected.ToString();
        entity.Severity = raised.Severity.ToString();
        entity.Latitude = raised.Latitude;
        entity.Longitude = raised.Longitude;
        entity.Location = new Point(raised.Longitude, raised.Latitude) { SRID = 4326 };
        entity.SensorStationId = raised.SensorStationId;
        entity.RaisedAt = raised.RaisedAt;
        entity.UpdatedAt = raised.OccurredAt;
    }

    private async Task ApplyAsync(
        IncidentStateAdvanced advanced,
        CancellationToken cancellationToken
    )
    {
        var entity = await LoadRequiredAsync(advanced.IncidentId, cancellationToken)
            .ConfigureAwait(false);
        entity.State = advanced.ToState;
        entity.UpdatedAt = advanced.OccurredAt;
    }

    private async Task ApplyAsync(ResponderAssigned assigned, CancellationToken cancellationToken)
    {
        var entity = await LoadRequiredAsync(assigned.IncidentId, cancellationToken)
            .ConfigureAwait(false);
        entity.AssignedResponderId = assigned.ResponderId;
        entity.UpdatedAt = assigned.OccurredAt;
    }

    private async Task ApplyAsync(
        IncidentSeverityChanged severityChanged,
        CancellationToken cancellationToken
    )
    {
        var entity = await LoadRequiredAsync(severityChanged.IncidentId, cancellationToken)
            .ConfigureAwait(false);
        entity.Severity = severityChanged.Severity.ToString();
        entity.UpdatedAt = severityChanged.OccurredAt;
    }

    private async Task<IncidentReadModelEntity> LoadRequiredAsync(
        Guid incidentId,
        CancellationToken cancellationToken
    )
    {
        var entity = await readModelContext
            .Incidents.FirstOrDefaultAsync(x => x.Id == incidentId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            throw new InvalidOperationException($"Incident {incidentId} not found in read model.");
        }

        return entity;
    }

    private async Task<IncidentSummaryDto?> LoadSummaryAsync(
        Guid incidentId,
        CancellationToken cancellationToken
    )
    {
        var entity = await readModelContext
            .Incidents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == incidentId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return null;
        }

        var type = string.IsNullOrWhiteSpace(entity.Type)
            ? GeoStream.Domain.Enums.IncidentType.StreetFlooding
            : Enum.Parse<GeoStream.Domain.Enums.IncidentType>(entity.Type);
        var state = Enum.Parse<GeoStream.Domain.Aggregates.IncidentState>(entity.State);
        var severity = Enum.Parse<GeoStream.Domain.ValueObjects.IncidentSeverity>(entity.Severity);

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

    private async Task IndexAsync(IncidentSummaryDto summary, CancellationToken cancellationToken)
    {
        try
        {
            var document = new Search.IncidentSearchDocument
            {
                Id = summary.Id,
                Type = summary.Type.ToString(),
                Severity = summary.Severity.ToString(),
                State = summary.State.ToString(),
                Latitude = summary.Latitude,
                Longitude = summary.Longitude,
                RaisedAt = summary.RaisedAt.UtcDateTime,
                SensorStationId = summary.SensorStationId,
                AssignedResponderId = summary.AssignedResponderId,
            };

            var response = await elasticClient
                .IndexAsync(
                    document,
                    idx => idx.Index(IndexName).Id(summary.Id.ToString()),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (!response.IsSuccess())
            {
                var serverError =
                    response.ElasticsearchServerError != null
                        ? response.ElasticsearchServerError.Error.Reason
                        : "Unknown";
                logger.LogWarning(
                    "Failed to index incident {IncidentId}: {Error}",
                    summary.Id,
                    serverError
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Elasticsearch indexing failed for incident {IncidentId}",
                summary.Id
            );
        }
    }
}
