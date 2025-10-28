using System.Text.Json.Serialization;
using GeoStream.Api.Routing;
using GeoStream.Api.Services;
using GeoStream.Application.Commands.Incidents;
using GeoStream.Infrastructure.Configurations;
using GeoStream.Infrastructure.RealTime;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
        };
    });

builder.Services.AddAuthorization();

// Health checks for all infrastructure dependencies (per spec)
builder
    .Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("EventStore")
            ?? throw new InvalidOperationException("EventStore connection string required"),
        name: "sql-server-eventstore",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "db", "sql" }
    )
    .AddNpgSql(
        builder.Configuration.GetConnectionString("ReadModel")
            ?? throw new InvalidOperationException("ReadModel connection string required"),
        name: "postgres-readmodel",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "db", "postgres" }
    )
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
        name: "redis-cache",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "cache", "redis" }
    )
    .AddElasticsearch(
        options =>
        {
            var uri = builder.Configuration["Elasticsearch:Uri"] ?? "http://localhost:9200";
            options.UseServer(uri);
        },
        name: "elasticsearch-search",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "search", "elasticsearch" }
    )
    .AddRabbitMQ(
        rabbitConnectionString: builder.Configuration.GetConnectionString("RabbitMq")
            ?? "amqp://guest:guest@localhost:5672/",
        name: "rabbitmq-bus",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "messaging", "rabbitmq" }
    );

builder.Services.AddCors(options =>
{
    var allowedOrigins =
        builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[]
        {
            "http://localhost:5173",
        };

    options.AddPolicy(
        "spa",
        policy =>
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    );
});

builder
    .Services.AddSignalR()
    .AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(typeof(RaiseIncidentCommand).Assembly);
builder.Services.AddGeoStreamInfrastructure(builder.Configuration);
builder.Services.AddHostedService<IncidentSimulatorService>();

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("spa");

app.MapIncidentEndpoints();
app.MapHealthChecks(
    "/health",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds,
                    tags = e.Value.Tags,
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds,
            };
            await context.Response.WriteAsJsonAsync(response);
        },
    }
);
app.MapHub<IncidentHub>("/hubs/incidents");

app.Run();
