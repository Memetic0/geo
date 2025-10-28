using GeoStream.Application.Abstractions;
using MediatR;

namespace GeoStream.Application.Queries.Incidents;

public sealed record SearchIncidentsQuery(
    string? SearchTerm = null,
    string? Severity = null,
    string? State = null,
    string? Type = null,
    int Page = 1,
    int PageSize = 100
) : IRequest<SearchIncidentsResult>;

public sealed record SearchIncidentsResult(
    IReadOnlyCollection<IncidentSummaryDto> Incidents,
    int TotalCount,
    int Page,
    int PageSize
);

public sealed class SearchIncidentsQueryHandler(IIncidentSearchService searchService)
    : IRequestHandler<SearchIncidentsQuery, SearchIncidentsResult>
{
    public async Task<SearchIncidentsResult> Handle(
        SearchIncidentsQuery request,
        CancellationToken cancellationToken
    )
    {
        return await searchService.SearchAsync(
            request.SearchTerm,
            request.Severity,
            request.State,
            request.Type,
            request.Page,
            request.PageSize,
            cancellationToken
        );
    }
}
