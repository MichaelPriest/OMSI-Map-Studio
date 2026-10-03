namespace MapStudio.Core.Generation.Roads;

public readonly record struct MapStudioGeographicAnchor(
    double Latitude,
    double Longitude,
    double WorldX,
    double WorldZ);

public static class MapStudioGeographicProjection
{
    private const double EarthRadiusMeters =
        6378137.0;

    public static MapStudioRoadPoint Project(
        MapStudioGeographicAnchor anchor,
        MapStudioGeoRoadPoint point)
    {
        Validate(
            anchor,
            point);

        var latitudeRadians =
            anchor.Latitude *
            Math.PI /
            180.0;

        var longitudeScale =
            Math.Cos(
                latitudeRadians);

        if (
            Math.Abs(
                longitudeScale) <
            0.000001)
        {
            throw new InvalidDataException(
                "geographicProjectionLatitudeUnsupported");
        }

        var northMeters =
            (
                point.Latitude -
                anchor.Latitude
            ) *
            Math.PI /
            180.0 *
            EarthRadiusMeters;

        var eastMeters =
            (
                point.Longitude -
                anchor.Longitude
            ) *
            Math.PI /
            180.0 *
            EarthRadiusMeters *
            longitudeScale;

        return new MapStudioRoadPoint(
            anchor.WorldX +
                eastMeters,
            anchor.WorldZ -
                northMeters);
    }

    public static MapStudioGeoRoadPoint Unproject(
        MapStudioGeographicAnchor anchor,
        MapStudioRoadPoint point)
    {
        if (
            !double.IsFinite(
                point.X) ||
            !double.IsFinite(
                point.Z))
        {
            throw new InvalidDataException(
                "geographicProjectionPointInvalid");
        }

        ValidateAnchor(
            anchor);

        var latitudeRadians =
            anchor.Latitude *
            Math.PI /
            180.0;

        var longitudeScale =
            Math.Cos(
                latitudeRadians);

        if (
            Math.Abs(
                longitudeScale) <
            0.000001)
        {
            throw new InvalidDataException(
                "geographicProjectionLatitudeUnsupported");
        }

        var eastMeters =
            point.X -
            anchor.WorldX;

        var northMeters =
            -(
                point.Z -
                anchor.WorldZ
            );

        var latitude =
            anchor.Latitude +
            northMeters /
            EarthRadiusMeters *
            180.0 /
            Math.PI;

        var longitude =
            anchor.Longitude +
            eastMeters /
            (
                EarthRadiusMeters *
                longitudeScale
            ) *
            180.0 /
            Math.PI;

        return new MapStudioGeoRoadPoint(
            latitude,
            longitude);
    }

    private static void Validate(
        MapStudioGeographicAnchor anchor,
        MapStudioGeoRoadPoint point)
    {
        ValidateAnchor(
            anchor);

        if (
            !double.IsFinite(
                point.Latitude) ||
            !double.IsFinite(
                point.Longitude) ||
            point.Latitude is
                < -90 or > 90 ||
            point.Longitude is
                < -180 or > 180)
        {
            throw new InvalidDataException(
                "geographicProjectionPointInvalid");
        }
    }

    private static void ValidateAnchor(
        MapStudioGeographicAnchor anchor)
    {
        if (
            !double.IsFinite(
                anchor.Latitude) ||
            !double.IsFinite(
                anchor.Longitude) ||
            !double.IsFinite(
                anchor.WorldX) ||
            !double.IsFinite(
                anchor.WorldZ) ||
            anchor.Latitude is
                < -90 or > 90 ||
            anchor.Longitude is
                < -180 or > 180)
        {
            throw new InvalidDataException(
                "geographicProjectionAnchorInvalid");
        }
    }
}
