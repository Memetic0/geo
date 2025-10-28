using GeoStream.Application.Abstractions;
using GeoStream.Domain.Aggregates;
using GeoStream.Domain.Enums;
using GeoStream.Domain.ValueObjects;
using MediatR;

namespace GeoStream.Application.Commands.Incidents;

public sealed record RaiseIncidentCommand(IncidentType Type, double Latitude, double Longitude, IncidentSeverity Severity, string SensorStationId) : IRequest<Guid>;

public sealed class RaiseIncidentCommandHandler(IIncidentRepository repository, IDomainEventPublisher eventPublisher)
    : IRequestHandler<RaiseIncidentCommand, Guid>
{
    public async Task<Guid> Handle(RaiseIncidentCommand request, CancellationToken cancellationToken)
    {
        var incident = Incident.Raise(request.Type, new GeoPoint(request.Latitude, request.Longitude), request.Severity, request.SensorStationId);

        await repository.SaveAsync(incident, cancellationToken).ConfigureAwait(false);

        var eventsToPublish = incident.DrainEvents();
        if (eventsToPublish.Count > 0)
        {
            await eventPublisher.PublishAsync(eventsToPublish, cancellationToken).ConfigureAwait(false);
        }

        return incident.Id;
    }
}
