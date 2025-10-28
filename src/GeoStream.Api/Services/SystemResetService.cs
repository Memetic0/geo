using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using GeoStream.Infrastructure.Persistence;
using GeoStream.Infrastructure.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GeoStream.Api.Services;

public sealed class SystemResetService(
    IServiceScopeFactory scopeFactory,
    ElasticsearchClient elasticClient,
    IIncidentSimulatorControl simulatorControl,
    ILogger<SystemResetService> logger
)
{
    private const string IndexName = "geostream-incidents";

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("⚠️  System reset requested");

        await ResetReadModelAsync(cancellationToken).ConfigureAwait(false);
        await ResetElasticsearchAsync(cancellationToken).ConfigureAwait(false);
        await simulatorControl.ResetAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("✅ System reset completed");
    }

    private async Task ResetReadModelAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        logger.LogInformation("Clearing Postgres read model (incident_summaries)");

        await dbContext.Database
            .ExecuteSqlRawAsync(
                "TRUNCATE TABLE incident_summaries RESTART IDENTITY CASCADE;",
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task ResetElasticsearchAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Dropping Elasticsearch index '{IndexName}'", IndexName);

        await elasticClient.Indices
            .DeleteAsync(IndexName, d => d.IgnoreUnavailable(true), cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Recreating Elasticsearch index '{IndexName}'", IndexName);

        var response = await elasticClient.Indices
            .CreateAsync(
                IndexName,
                c =>
                    c.Mappings(m =>
                        m.Properties<IncidentSearchDocument>(p =>
                            p.Keyword(k => k.Id!)
                                .Keyword(k => k.Severity!)
                                .Keyword(k => k.State!)
                                .Keyword(k => k.Type!)
                                .DoubleNumber(n => n.Latitude)
                                .DoubleNumber(n => n.Longitude)
                                .Date(d => d.RaisedAt!)
                                .Keyword(k => k.SensorStationId!)
                                .Keyword(k => k.AssignedResponderId!)
                        )
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!response.IsSuccess())
        {
            var reason = response.ElasticsearchServerError?.Error?.Reason ?? "Unknown error";
            throw new InvalidOperationException(
                $"Failed to recreate Elasticsearch index '{IndexName}': {reason}"
            );
        }
    }
}
