using GeoStream.Application.Abstractions;
using MediatR;

namespace GeoStream.Application.Queries.Incidents;

public sealed record ListActiveIncidentsQuery : IRequest<IReadOnlyCollection<IncidentSummaryDto>>;

public sealed class ListActiveIncidentsQueryHandler(IIncidentReadModel readModel)
    : IRequestHandler<ListActiveIncidentsQuery, IReadOnlyCollection<IncidentSummaryDto>>
{
    public Task<IReadOnlyCollection<IncidentSummaryDto>> Handle(
        ListActiveIncidentsQuery request,
        CancellationToken cancellationToken
    )
    {
        return readModel.ListActiveAsync(cancellationToken);
    }
}
