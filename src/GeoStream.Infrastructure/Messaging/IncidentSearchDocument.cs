namespace GeoStream.Infrastructure.Messaging;

public sealed class IncidentSearchDocument
{
    public Guid Id { get; set; }

    public string State { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public DateTimeOffset RaisedAt { get; set; }

    public string SensorStationId { get; set; } = string.Empty;

    public string? AssignedResponderId { get; set; }
}
