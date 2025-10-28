using GeoStream.Application.Abstractions;
using GeoStream.Domain.Aggregates;
using MediatR;

namespace GeoStream.Application.Commands.Incidents;

public enum IncidentAdvanceAction
{
    AssignResponder,
    Validate,
    BeginMitigation,
    BeginMonitoring,
    Resolve,
}

public sealed record AdvanceIncidentCommand(
    Guid IncidentId,
    IncidentAdvanceAction Action,
    string? ResponderId = null
) : IRequest;

public sealed class AdvanceIncidentCommandHandler(
    IIncidentRepository repository,
    IDomainEventPublisher eventPublisher
) : IRequestHandler<AdvanceIncidentCommand>
{
    public async Task<Unit> Handle(
        AdvanceIncidentCommand request,
        CancellationToken cancellationToken
    )
    {
        var incident =
            await repository.GetAsync(request.IncidentId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Incident {request.IncidentId} was not found.");

        switch (request.Action)
        {
            case IncidentAdvanceAction.AssignResponder:
                if (string.IsNullOrWhiteSpace(request.ResponderId))
                {
                    throw new ArgumentException(
                        "Responder id is required when assigning responder.",
                        nameof(request)
                    );
                }
                incident.AssignResponder(request.ResponderId);
                break;
            case IncidentAdvanceAction.Validate:
                incident.Validate();
                break;
            case IncidentAdvanceAction.BeginMitigation:
                var responderId = request.ResponderId ?? incident.AssignedResponderId;
                if (string.IsNullOrWhiteSpace(responderId))
                {
                    throw new ArgumentException(
                        "Responder id is required when beginning mitigation. Either assign a responder first or provide one in the request.",
                        nameof(request)
                    );
                }

                incident.BeginMitigation(responderId);
                break;
            case IncidentAdvanceAction.BeginMonitoring:
                incident.BeginMonitoring();
                break;
            case IncidentAdvanceAction.Resolve:
                incident.Resolve();
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(request.Action),
                    request.Action,
                    "Unsupported incident action."
                );
        }

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
