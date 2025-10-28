using GeoStream.Application.Commands;
using GeoStream.Application.Commands.Incidents;
using GeoStream.Application.Queries.Incidents;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GeoStream.Api.Routing;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/incidents").WithTags("Incidents");

        group.MapPost(
            "",
            async (
                [FromBody] RaiseIncidentRequest request,
                ISender sender,
                CancellationToken cancellationToken
            ) =>
            {
                var command = new RaiseIncidentCommand(
                    request.Type,
                    request.Latitude,
                    request.Longitude,
                    request.Severity,
                    request.SensorStationId
                );
                var incidentId = await sender
                    .Send(command, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Created($"/api/incidents/{incidentId}", new { id = incidentId });
            }
        );

        group.MapPost(
            "{incidentId:guid}/advance",
            async (
                Guid incidentId,
                [FromBody] AdvanceIncidentRequest request,
                ISender sender,
                CancellationToken cancellationToken
            ) =>
            {
                var command = new AdvanceIncidentCommand(
                    incidentId,
                    request.Action,
                    request.ResponderId
                );
                await sender.Send(command, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
        );

        group.MapPatch(
            "{incidentId:guid}/severity",
            async (
                Guid incidentId,
                [FromBody] UpdateIncidentSeverityRequest request,
                ISender sender,
                CancellationToken cancellationToken
            ) =>
            {
                var command = new UpdateIncidentSeverityCommand(incidentId, request.Severity);
                await sender.Send(command, cancellationToken).ConfigureAwait(false);
                return Results.NoContent();
            }
        );

        group.MapGet(
            "{incidentId:guid}",
            async (Guid incidentId, ISender sender, CancellationToken cancellationToken) =>
            {
                var summary = await sender
                    .Send(new GetIncidentSummaryQuery(incidentId), cancellationToken)
                    .ConfigureAwait(false);
                return summary is not null ? Results.Ok(summary) : Results.NotFound();
            }
        );

        group.MapGet(
            "{incidentId:guid}/history",
            async (Guid incidentId, ISender sender, CancellationToken cancellationToken) =>
            {
                var history = await sender
                    .Send(new GetIncidentHistoryQuery(incidentId), cancellationToken)
                    .ConfigureAwait(false);
                return history is not null ? Results.Ok(history) : Results.NotFound();
            }
        );

        group.MapGet(
            "",
            async (ISender sender, CancellationToken cancellationToken) =>
            {
                var incidents = await sender
                    .Send(new ListActiveIncidentsQuery(), cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(incidents);
            }
        );

        group.MapGet(
            "search",
            async (
                ISender sender,
                CancellationToken cancellationToken,
                string? searchTerm = null,
                string? severity = null,
                string? state = null,
                string? type = null,
                int page = 1,
                int pageSize = 100
            ) =>
            {
                var query = new SearchIncidentsQuery(
                    searchTerm,
                    severity,
                    state,
                    type,
                    page,
                    pageSize
                );
                var result = await sender
                    .Send(query, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(result);
            }
        );

        return app;
    }

    public sealed record RaiseIncidentRequest(
        Domain.Enums.IncidentType Type,
        double Latitude,
        double Longitude,
        Domain.ValueObjects.IncidentSeverity Severity,
        string SensorStationId
    );

    public sealed record AdvanceIncidentRequest(IncidentAdvanceAction Action, string? ResponderId);

    public sealed record UpdateIncidentSeverityRequest(
        Domain.ValueObjects.IncidentSeverity Severity
    );
}
