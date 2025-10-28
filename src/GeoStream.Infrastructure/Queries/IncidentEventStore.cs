using GeoStream.Application.Abstractions;
using GeoStream.Application.Queries.Incidents;
using GeoStream.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GeoStream.Infrastructure.Queries;

public sealed class IncidentEventStore(EventStoreDbContext eventStoreContext) : IIncidentEventStore
{
    public async Task<List<IncidentEventDto>> GetEventsAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default
    )
    {
        return await eventStoreContext
            .Events.Where(e => e.AggregateId == incidentId)
            .OrderBy(e => e.Version)
            .Select(e => new IncidentEventDto(e.EventType, e.Data, e.OccurredAt, e.Version))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
