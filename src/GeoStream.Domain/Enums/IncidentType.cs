namespace GeoStream.Domain.Enums;

/// <summary>
/// Types of traffic and urban incidents that can be tracked by the system.
/// </summary>
public enum IncidentType
{
    /// <summary>
    /// Traffic congestion or heavy traffic
    /// </summary>
    TrafficCongestion,

    /// <summary>
    /// Road accident or collision
    /// </summary>
    RoadAccident,

    /// <summary>
    /// Road closure or blockage
    /// </summary>
    RoadClosure,

    /// <summary>
    /// Vehicle breakdown on roadway
    /// </summary>
    VehicleBreakdown,

    /// <summary>
    /// Construction or roadwork activity
    /// </summary>
    Roadwork,

    /// <summary>
    /// Public transport disruption
    /// </summary>
    PublicTransportDelay,

    /// <summary>
    /// Parking violation or illegal parking
    /// </summary>
    ParkingViolation,

    /// <summary>
    /// Traffic signal malfunction
    /// </summary>
    SignalMalfunction,

    /// <summary>
    /// Pedestrian incident or safety concern
    /// </summary>
    PedestrianIncident,

    /// <summary>
    /// Flooding on roadway (localized)
    /// </summary>
    StreetFlooding,
}
