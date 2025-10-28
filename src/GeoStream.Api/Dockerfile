# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["GeoStream.sln", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["global.json", "./"]
COPY ["src/GeoStream.Api/GeoStream.Api.csproj", "src/GeoStream.Api/"]
COPY ["src/GeoStream.Application/GeoStream.Application.csproj", "src/GeoStream.Application/"]
COPY ["src/GeoStream.Domain/GeoStream.Domain.csproj", "src/GeoStream.Domain/"]
COPY ["src/GeoStream.Infrastructure/GeoStream.Infrastructure.csproj", "src/GeoStream.Infrastructure/"]

# Restore dependencies
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "src/GeoStream.Api/GeoStream.Api.csproj"

# Copy source code
COPY ["src/", "src/"]

# Build
WORKDIR "/src/src/GeoStream.Api"
RUN dotnet build "GeoStream.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "GeoStream.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install curl for healthchecks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "GeoStream.Api.dll"]
