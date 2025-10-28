using GeoStream.Domain.Enums;
using GeoStream.Domain.ValueObjects;

namespace GeoStream.Domain.Events;

public sealed record IncidentRaised(
	Guid IncidentId,
	IncidentType Type,
	string SensorStationId,
	double Latitude,
	double Longitude,
	IncidentSeverity Severity,
	DateTimeOffset RaisedAt,
	DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record IncidentStateAdvanced(Guid IncidentId, string FromState, string ToState, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ResponderAssigned(Guid IncidentId, string ResponderId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record IncidentSeverityChanged(Guid IncidentId, IncidentSeverity Severity, DateTimeOffset OccurredAt) : IDomainEvent;
