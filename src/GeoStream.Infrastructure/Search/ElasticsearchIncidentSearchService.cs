using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using GeoStream.Application.Abstractions;
using GeoStream.Application.Queries.Incidents;
using Microsoft.Extensions.Logging;

namespace GeoStream.Infrastructure.Search;

public sealed class ElasticsearchIncidentSearchService(
    ElasticsearchClient elasticClient,
    IIncidentReadModel readModel,
    ILogger<ElasticsearchIncidentSearchService> logger
) : IIncidentSearchService
{
    private const string IndexName = "geostream-incidents";
    private static readonly string[] item =
    [
        "id",
        "sensorStationId",
        "assignedResponderId",
        "severity",
        "state",
    ];

    public async Task<SearchIncidentsResult> SearchAsync(
        string? searchTerm,
        string? severity,
        string? state,
        string? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            logger.LogInformation(
                "Searching incidents: searchTerm={SearchTerm}, severity={Severity}, state={State}, type={Type}, page={Page}, pageSize={PageSize}",
                searchTerm,
                severity,
                state,
                type,
                page,
                pageSize
            );

            // Build Elasticsearch query
            var mustQueries = new List<Query>();
            var mustNotQueries = new List<Query>();

            // Exclude resolved incidents by default unless explicitly filtering for them
            if (
                string.IsNullOrWhiteSpace(state)
                || !state.Equals("Resolved", StringComparison.OrdinalIgnoreCase)
            )
            {
                mustNotQueries.Add(new TermQuery(new Field("state")) { Value = "Resolved" });
            }

            // Add full-text search if provided
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                mustQueries.Add(new MultiMatchQuery { Query = searchTerm, Fields = item });
            }

            // Add severity filter
            if (
                !string.IsNullOrWhiteSpace(severity)
                && !severity.Equals("all", StringComparison.OrdinalIgnoreCase)
            )
            {
                mustQueries.Add(
                    new TermQuery(new Field("severity"))
                    {
                        Value = severity,
                        CaseInsensitive = true,
                    }
                );
            }

            // Add state filter
            if (
                !string.IsNullOrWhiteSpace(state)
                && !state.Equals("all", StringComparison.OrdinalIgnoreCase)
            )
            {
                mustQueries.Add(
                    new TermQuery(new Field("state")) { Value = state, CaseInsensitive = true }
                );
            }

            // Add type filter
            if (
                !string.IsNullOrWhiteSpace(type)
                && !type.Equals("all", StringComparison.OrdinalIgnoreCase)
            )
            {
                mustQueries.Add(
                    new TermQuery(new Field("type")) { Value = type, CaseInsensitive = true }
                );
            }

            // Build the bool query
            var boolQuery = new BoolQuery();

            if (mustQueries.Count > 0)
            {
                boolQuery.Must = mustQueries.ToArray();
            }

            if (mustNotQueries.Count > 0)
            {
                boolQuery.MustNot = mustNotQueries.ToArray();
            }

            Query finalQuery = boolQuery;

            // Execute search
            var from = (page - 1) * pageSize;
            var searchResponse = await elasticClient.SearchAsync<IncidentSearchDocument>(
                s =>
                    s.Index(IndexName)
                        .Query(finalQuery)
                        .From(from)
                        .Size(pageSize)
                        .Sort(ss =>
                            ss.Field(f => f.RaisedAt, new FieldSort { Order = SortOrder.Desc })
                        ),
                cancellationToken
            );

            if (!searchResponse.IsSuccess())
            {
                logger.LogWarning(
                    "Elasticsearch search failed: {Error}",
                    searchResponse.ElasticsearchServerError?.Error?.Reason ?? "Unknown error"
                );
                // Fallback to read model
                return await FallbackToReadModelAsync(
                    searchTerm,
                    severity,
                    state,
                    type,
                    page,
                    pageSize,
                    cancellationToken
                );
            }

            var total = searchResponse.Total;
            var incidentIds = searchResponse.Documents.Select(d => d.Id).ToList();

            logger.LogInformation(
                "Elasticsearch returned {Count} incidents out of {Total} total",
                incidentIds.Count,
                total
            );

            // Fetch full DTOs from read model
            var incidents = new List<IncidentSummaryDto>();
            foreach (var id in incidentIds)
            {
                var incident = await readModel.GetAsync(id, cancellationToken);
                if (incident != null)
                {
                    incidents.Add(incident);
                }
            }

            return new SearchIncidentsResult(incidents, (int)total, page, pageSize);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during Elasticsearch search");
            // Fallback to read model
            return await FallbackToReadModelAsync(
                searchTerm,
                severity,
                state,
                type,
                page,
                pageSize,
                cancellationToken
            );
        }
    }

    private async Task<SearchIncidentsResult> FallbackToReadModelAsync(
        string? searchTerm,
        string? severity,
        string? state,
        string? type,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("Falling back to read model for search");

        var allIncidents = await readModel.ListActiveAsync(cancellationToken);
        var filtered = allIncidents.AsEnumerable();

        // Apply filters (in-memory)
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
                || i.SensorStationId.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (
                    i.AssignedResponderId?.Contains(term, StringComparison.OrdinalIgnoreCase)
                    ?? false
                )
                || i.Severity.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
                || i.State.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (
            !string.IsNullOrWhiteSpace(severity)
            && !severity.Equals("all", StringComparison.OrdinalIgnoreCase)
        )
        {
            filtered = filtered.Where(i =>
                i.Severity.ToString().Equals(severity, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (
            !string.IsNullOrWhiteSpace(state)
            && !state.Equals("all", StringComparison.OrdinalIgnoreCase)
        )
        {
            filtered = filtered.Where(i =>
                i.State.ToString().Equals(state, StringComparison.OrdinalIgnoreCase)
            );
        }

        if (
            !string.IsNullOrWhiteSpace(type)
            && !type.Equals("all", StringComparison.OrdinalIgnoreCase)
        )
        {
            filtered = filtered.Where(i =>
                i.Type.ToString().Equals(type, StringComparison.OrdinalIgnoreCase)
            );
        }

        var results = filtered.OrderByDescending(i => i.RaisedAt).ToList();

        var total = results.Count;
        var paged = results.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new SearchIncidentsResult(paged, total, page, pageSize);
    }
}

public sealed class IncidentSearchDocument
{
    public Guid Id { get; set; }
    public string? Severity { get; set; }
    public string? State { get; set; }
    public string? Type { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RaisedAt { get; set; }
    public string SensorStationId { get; set; } = string.Empty;
    public string? AssignedResponderId { get; set; }
}
