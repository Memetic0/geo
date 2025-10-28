using GeoStream.Application.Queries.Incidents;

namespace GeoStream.Application.Abstractions;

public interface IIncidentEventStore
{
    Task<List<IncidentEventDto>> GetEventsAsync(Guid incidentId, CancellationToken cancellationToken = default);
}
