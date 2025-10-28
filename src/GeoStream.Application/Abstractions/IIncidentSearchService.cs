using GeoStream.Application.Queries.Incidents;

namespace GeoStream.Application.Abstractions;

public interface IIncidentSearchService
{
    Task<SearchIncidentsResult> SearchAsync(
        string? searchTerm,
        string? severity,
        string? state,
        string? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );
}
