using System.Linq;
using GeoStream.Application.Abstractions;
using GeoStream.Domain.Events;

namespace GeoStream.Infrastructure.Messaging;

public sealed class CompositeDomainEventPublisher(
    ProjectionDomainEventPublisher projectionPublisher,
    MassTransitDomainEventPublisher busPublisher
) : IDomainEventPublisher
{
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

        await projectionPublisher.PublishAsync(events, cancellationToken).ConfigureAwait(false);
        await busPublisher.PublishAsync(events, cancellationToken).ConfigureAwait(false);
    }
}
