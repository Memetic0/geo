using GeoStream.Domain.Events;

namespace GeoStream.Application.Abstractions;

public interface IDomainEventPublisher
{
    Task PublishAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
