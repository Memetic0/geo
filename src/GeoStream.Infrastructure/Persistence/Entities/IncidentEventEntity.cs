namespace GeoStream.Infrastructure.Persistence.Entities;

public sealed class IncidentEventEntity
{
    public Guid Id { get; set; }

    public Guid AggregateId { get; set; }

    public string AggregateType { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string Data { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    public int Version { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
