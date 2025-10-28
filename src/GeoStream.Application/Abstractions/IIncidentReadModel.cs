namespace GeoStream.Application.Abstractions;

using GeoStream.Application.Queries.Incidents;

public interface IIncidentReadModel
{
    Task<IncidentSummaryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IncidentSummaryDto>> ListActiveAsync(CancellationToken cancellationToken = default);
}
