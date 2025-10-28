using GeoStream.Domain.Aggregates;
using GeoStream.Domain.Enums;
using GeoStream.Domain.ValueObjects;

namespace GeoStream.Application.Queries.Incidents;

public sealed record IncidentSummaryDto(
    Guid Id,
    IncidentType Type,
    IncidentState State,
    IncidentSeverity Severity,
    double Latitude,
    double Longitude,
    string SensorStationId,
    string? AssignedResponderId,
    DateTimeOffset RaisedAt
);
