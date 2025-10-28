namespace GeoStream.Api.Services;

public interface IIncidentSimulatorControl
{
    Task ResetAsync(CancellationToken cancellationToken = default);
}
