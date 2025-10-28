using GeoStream.Application.Abstractions;
using GeoStream.Domain.ValueObjects;
using MediatR;

namespace GeoStream.Application.Commands.Incidents;

public sealed record UpdateIncidentSeverityCommand(Guid IncidentId, IncidentSeverity Severity)
    : IRequest;

public sealed class UpdateIncidentSeverityCommandHandler(
    IIncidentRepository repository,
    IDomainEventPublisher eventPublisher
) : IRequestHandler<UpdateIncidentSeverityCommand>
{
    public async Task<Unit> Handle(
        UpdateIncidentSeverityCommand request,
        CancellationToken cancellationToken
    )
    {
        var incident =
            await repository.GetAsync(request.IncidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Incident {request.IncidentId} was not found.");

        incident.UpdateSeverity(request.Severity);

        await repository.SaveAsync(incident, cancellationToken).ConfigureAwait(false);

        var eventsToPublish = incident.DrainEvents();
        if (eventsToPublish.Count > 0)
        {
            await eventPublisher
                .PublishAsync(eventsToPublish, cancellationToken)
                .ConfigureAwait(false);
        }

        return Unit.Value;
    }
}
