using GeoStream.Application.Abstractions;
using MediatR;

namespace GeoStream.Application.Queries.Incidents;

public sealed record GetIncidentSummaryQuery(Guid IncidentId) : IRequest<IncidentSummaryDto?>;

public sealed class GetIncidentSummaryQueryHandler(IIncidentReadModel readModel)
    : IRequestHandler<GetIncidentSummaryQuery, IncidentSummaryDto?>
{
    public Task<IncidentSummaryDto?> Handle(GetIncidentSummaryQuery request, CancellationToken cancellationToken)
    {
        return readModel.GetAsync(request.IncidentId, cancellationToken);
    }
}
