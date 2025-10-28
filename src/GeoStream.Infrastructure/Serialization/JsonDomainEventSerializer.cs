using System.Text.Json;
using System.Text.Json.Serialization;
using GeoStream.Domain.Events;

namespace GeoStream.Infrastructure.Serialization;

public sealed class JsonDomainEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string Serialize(IDomainEvent domainEvent)
    {
        var type = domainEvent.GetType();
        return JsonSerializer.Serialize(domainEvent, type, Options);
    }

    public IDomainEvent Deserialize(string eventType, string payload)
    {
        var resolvedType =
            Type.GetType(eventType, throwOnError: true)
            ?? throw new InvalidOperationException(
                $"Unable to resolve domain event type '{eventType}'."
            );

        var domainEvent =
            JsonSerializer.Deserialize(payload, resolvedType, Options)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize event payload for type '{eventType}'."
            );

        if (domainEvent is not IDomainEvent typedEvent)
        {
            throw new InvalidOperationException(
                $"Deserialized event does not implement {nameof(IDomainEvent)}."
            );
        }

        return typedEvent;
    }
}
