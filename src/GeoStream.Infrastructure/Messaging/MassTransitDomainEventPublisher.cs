using GeoStream.Application.Abstractions;
using GeoStream.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GeoStream.Infrastructure.Messaging;

public sealed class MassTransitDomainEventPublisher(
    IPublishEndpoint publishEndpoint,
    ILogger<MassTransitDomainEventPublisher> logger
) : IDomainEventPublisher
{
    public async Task PublishAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var domainEvent in domainEvents)
        {
            await publishEndpoint.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Published domain event {EventType} at {OccurredAt}",
                domainEvent.GetType().Name,
                domainEvent.OccurredAt
            );
        }
    }
}
