using GeoStream.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GeoStream.Api.Routing;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/system").WithTags("System");

        group.MapPost("reset", async (SystemResetService resetService, CancellationToken cancellationToken) =>
        {
            await resetService.ResetAsync(cancellationToken).ConfigureAwait(false);
            return Results.Accepted();
        });

        return app;
    }
}
