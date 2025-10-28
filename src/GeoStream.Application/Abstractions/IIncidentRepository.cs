using GeoStream.Domain.Aggregates;

namespace GeoStream.Application.Abstractions;

public interface IIncidentRepository
{
    Task<Incident?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAsync(Incident incident, CancellationToken cancellationToken = default);
}
