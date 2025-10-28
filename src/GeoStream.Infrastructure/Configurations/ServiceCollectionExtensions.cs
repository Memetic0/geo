using System;
using Elastic.Clients.Elasticsearch;
using GeoStream.Application.Abstractions;
using GeoStream.Infrastructure.Caching;
using GeoStream.Infrastructure.Messaging;
using GeoStream.Infrastructure.Persistence;
using GeoStream.Infrastructure.Queries;
using GeoStream.Infrastructure.Search;
using GeoStream.Infrastructure.Serialization;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GeoStream.Infrastructure.Configurations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGeoStreamInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var eventStoreConnection =
            configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException("Connection string 'EventStore' is required.");
        var readModelConnection =
            configuration.GetConnectionString("ReadModel")
            ?? throw new InvalidOperationException("Connection string 'ReadModel' is required.");

        services.AddDbContext<EventStoreDbContext>(options =>
        {
            options.UseSqlServer(eventStoreConnection);
        });

        services.AddDbContext<ReadModelDbContext>(options =>
        {
            options.UseNpgsql(readModelConnection, npgsql => npgsql.UseNetTopologySuite());
        });

        var redisConfiguration = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConfiguration;
        });
        services.AddSingleton<IncidentCache>();

        services.AddSingleton<JsonDomainEventSerializer>();

        services.AddSingleton(sp =>
        {
            var uri = configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
            var settings = new ElasticsearchClientSettings(new Uri(uri));
            return new ElasticsearchClient(settings);
        });

        services.AddScoped<IIncidentRepository, EventStoreIncidentRepository>();
        services.AddScoped<PostgresIncidentReadModel>();
        services.AddScoped<IIncidentReadModel>(sp =>
            sp.GetRequiredService<PostgresIncidentReadModel>()
        );
        services.AddScoped<IIncidentEventStore, IncidentEventStore>();
        services.AddScoped<IIncidentSearchService, ElasticsearchIncidentSearchService>();

        services.AddScoped<ProjectionDomainEventPublisher>();
        services.AddScoped<MassTransitDomainEventPublisher>();
        services.AddScoped<IDomainEventPublisher, CompositeDomainEventPublisher>();

        services.AddMassTransit(busConfigurator =>
        {
            var rabbitMqUri = configuration.GetConnectionString("RabbitMq");
            if (!string.IsNullOrWhiteSpace(rabbitMqUri))
            {
                busConfigurator.UsingRabbitMq(
                    (context, cfg) =>
                    {
                        cfg.Host(new Uri(rabbitMqUri));
                        cfg.ConfigureEndpoints(context);
                    }
                );
            }
            else
            {
                busConfigurator.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            }
        });

        services.AddHostedService<DatabaseInitializerHostedService>();
        services.AddHostedService<ElasticsearchIndexInitializer>();

        return services;
    }
}
