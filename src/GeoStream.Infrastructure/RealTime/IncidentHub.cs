using Microsoft.AspNetCore.SignalR;

namespace GeoStream.Infrastructure.RealTime;

/// <summary>
/// SignalR hub for broadcasting real-time incident updates to connected clients.
/// </summary>
public sealed class IncidentHub : Hub
{
    // Hub methods can be added here if needed for client-to-server communication
    // Currently used only for server-to-client broadcasts via IHubContext
}
