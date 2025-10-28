using NetTopologySuite.Geometries;

namespace GeoStream.Domain.ValueObjects;

public sealed record GeoPoint(double Latitude, double Longitude)
{
    public Point ToPoint()
    {
        return new(Longitude, Latitude) { SRID = 4326 };
    }
}
