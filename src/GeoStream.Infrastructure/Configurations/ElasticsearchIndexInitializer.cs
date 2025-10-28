using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using GeoStream.Infrastructure.Search;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeoStream.Infrastructure.Configurations;

public sealed class ElasticsearchIndexInitializer(
    ElasticsearchClient elasticClient,
    ILogger<ElasticsearchIndexInitializer> logger
) : IHostedService
{
    private const string IndexName = "geostream-incidents";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if index exists
            var existsResponse = await elasticClient.Indices.ExistsAsync(
                IndexName,
                cancellationToken
            );

            if (existsResponse.Exists)
            {
                logger.LogInformation(
                    "Elasticsearch index '{IndexName}' already exists",
                    IndexName
                );
                return;
            }

            // Create index with mappings
            var createResponse = await elasticClient.Indices.CreateAsync(
                IndexName,
                c =>
                    c.Mappings(m =>
                        m.Properties<IncidentSearchDocument>(p =>
                            p.Keyword(k => k.Id!)
                                .Keyword(k => k.Severity!)
                                .Keyword(k => k.State!)
                                .Keyword(k => k.Type!)
                                .IntegerNumber(n => n.Latitude)
                                .IntegerNumber(n => n.Longitude)
                                .Date(d => d.RaisedAt!)
                                .Keyword(k => k.SensorStationId!)
                                .Keyword(k => k.AssignedResponderId!)
                        )
                    ),
                cancellationToken
            );

            if (createResponse.IsSuccess())
            {
                logger.LogInformation("Created Elasticsearch index '{IndexName}'", IndexName);
            }
            else
            {
                logger.LogWarning(
                    "Failed to create Elasticsearch index '{IndexName}': {Error}",
                    IndexName,
                    createResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"
                );
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing Elasticsearch index '{IndexName}'", IndexName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
