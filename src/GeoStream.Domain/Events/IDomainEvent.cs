namespace GeoStream.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
