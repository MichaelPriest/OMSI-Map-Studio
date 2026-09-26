using System.Numerics;
using MapStudio.Core.Generation.Vegetation;
using MapStudio.Renderer.Scene;

namespace MapStudio.Renderer.Viewport;

public sealed record NativeOsmVegetationPlacementBuildResult(
    IReadOnlyList<NativeSceneryPlacementRequest> Requests,
    int SkippedPointCount);

public sealed class NativeOsmVegetationPlacementBuilder
{
    public NativeOsmVegetationPlacementBuildResult Build(
        NativeSceneSnapshot scene,
        IReadOnlyList<MapStudioProjectedVegetationPoint> points,
        string sceneryObjectPath,
        bool randomRotation)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            points);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            sceneryObjectPath);

        var requests =
            new List<
                NativeSceneryPlacementRequest>(
                    Math.Min(
                        points.Count,
                        256));

        var skipped =
            0;

        foreach (
            var point in points)
        {
            if (
                requests.Count >=
                    256)
            {
                skipped +=
                    points.Count -
                    requests.Count -
                    skipped;

                break;
            }

            if (
                !NativeTerrainSampler
                    .TryGetHeightAtWorldPoint(
                        scene,
                        point.Position.X,
                        point.Position.Z,
                        out var terrainHeight))
            {
                skipped++;
                continue;
            }

            var tileX =
                (int)Math.Floor(
                    point.Position.X /
                    300.0);

            var tileY =
                (int)Math.Floor(
                    point.Position.Z /
                    300.0);

            var tile =
                scene.Tiles
                    .FirstOrDefault(
                        candidate =>
                            candidate.Reference.X ==
                                tileX &&
                            candidate.Reference.Y ==
                                tileY);

            if (tile is null)
            {
                skipped++;
                continue;
            }

            var rotation =
                randomRotation
                    ? StableRotation(
                        point.Id)
                    : 0.0;

            requests.Add(
                new NativeSceneryPlacementRequest(
                    tile.Reference,
                    sceneryObjectPath,
                    point.Position.X -
                        tileX *
                        300.0,
                    point.Position.Z -
                        tileY *
                        300.0,
                    0.0,
                    rotation,
                    0.0,
                    0.0,
                    new Vector3(
                        (float)
                            point.Position.X,
                        (float)
                            terrainHeight,
                        (float)
                            point.Position.Z),
                    false));
        }

        return new NativeOsmVegetationPlacementBuildResult(
            requests,
            skipped);
    }

    private static double StableRotation(
        string value)
    {
        unchecked
        {
            uint hash =
                2166136261;

            foreach (
                var character in value)
            {
                hash ^=
                    character;

                hash *=
                    16777619;
            }

            return
                hash %
                    36000 /
                100.0;
        }
    }
}
