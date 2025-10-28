using GeoStream.Application.Commands.Incidents;
using GeoStream.Infrastructure.Configurations;
using GeoStream.Ingestion;
using GeoStream.Ingestion.Services;
using MediatR;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddGeoStreamInfrastructure(builder.Configuration);
builder.Services.AddMediatR(typeof(RaiseIncidentCommand).Assembly);
builder.Services.AddSingleton<SensorIngestionPipeline>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
