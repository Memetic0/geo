using GeoStream.Application.Abstractions;
using MediatR;

namespace GeoStream.Application.Queries.Incidents;

public sealed record GetIncidentHistoryQuery(Guid IncidentId) : IRequest<IncidentHistoryDto?>;

public sealed class GetIncidentHistoryQueryHandler(IIncidentEventStore eventStore)
    : IRequestHandler<GetIncidentHistoryQuery, IncidentHistoryDto?>
{
    public async Task<IncidentHistoryDto?> Handle(GetIncidentHistoryQuery request, CancellationToken cancellationToken)
    {
        var events = await eventStore.GetEventsAsync(request.IncidentId, cancellationToken).ConfigureAwait(false);

        if (events.Count == 0)
        {
            return null;
        }

        return new IncidentHistoryDto(request.IncidentId, events);
    }
}

public sealed record IncidentHistoryDto(Guid IncidentId, List<IncidentEventDto> Events);

public sealed record IncidentEventDto(
    string EventType,
    string Data,
    DateTimeOffset OccurredAt,
    int Version
);
