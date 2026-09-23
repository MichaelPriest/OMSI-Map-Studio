using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusResolvedSceneryMesh(
    OmsiO3dGeometry Geometry,
    OmsiSceneryMeshTransform Transform,
    int MeshOrdinal,
    bool GenerateCollider = false,
    bool Invisible = false);

public sealed record ProtonBusResolvedSceneryAsset(
    IReadOnlyList<ProtonBusResolvedSceneryMesh>
        Meshes,
    bool UsesAbsoluteHeight = false);

public sealed record ProtonBusOmsiTileExportOptions(
    ProtonBusSplineTessellationOptions?
        SplineOptions = null,
    ProtonBusTerrainTessellationOptions?
        TerrainOptions = null,
    ProtonBusSceneryConversionOptions?
        SceneryOptions = null);

public sealed record ProtonBusOmsiTileExportResult(
    ProtonBusExportScene Scene,
    int TerrainMeshCount,
    int SplineMeshCount,
    int SceneryMeshCount,
    IReadOnlyList<string>
        MissingSplineDefinitions,
    IReadOnlyList<string>
        MissingSceneryAssets)
{
    public bool HasMissingAssets =>
        MissingSplineDefinitions.Count >
            0 ||
        MissingSceneryAssets.Count >
            0;
}

public static class ProtonBusOmsiTileExportBuilder
{
    public static ProtonBusOmsiTileExportResult
        Build(
            OmsiTileReference tile,
            OmsiTileContent content,
            IReadOnlyDictionary<
                string,
                OmsiSplineDefinition>
                splineDefinitions,
            IReadOnlyDictionary<
                string,
                ProtonBusResolvedSceneryAsset>
                sceneryAssets,
            ProtonBusOmsiTileExportOptions?
                options = null)
    {
        ArgumentNullException.ThrowIfNull(
            tile);

        ArgumentNullException.ThrowIfNull(
            content);

        ArgumentNullException.ThrowIfNull(
            splineDefinitions);

        ArgumentNullException.ThrowIfNull(
            sceneryAssets);

        options ??=
            new();

        var meshes =
            new List<
                ProtonBusExportMesh>();

        var missingSplines =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        var missingObjects =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);

        var terrainCount =
            0;

        var splineCount =
            0;

        var sceneryCount =
            0;

        if (
            content.Terrain is
                { } terrain)
        {
            meshes.Add(
                ProtonBusOmsiTerrainTessellator
                    .Build(
                        tile,
                        terrain,
                        options
                            .TerrainOptions));

            terrainCount++;
        }

        foreach (
            var spline
            in content.Splines)
        {
            if (
                !TryGetByPath(
                    splineDefinitions,
                    spline.SplinePath,
                    out var definition) ||
                definition is null ||
                !definition.Exists)
            {
                missingSplines.Add(
                    spline.SplinePath);

                continue;
            }

            var mesh =
                ProtonBusOmsiSplineTessellator
                    .Build(
                        tile,
                        spline,
                        definition,
                        options
                            .SplineOptions);

            if (
                mesh.Vertices.Count >
                    0 &&
                mesh.Triangles.Count >
                    0)
            {
                meshes.Add(
                    mesh);

                splineCount++;
            }
        }

        foreach (
            var placedObject
            in content.Objects)
        {
            if (
                !TryGetByPath(
                    sceneryAssets,
                    placedObject
                        .SceneryObjectPath,
                    out var asset) ||
                asset is null)
            {
                missingObjects.Add(
                    placedObject
                        .SceneryObjectPath);

                continue;
            }

            foreach (
                var resolvedMesh
                in asset.Meshes)
            {
                if (
                    !resolvedMesh
                        .Geometry
                        .IsLoaded)
                {
                    continue;
                }

                var baseOptions =
                    options
                        .SceneryOptions ??
                    new();

                var terrainOffset =
                    asset.UsesAbsoluteHeight
                        ? 0.0
                        : ProtonBusOmsiTerrainSampler
                            .GetHeightAtLocalPoint(
                                content.Terrain,
                                placedObject.X,
                                placedObject.Y);

                var mesh =
                    ProtonBusOmsiSceneryGeometryConverter
                        .Build(
                            tile,
                            placedObject,
                            resolvedMesh
                                .Geometry,
                            resolvedMesh
                                .Transform,
                            resolvedMesh
                                .MeshOrdinal,
                            baseOptions with
                            {
                                GenerateCollider =
                                    resolvedMesh
                                        .GenerateCollider ||
                                    baseOptions
                                        .GenerateCollider,
                                Invisible =
                                    resolvedMesh
                                        .Invisible ||
                                    baseOptions
                                        .Invisible,
                                TerrainOffset =
                                    baseOptions
                                        .TerrainOffset +
                                    terrainOffset
                            });

                if (
                    mesh.Vertices.Count >
                        0 &&
                    mesh.Triangles.Count >
                        0)
                {
                    meshes.Add(
                        mesh);

                    sceneryCount++;
                }
            }
        }

        return new(
            new(
                meshes.ToArray()),
            terrainCount,
            splineCount,
            sceneryCount,
            missingSplines
                .OrderBy(
                    value =>
                        value,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray(),
            missingObjects
                .OrderBy(
                    value =>
                        value,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray());
    }

    private static bool TryGetByPath<T>(
        IReadOnlyDictionary<string, T>
            values,
        string path,
        out T? value)
    {
        if (
            values.TryGetValue(
                path,
                out value))
        {
            return true;
        }

        var normalized =
            NormalizePath(
                path);

        foreach (
            var pair
            in values)
        {
            if (
                string.Equals(
                    NormalizePath(
                        pair.Key),
                    normalized,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                value =
                    pair.Value;

                return true;
            }
        }

        value =
            default;

        return false;
    }

    private static string NormalizePath(
        string value) =>
        value
            .Trim()
            .Replace(
                '\\',
                '/');
}
