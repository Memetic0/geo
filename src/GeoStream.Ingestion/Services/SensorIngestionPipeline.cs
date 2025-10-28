using GeoStream.Application.Commands.Incidents;
using GeoStream.Domain.Enums;
using GeoStream.Domain.ValueObjects;
using MediatR;

namespace GeoStream.Ingestion.Services;

public sealed class SensorIngestionPipeline
{
    private readonly ILogger<SensorIngestionPipeline> _logger;
    private readonly ISender _sender;
    private readonly Random _random = new();

    public SensorIngestionPipeline(ISender sender, ILogger<SensorIngestionPipeline> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task PollAsync(CancellationToken cancellationToken)
    {
        var waterLevel = _random.NextDouble();
        if (waterLevel < 0.92)
        {
            return;
        }

        var severity = waterLevel switch
        {
            > 0.98 => IncidentSeverity.Critical,
            > 0.96 => IncidentSeverity.High,
            > 0.94 => IncidentSeverity.Moderate,
            _ => IncidentSeverity.Low,
        };

        var latitude = 51.50 + _random.NextDouble() / 100;
        var longitude = -0.12 + _random.NextDouble() / 100;
        var sensorId = $"sensor-{_random.Next(1, 5)}";

        var command = new RaiseIncidentCommand(
            IncidentType.StreetFlooding,
            latitude,
            longitude,
            severity,
            sensorId
        );
        var incidentId = await _sender.Send(command, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Raised synthetic incident {IncidentId} at severity {Severity} from {SensorId}",
            incidentId,
            severity,
            sensorId
        );
    }
}
