using System.Linq;
using GeoStream.Domain.Enums;
using GeoStream.Domain.Events;
using GeoStream.Domain.ValueObjects;
using Stateless;

namespace GeoStream.Domain.Aggregates;

public enum IncidentState
{
    Detected,
    Validated,
    Mitigating,
    Monitoring,
    Resolved,
}

public enum IncidentTrigger
{
    Validate,
    BeginMitigation,
    BeginMonitoring,
    Resolve,
}

public sealed class Incident
{
    private readonly List<IDomainEvent> _pendingEvents = [];
    private readonly StateMachine<IncidentState, IncidentTrigger> _stateMachine;
    private string? _sensorStationId;
    private int _version;

    private Incident(Guid id)
    {
        Id = id;
        State = IncidentState.Detected;
        Location = new GeoPoint(0, 0);
        _stateMachine = ConfigureStateMachine();
    }

    public Guid Id { get; private set; }

    public IncidentType Type { get; private set; }

    public GeoPoint Location { get; private set; }

    public IncidentSeverity Severity { get; private set; }

    public IncidentState State { get; private set; }

    public string? AssignedResponderId { get; private set; }

    public string SensorStationId => _sensorStationId ?? string.Empty;

    public DateTimeOffset RaisedAt { get; private set; }

    public int Version => _version;

    public int OriginalVersion { get; private set; }

    public IReadOnlyCollection<IDomainEvent> PendingEvents => _pendingEvents.AsReadOnly();

    public static Incident Raise(
        IncidentType type,
        GeoPoint location,
        IncidentSeverity severity,
        string sensorStationId
    )
    {
        if (string.IsNullOrWhiteSpace(sensorStationId))
        {
            throw new ArgumentException("Sensor station id is required.", nameof(sensorStationId));
        }

        var incident = new Incident(Guid.NewGuid());
        var raisedAt = DateTimeOffset.UtcNow;
        incident.Append(
            new IncidentRaised(
                incident.Id,
                type,
                sensorStationId,
                location.Latitude,
                location.Longitude,
                severity,
                raisedAt,
                raisedAt
            )
        );
        return incident;
    }

    public static Incident? FromHistory(IEnumerable<IDomainEvent> events)
    {
        if (events is null)
        {
            throw new ArgumentNullException(nameof(events));
        }

        var list = events.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var incidentId =
            list.OfType<IncidentRaised>().FirstOrDefault()?.IncidentId
            ?? throw new InvalidOperationException(
                "Incident history is missing the initial IncidentRaised event."
            );

        var incident = new Incident(incidentId);
        foreach (var domainEvent in list)
        {
            incident.Apply(domainEvent, isHistorical: true);
        }

        incident.OriginalVersion = list.Count;
        incident._version = incident.OriginalVersion;

        return incident;
    }

    public void Validate()
    {
        MoveTo(IncidentTrigger.Validate);
    }

    public void BeginMitigation(string responderId)
    {
        AssignResponder(responderId);
        MoveTo(IncidentTrigger.BeginMitigation);
    }

    public void BeginMonitoring()
    {
        MoveTo(IncidentTrigger.BeginMonitoring);
    }

    public void Resolve()
    {
        MoveTo(IncidentTrigger.Resolve);
    }

    public void UpdateSeverity(IncidentSeverity severity)
    {
        if (!Enum.IsDefined(typeof(IncidentSeverity), severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity));
        }

        if (severity == Severity)
        {
            return;
        }

