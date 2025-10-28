using System.Text.Json;
using System.Text.Json.Serialization;
using GeoStream.Application.Queries.Incidents;
using Microsoft.Extensions.Caching.Distributed;

namespace GeoStream.Infrastructure.Caching;

public sealed class IncidentCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly IDistributedCache _cache;

    public IncidentCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<IncidentSummaryDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var payload = await _cache
            .GetStringAsync(CacheKey(id), cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<IncidentSummaryDto>(payload, SerializerOptions);
    }

    public Task SetAsync(IncidentSummaryDto summary, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
            SlidingExpiration = TimeSpan.FromMinutes(5),
        };

        var payload = JsonSerializer.Serialize(summary, SerializerOptions);
        return _cache.SetStringAsync(CacheKey(summary.Id), payload, options, cancellationToken);
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    private static string CacheKey(Guid id)
    {
        return $"incident:{id}";
    }
}
