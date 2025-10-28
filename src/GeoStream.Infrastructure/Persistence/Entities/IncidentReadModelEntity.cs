using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace GeoStream.Infrastructure.Persistence.Entities;

[Index(nameof(State), nameof(RaisedAt), IsDescending = new[] { false, true })]
[Index(nameof(RaisedAt), IsDescending = new[] { true })]
public sealed class IncidentReadModelEntity
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public Point Location { get; set; } = default!;

    public string SensorStationId { get; set; } = string.Empty;

    public string? AssignedResponderId { get; set; }

    public DateTimeOffset RaisedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
