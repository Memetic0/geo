using System.Linq;
using FluentAssertions;
using GeoStream.Domain.Aggregates;
using GeoStream.Domain.Enums;
using GeoStream.Domain.Events;
using GeoStream.Domain.ValueObjects;

namespace GeoStream.Domain.Tests;

public class IncidentTests
{
    [Fact]
    public void Raise_ShouldCreateIncidentInDetectedState()
    {
        var incident = Incident.Raise(IncidentType.StreetFlooding, new GeoPoint(51.5, -0.1), IncidentSeverity.Moderate, "sensor-123");

        incident.State.Should().Be(IncidentState.Detected);
        incident.PendingEvents.Should().HaveCount(1);
        incident.SensorStationId.Should().Be("sensor-123");
    }

    [Fact]
    public void Validate_ShouldTransitionToValidated()
    {
        var incident = Incident.Raise(IncidentType.StreetFlooding, new GeoPoint(51.5, -0.1), IncidentSeverity.Moderate, "sensor-123");
        incident.DrainEvents();

        incident.Validate();

        incident.State.Should().Be(IncidentState.Validated);
        incident.PendingEvents
            .OfType<IncidentStateAdvanced>()
            .Single()
            .ToState.Should().Be(IncidentState.Validated.ToString());
    }

    [Fact]
    public void BeginMitigation_ShouldAssignResponderAndTransition()
    {
        var incident = Incident.Raise(IncidentType.StreetFlooding, new GeoPoint(51.5, -0.1), IncidentSeverity.Moderate, "sensor-123");
        incident.DrainEvents();
        incident.Validate();
        incident.DrainEvents();

        incident.BeginMitigation("responder-456");

        incident.State.Should().Be(IncidentState.Mitigating);
        incident.AssignedResponderId.Should().Be("responder-456");
    }

    [Fact]
    public void Resolve_ShouldRequireMonitoring()
    {
        var incident = Incident.Raise(IncidentType.StreetFlooding, new GeoPoint(51.5, -0.1), IncidentSeverity.Moderate, "sensor-123");
        incident.DrainEvents();
        incident.Validate();
        incident.BeginMitigation("responder-456");
        incident.BeginMonitoring();

        incident.Resolve();

        incident.State.Should().Be(IncidentState.Resolved);
    }

    [Fact]
    public void AssignResponder_ShouldMoveIncidentToAcknowledgedState()
    {
        var incident = Incident.Raise(IncidentType.StreetFlooding, new GeoPoint(51.5, -0.1), IncidentSeverity.Moderate, "sensor-123");
        incident.DrainEvents();

        incident.AssignResponder("responder-456");

        incident.State.Should().Be(IncidentState.Acknowledged);
        incident.AssignedResponderId.Should().Be("responder-456");
    }

    [Fact]
    public void FromHistory_ShouldRehydrateIncident()
    {
        var raisedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var incidentId = Guid.NewGuid();
        var history = new IDomainEvent[]
        {
            new IncidentRaised(incidentId, IncidentType.StreetFlooding, "sensor-001", 51.5, -0.1, IncidentSeverity.High, raisedAt, raisedAt),
            new IncidentStateAdvanced(incidentId, IncidentState.Detected.ToString(), IncidentState.Validated.ToString(), raisedAt.AddMinutes(1)),
            new ResponderAssigned(incidentId, "responder-123", raisedAt.AddMinutes(2))
        };

        var incident = Incident.FromHistory(history)
            ?? throw new InvalidOperationException("Incident should be reconstructed");

        incident.State.Should().Be(IncidentState.Validated);
        incident.AssignedResponderId.Should().Be("responder-123");
        incident.SensorStationId.Should().Be("sensor-001");
        incident.OriginalVersion.Should().Be(history.Length);
    }
}
