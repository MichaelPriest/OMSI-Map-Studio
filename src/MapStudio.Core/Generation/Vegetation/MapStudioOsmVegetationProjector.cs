using MapStudio.Core.Generation.Roads;

namespace MapStudio.Core.Generation.Vegetation;

public sealed record MapStudioProjectedVegetationPoint(
    string Id,
    MapStudioRoadPoint Position,
    MapStudioOsmVegetationKind Kind,
    string? Species,
    string? Genus,
    string? LeafType,
    string? Name);

public sealed class MapStudioOsmVegetationProjector
{
    public IReadOnlyList<
        MapStudioProjectedVegetationPoint>
        Project(
            IReadOnlyList<
                MapStudioGeoVegetationPoint>
                points,
            MapStudioGeographicAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(
            points);

        var result =
            new List<
                MapStudioProjectedVegetationPoint>(
                    points.Count);

        foreach (
            var point in points)
        {
            var projected =
                MapStudioGeographicProjection
                    .Project(
                        anchor,
                        new MapStudioGeoRoadPoint(
                            point.Latitude,
                            point.Longitude));

            result.Add(
                new MapStudioProjectedVegetationPoint(
                    point.Id,
                    projected,
                    point.Kind,
                    point.Species,
                    point.Genus,
                    point.LeafType,
                    point.Name));
        }

        return result;
    }
}
