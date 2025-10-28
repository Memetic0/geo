using GeoStream.Application.Abstractions;
using GeoStream.Domain.Aggregates;
using GeoStream.Infrastructure.Persistence.Entities;
using GeoStream.Infrastructure.Serialization;
using Microsoft.EntityFrameworkCore;

namespace GeoStream.Infrastructure.Persistence;

public sealed class EventStoreIncidentRepository(
    EventStoreDbContext eventContext,
    JsonDomainEventSerializer serializer
) : IIncidentRepository
{
    public async Task<Incident?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var events = await eventContext
            .Events.AsNoTracking()
            .Where(e => e.AggregateId == id)
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (events.Count == 0)
        {
            return null;
        }

        var domainEvents = events.Select(e => serializer.Deserialize(e.EventType, e.Data)).ToList();

        return Incident.FromHistory(domainEvents);
    }

    public async Task SaveAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        var pendingEvents = incident.PendingEvents.ToList();
        if (pendingEvents.Count == 0)
        {
            return;
        }

        var expectedVersion = incident.OriginalVersion;
        var currentVersion =
            await eventContext
                .Events.AsNoTracking()
                .Where(e => e.AggregateId == incident.Id)
                .Select(e => (int?)e.Version)
                .OrderByDescending(v => v)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false) ?? 0;

        if (currentVersion != expectedVersion)
        {
            throw new InvalidOperationException(
                $"Concurrency conflict for incident {incident.Id}. Expected version {expectedVersion} but found {currentVersion}."
            );
        }

        var version = expectedVersion;
        foreach (var domainEvent in pendingEvents)
        {
            version++;
            eventContext.Events.Add(
                new IncidentEventEntity
                {
                    Id = Guid.NewGuid(),
                    AggregateId = incident.Id,
                    AggregateType = nameof(Incident),
                    EventType =
                        domainEvent.GetType().AssemblyQualifiedName
                        ?? domainEvent.GetType().FullName!,
                    Data = serializer.Serialize(domainEvent),
                    OccurredAt = domainEvent.OccurredAt,
                    Version = version,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );
        }

        await eventContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        incident.MarkEventsAsCommitted();
    }
}