        Append(new IncidentSeverityChanged(Id, severity, DateTimeOffset.UtcNow));
    }

    public void AssignResponder(string responderId)
    {
        if (string.IsNullOrWhiteSpace(responderId))
        {
            throw new ArgumentException("Responder id is required.", nameof(responderId));
        }

        // Auto-validate if in Detected state
        if (State == IncidentState.Detected)
        {
            Validate();
        }

        if (AssignedResponderId == responderId)
        {
            return;
        }

        Append(new ResponderAssigned(Id, responderId, DateTimeOffset.UtcNow));
    }

    public void StartMitigation()
    {
        if (State == IncidentState.Detected)
        {
            Validate();
        }

        if (State == IncidentState.Validated)
        {
            MoveTo(IncidentTrigger.BeginMitigation);
        }
    }

    public void CompleteMitigation()
    {
        if (State == IncidentState.Mitigating)
        {
            MoveTo(IncidentTrigger.BeginMonitoring);
        }
    }

    public void LoadFromHistory(string eventType, string eventData)
    {
        // Simplified event replay for simulator
        // In production, deserialize properly based on eventType
        if (eventType.Contains("IncidentRaised"))
        {
            var evt = System.Text.Json.JsonSerializer.Deserialize<IncidentRaised>(eventData);
            if (evt != null)
                Apply(evt, isHistorical: true);
        }
        else if (eventType.Contains("ResponderAssigned"))
        {
            var evt = System.Text.Json.JsonSerializer.Deserialize<ResponderAssigned>(eventData);
            if (evt != null)
                Apply(evt, isHistorical: true);
        }
        else if (eventType.Contains("IncidentStateAdvanced"))
        {
            var evt = System.Text.Json.JsonSerializer.Deserialize<IncidentStateAdvanced>(eventData);
            if (evt != null)
                Apply(evt, isHistorical: true);
        }
        else if (eventType.Contains("IncidentSeverityChanged"))
        {
            var evt = System.Text.Json.JsonSerializer.Deserialize<IncidentSeverityChanged>(
                eventData
            );
            if (evt != null)
                Apply(evt, isHistorical: true);
        }
    }

    public IReadOnlyCollection<IDomainEvent> UncommittedEvents => _pendingEvents.AsReadOnly();

    public void ClearUncommittedEvents()
    {
        _pendingEvents.Clear();
    }

    public IReadOnlyCollection<IDomainEvent> DrainEvents()
    {
        var items = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        return items;
    }

    public void MarkEventsAsCommitted()
    {
        OriginalVersion = _version;
    }

    private void MoveTo(IncidentTrigger trigger)
    {
        var fromState = State;
        _stateMachine.Fire(trigger);
        if (State != fromState)
        {
            Append(
                new IncidentStateAdvanced(
                    Id,
                    fromState.ToString(),
                    State.ToString(),
                    DateTimeOffset.UtcNow
                )
            );
        }
    }

    private void Append(IDomainEvent domainEvent)
    {
        Apply(domainEvent);
        _pendingEvents.Add(domainEvent);
        _version++;
    }

    private void Apply(IDomainEvent domainEvent, bool isHistorical = false)
    {
        switch (domainEvent)
        {
            case IncidentRaised raised:
                Id = raised.IncidentId;
                Type = raised.Type;
                _sensorStationId = raised.SensorStationId;
                Location = new GeoPoint(raised.Latitude, raised.Longitude);
                Severity = raised.Severity;
                State = IncidentState.Detected;
                RaisedAt = raised.RaisedAt;
                break;
            case IncidentStateAdvanced advanced:
                State = Enum.Parse<IncidentState>(advanced.ToState);
                break;
            case ResponderAssigned assigned:
                AssignedResponderId = assigned.ResponderId;
                break;
            case IncidentSeverityChanged severityChanged:
                Severity = severityChanged.Severity;
                break;
        }

        if (isHistorical)
        {
            _version++;
        }
    }

    private StateMachine<IncidentState, IncidentTrigger> ConfigureStateMachine()
    {
        var machine = new StateMachine<IncidentState, IncidentTrigger>(() => State, s => State = s);

        machine
            .Configure(IncidentState.Detected)
            .Permit(IncidentTrigger.Validate, IncidentState.Validated);

        machine
            .Configure(IncidentState.Validated)
            .Permit(IncidentTrigger.BeginMitigation, IncidentState.Mitigating);

        machine
            .Configure(IncidentState.Mitigating)
            .Permit(IncidentTrigger.BeginMonitoring, IncidentState.Monitoring);

        machine
            .Configure(IncidentState.Monitoring)
            .Permit(IncidentTrigger.Resolve, IncidentState.Resolved);

        return machine;
    }
}
