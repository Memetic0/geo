using GeoStream.Application.Commands.Incidents;
using GeoStream.Application.Queries.Incidents;
using GeoStream.Domain.Enums;
using GeoStream.Domain.ValueObjects;
using MediatR;

namespace GeoStream.Api.Services;

/// <summary>
/// Background service that simulates random incidents and automatically progresses them through
/// the state machine at random intervals. Allows user interventions through the API.
/// </summary>
public sealed class IncidentSimulatorService : BackgroundService, IIncidentSimulatorControl
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IncidentSimulatorService> _logger;
    private readonly Random _random = new();
    private readonly List<Guid> _activeIncidentIds = [];
    private readonly Lock _lock = new();

    // Simulation parameters
    private const int MaxIncidents = 100; // Hard cap on total incidents
    private const int TargetMinIncidents = 50; // Lower bound for steady state
    private const int TargetMaxIncidents = 80; // Upper bound for steady state

    private static readonly string[] SensorStations =
    {
        "SENS-001",
        "SENS-002",
        "SENS-003",
        "SENS-004",
        "SENS-005",
        "SENS-006",
    };
    private static readonly IncidentType[] IncidentTypes =
    {
        IncidentType.TrafficCongestion,
        IncidentType.RoadAccident,
        IncidentType.RoadClosure,
        IncidentType.VehicleBreakdown,
        IncidentType.Roadwork,
        IncidentType.PublicTransportDelay,
        IncidentType.ParkingViolation,
        IncidentType.SignalMalfunction,
        IncidentType.PedestrianIncident,
        IncidentType.StreetFlooding,
    };
    private static readonly IncidentSeverity[] Severities =
    {
        IncidentSeverity.Low,
        IncidentSeverity.Moderate,
        IncidentSeverity.High,
        IncidentSeverity.Critical,
    };
    private static readonly string[] ResponderIds =
    {
        "RESP-001",
        "RESP-002",
        "RESP-003",
        "RESP-004",
        "RESP-005",
    };

    // London area coordinates (51.28 to 51.69 N, -0.51 to 0.33 E)
    private const double MinLatitude = 51.35;
    private const double MaxLatitude = 51.65;
    private const double MinLongitude = -0.35;
    private const double MaxLongitude = 0.15;

    public IncidentSimulatorService(
        IServiceProvider serviceProvider,
        ILogger<IncidentSimulatorService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🎬 Incident Simulator Service starting...");

        // Wait for infrastructure to be ready
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        _logger.LogInformation("✅ Incident Simulator Service ready - beginning simulation");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int activeCount;
                lock (_lock)
                {
                    activeCount = _activeIncidentIds.Count;
                }

                _logger.LogDebug("📊 Active incidents: {Count}/{Max}", activeCount, MaxIncidents);

                // Determine action probabilities based on current incident count
                var (raiseWeight, progressWeight, idleWeight) = GetActionWeights(activeCount);

                // Decide what action to take (weighted probabilities)
                var action = _random.Next(0, raiseWeight + progressWeight + idleWeight);

                if (action < raiseWeight && activeCount < MaxIncidents) // Raise new incident
                {
                    await RaiseRandomIncident(stoppingToken);
                }
                else if (action < raiseWeight + progressWeight && HasActiveIncidents()) // Progress existing incident
                {
                    await ProgressRandomIncident(stoppingToken);
                }
                // Otherwise: Do nothing (allows time for user intervention)

                // Slower variable delay between actions (10-30 seconds for more realistic pace)
                var delaySeconds = _random.Next(10, 31);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in incident simulator cycle");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("⏹️  Incident Simulator Service stopped");
    }

    /// <summary>
    /// Dynamically adjusts action probabilities based on current incident count to maintain
    /// a steady state between 50-80 incidents while respecting the 100 hard cap.
    /// </summary>
    private (int raiseWeight, int progressWeight, int idleWeight) GetActionWeights(int activeCount)
    {
        return activeCount switch
        {
            // Below target range: Aggressively raise new incidents
            < TargetMinIncidents => (40, 40, 20),

            // Within target range: Balanced operation
            >= TargetMinIncidents and <= TargetMaxIncidents => (25, 50, 25),

            // Above target range: Prioritize progression/resolution
            > TargetMaxIncidents and < MaxIncidents => (10, 70, 20),

            // At cap: Only progress existing incidents
            _ => (0, 80, 20),
        };
    }

    private async Task RaiseRandomIncident(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var latitude = MinLatitude + (_random.NextDouble() * (MaxLatitude - MinLatitude));
        var longitude = MinLongitude + (_random.NextDouble() * (MaxLongitude - MinLongitude));

        // Randomly select incident type
        var incidentType = IncidentTypes[_random.Next(IncidentTypes.Length)];

        // Bias towards moderate severity for realistic distribution
        var severityRoll = _random.Next(100);
        var severity = severityRoll switch
        {
            < 10 => IncidentSeverity.Critical,
            < 30 => IncidentSeverity.High,
            < 70 => IncidentSeverity.Moderate,
            _ => IncidentSeverity.Low,
        };

        var sensorStation = SensorStations[_random.Next(SensorStations.Length)];

        var command = new RaiseIncidentCommand(
            incidentType,
            latitude,
            longitude,
            severity,
            sensorStation
        );
        var incidentId = await mediator.Send(command, cancellationToken);

        lock (_lock)
        {
            _activeIncidentIds.Add(incidentId);
        }

        _logger.LogInformation(
            "🚨 Raised incident {IncidentId} ({Type}) at ({Lat:F4}, {Lon:F4}) - Severity: {Severity}, Sensor: {Sensor}",
            incidentId,
            incidentType,
            latitude,
            longitude,
            severity,
            sensorStation
        );
    }

    private async Task ProgressRandomIncident(CancellationToken cancellationToken)
    {
        Guid incidentId;
        lock (_lock)
        {
            if (_activeIncidentIds.Count == 0)
                return;

            var index = _random.Next(_activeIncidentIds.Count);
            incidentId = _activeIncidentIds[index];
        }

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            // Get current incident summary
            var query = new GetIncidentSummaryQuery(incidentId);
            var summary = await mediator.Send(query, cancellationToken);

            if (summary == null)
            {
                // Incident no longer exists (possibly deleted)
                RemoveIncidentFromTracking(incidentId);
                return;
            }

            // Progress based on current state and responder assignment
            var (action, responderId) = DetermineNextAction(summary);

            if (action == null)
            {
                // Incident is resolved, remove from tracking
                RemoveIncidentFromTracking(incidentId);
                _logger.LogInformation("✅ Incident {IncidentId} completed lifecycle", incidentId);
                return;
            }

            var advanceCommand = new AdvanceIncidentCommand(incidentId, action.Value, responderId);
            await mediator.Send(advanceCommand, cancellationToken);

            _logger.LogInformation(
                "⚡ Advanced incident {IncidentId}: {State} → {Action}",
                incidentId,
                summary.State,
                action.Value
            );
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                "⚠️  Could not progress incident {IncidentId}: {Message}",
                incidentId,
                ex.Message
            );
            RemoveIncidentFromTracking(incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to progress incident {IncidentId}", incidentId);
        }
    }

    private (IncidentAdvanceAction? action, string? responderId) DetermineNextAction(
        IncidentSummaryDto summary
    )
    {
        var currentState = summary.State.ToString().ToLowerInvariant();
        var assignedResponder = summary.AssignedResponderId;

        return currentState switch
        {
            "detected" => string.IsNullOrWhiteSpace(assignedResponder)
                ? (
                    IncidentAdvanceAction.AssignResponder,
                    ResponderIds[_random.Next(ResponderIds.Length)]
                )
                : (IncidentAdvanceAction.Validate, null),
            "acknowledged" => (
                IncidentAdvanceAction.Validate,
                null
            ),
            "validated" => (
                IncidentAdvanceAction.BeginMitigation,
                assignedResponder ?? ResponderIds[_random.Next(ResponderIds.Length)]
            ),
            "mitigating" => (IncidentAdvanceAction.BeginMonitoring, null),
            "monitoring" =>
            // When above target range, always resolve to maintain steady state
            // Otherwise, 70% chance to resolve, 30% stay in monitoring
            ShouldResolveIncident()
                ? (IncidentAdvanceAction.Resolve, null)
                : (null, null),
            "resolved" => (null, null),
            _ => (null, null),
        };
    }

    /// <summary>
    /// Determines if an incident in monitoring state should be resolved.
    /// More aggressive resolution when above target incident count.
    /// </summary>
    private bool ShouldResolveIncident()
    {
        int activeCount;
        lock (_lock)
        {
            activeCount = _activeIncidentIds.Count;
        }

        // Above target: Always resolve
        if (activeCount > TargetMaxIncidents)
            return true;

        // Within target: 70% chance to resolve, 30% to keep monitoring
        return _random.Next(100) < 70;
    }

    private bool HasActiveIncidents()
    {
        lock (_lock)
        {
            return _activeIncidentIds.Count > 0;
        }
    }

    private void RemoveIncidentFromTracking(Guid incidentId)
    {
        lock (_lock)
        {
            _activeIncidentIds.Remove(incidentId);
        }
    }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        int cleared;
        lock (_lock)
        {
            cleared = _activeIncidentIds.Count;
            _activeIncidentIds.Clear();
        }

        _logger.LogInformation(
            "🔄 Incident simulator reset requested - cleared {Count} tracked incidents",
            cleared
        );

        return Task.CompletedTask;
    }
}
