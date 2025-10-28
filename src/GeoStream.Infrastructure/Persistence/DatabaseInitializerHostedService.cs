using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeoStream.Infrastructure.Persistence;

public sealed class DatabaseInitializerHostedService(
    IServiceProvider serviceProvider,
    ILogger<DatabaseInitializerHostedService> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var eventContext = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        var readModelContext = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        await eventContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await readModelContext.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Verified GeoStream databases.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
