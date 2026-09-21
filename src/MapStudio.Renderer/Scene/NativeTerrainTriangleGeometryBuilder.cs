using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Renderer.Scene;

public sealed record NativeTerrainTriangleGeometry(
    NativeMapVertex[] Vertices,
    IReadOnlyList<
        NativeMaterialBatch> MaterialBatches)
{
    public int TriangleCount =>
        Vertices.Length / 3;

    public int TexturedBatchCount =>
        MaterialBatches.Count(
            batch =>
                !string.IsNullOrWhiteSpace(
                    batch.TexturePath));

    public int MaskedLayerCount =>
        MaterialBatches.Count(
            batch =>
                !string.IsNullOrWhiteSpace(
                    batch.MaskTexturePath));
}

public sealed class NativeTerrainTriangleGeometryBuilder
{
    private static readonly Vector4
        TexturedColor =
            Vector4.One;

    public NativeTerrainTriangleGeometry Build(
        NativeSceneSnapshot scene) =>
        BuildCore(
            scene,
            map: null,
            omsiRoot: null);

    public NativeTerrainTriangleGeometry Build(
        NativeSceneSnapshot scene,
        OmsiMapDescriptor map,
        string omsiRoot)
    {
        ArgumentNullException.ThrowIfNull(
            map);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        return BuildCore(
            scene,
            map,
            omsiRoot);
    }

    private static NativeTerrainTriangleGeometry BuildCore(
        NativeSceneSnapshot scene,
        OmsiMapDescriptor? map,
        string? omsiRoot)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var vertices =
            new List<NativeMapVertex>(
                32_768);

        var batches =
            new List<
                NativeMaterialBatch>();

        string? baseTexturePath =
            null;

        string? baseDetailTexturePath =
            null;

        var baseRepeating =
            1.0;

        var baseDetailRepeating =
            1.0;

        if (
            map is not null &&
            omsiRoot is not null &&
            map.GroundTextures.Count >
                0)
        {
            var ground =
                map.GroundTextures[0];

            baseRepeating =
                ground.MainTextureRepeating;

            baseDetailRepeating =
                ground.DetailTextureRepeating;

            if (
                OmsiTextureAssetPathResolver
                    .TryResolveGroundTexture(
                        omsiRoot,
                        map.DirectoryPath,
                        ground.MainTexturePath,
                        out var resolved))
            {
                baseTexturePath =
                    resolved;
            }

            if (
                OmsiTextureAssetPathResolver
                    .TryResolveGroundTexture(
                        omsiRoot,
                        map.DirectoryPath,
                        ground.DetailTexturePath,
                        out var detailResolved))
            {
                baseDetailTexturePath =
                    detailResolved;
            }
        }

        foreach (var tile in scene.Tiles)
        {
            var terrain =
                tile.Content.Terrain;

            if (
                terrain is null ||
                terrain.CellCount <= 0)
            {
                continue;
            }

            AppendTileLayer(
                tile,
                terrain,
                vertices,
                batches,
                baseTexturePath,
                maskTexturePath: null,
                baseDetailTexturePath,
                baseRepeating,
                baseDetailRepeating,
                heightOffset:
                    0.0f,
                fallbackToHeightColor:
                    baseTexturePath is null);

            var lightMapPath =
                map is null
                    ? null
                    : ResolveTerrainLightMapPath(
                        map,
                        tile.Reference);

            if (
                map is null ||
                omsiRoot is null ||
                map.GroundTextures.Count <=
                    1 ||
                tile.Content
                    .TerrainTextureMasks is
                    not { Count: > 0 }
                    masks)
            {
                if (lightMapPath is not null)
                {
                    AppendTileLayer(
                        tile,
                        terrain,
                        vertices,
                        batches,
                        lightMapPath,
                        maskTexturePath: null,
                        detailTexturePath: null,
                        repeating: 1.0,
                        detailRepeating: 1.0,
                        heightOffset: 0.04f,
                        fallbackToHeightColor: false,
                        additiveLightMap: true);
                }

                continue;
            }

            var overlayOrdinal =
                0;

            foreach (
                var mask in masks
                    .OrderBy(
                        item =>
                            item.LayerIndex))
            {
                if (
                    !mask.IsValid ||
                    mask.LayerIndex <= 0 ||
                    mask.LayerIndex >=
                        map.GroundTextures.Count)
                {
                    continue;
                }

                var ground =
                    map.GroundTextures[
                        mask.LayerIndex];

                if (
                    !OmsiTextureAssetPathResolver
                        .TryResolveGroundTexture(
                            omsiRoot,
                            map.DirectoryPath,
                            ground.MainTexturePath,
                            out var layerTexturePath))
                {
                    continue;
                }

                string? detailTexturePath =
                    null;

                if (
                    OmsiTextureAssetPathResolver
                        .TryResolveGroundTexture(
                            omsiRoot,
                            map.DirectoryPath,
                            ground.DetailTexturePath,
                            out var resolvedDetail))
                {
                    detailTexturePath =
                        resolvedDetail;
                }

                var maskPath =
                    Path.Combine(
                        map.DirectoryPath,
                        "texture",
                        "map",
                        mask.FileName);

                if (!File.Exists(maskPath))
                {
                    continue;
                }

                overlayOrdinal++;

                AppendTileLayer(
                    tile,
                    terrain,
                    vertices,
                    batches,
                    layerTexturePath,
                    maskPath,
                    detailTexturePath,
                    ground.MainTextureRepeating,
                    ground.DetailTextureRepeating,
                    heightOffset:
                        Math.Min(
                            overlayOrdinal,
                            16) *
                        0.002f,
                    fallbackToHeightColor:
                        false,
                    terrainLayerIndex:
                        mask.LayerIndex);
            }

            if (lightMapPath is not null)
            {
                AppendTileLayer(
                    tile,
                    terrain,
                    vertices,
                    batches,
                    lightMapPath,
                    maskTexturePath: null,
                    detailTexturePath: null,
                    repeating: 1.0,
                    detailRepeating: 1.0,
                    heightOffset: 0.04f,
                    fallbackToHeightColor: false,
                    additiveLightMap: true);
            }
        }

        return new NativeTerrainTriangleGeometry(
            vertices.ToArray(),
            batches.ToArray());
    }

    private static void AppendTileLayer(
        NativeSceneTile tile,
        OmsiTerrainGrid terrain,
        List<NativeMapVertex> vertices,
        List<NativeMaterialBatch> batches,
        string? texturePath,
        string? maskTexturePath,
        string? detailTexturePath,
        double repeating,
        double detailRepeating,
        float heightOffset,
        bool fallbackToHeightColor,
        bool additiveLightMap = false,
        int? terrainLayerIndex = null)
    {
        var cellCount =
            terrain.CellCount;

        var sampleCount =
            cellCount + 1;

        if (
            terrain.Heights.Count !=
                sampleCount *
                sampleCount)
        {
            return;
        }

        var layerStart =
            vertices.Count;

        var spacing =
            300.0 /
            cellCount;

        var originX =
            tile.Reference.X *
            300.0;

        var originZ =
            tile.Reference.Y *
            300.0;

        for (
            var row = 0;
            row < cellCount;
            row++)
        {
            for (
                var column = 0;
                column < cellCount;
                column++)
            {
                var topLeft =
                    row *
                    sampleCount +
                    column;

                var topRight =
                    topLeft + 1;

                var bottomLeft =
                    topLeft +
                    sampleCount;

                var bottomRight =
                    bottomLeft + 1;

                var h00 =
                    terrain.Heights[
                        topLeft];

                var h10 =
                    terrain.Heights[
                        topRight];

                var h01 =
                    terrain.Heights[
                        bottomLeft];

                var h11 =
                    terrain.Heights[
                        bottomRight];

                if (
                    !float.IsFinite(h00) ||
                    !float.IsFinite(h10) ||
                    !float.IsFinite(h01) ||
                    !float.IsFinite(h11))
                {
                    continue;
                }

                var localX0 =
                    column *
                    spacing;

                var localX1 =
                    localX0 +
                    spacing;

                var localZ0 =
                    row *
                    spacing;

                var localZ1 =
                    localZ0 +
                    spacing;

                var x0 =
                    originX +
                    localX0;

                var x1 =
                    originX +
                    localX1;

                var z0 =
                    originZ +
                    localZ0;

                var z1 =
                    originZ +
                    localZ1;

                var averageHeight =
                    (
                        h00 +
                        h10 +
                        h01 +
                        h11
                    ) /
                    4.0f;

                var color =
                    fallbackToHeightColor
                        ? GetTerrainColor(
                            averageHeight)
                        : TexturedColor;

                var uv00 =
                    CreateUv(
                        localX0,
                        localZ0,
                        repeating);

                var uv10 =
                    CreateUv(
                        localX1,
                        localZ0,
                        repeating);

                var uv01 =
                    CreateUv(
                        localX0,
                        localZ1,
                        repeating);

                var uv11 =
                    CreateUv(
                        localX1,
                        localZ1,
                        repeating);

                var maskUv00 =
                    CreateMaskUv(
                        localX0,
                        localZ0);

                var maskUv10 =
                    CreateMaskUv(
                        localX1,
                        localZ0);

                var maskUv01 =
                    CreateMaskUv(
                        localX0,
                        localZ1);

                var maskUv11 =
                    CreateMaskUv(
                        localX1,
                        localZ1);

                var detailUv00 =
                    CreateUv(
                        localX0,
                        localZ0,
                        detailRepeating);

                var detailUv10 =
                    CreateUv(
                        localX1,
                        localZ0,
                        detailRepeating);

                var detailUv01 =
                    CreateUv(
                        localX0,
                        localZ1,
                        detailRepeating);

                var detailUv11 =
                    CreateUv(
                        localX1,
                        localZ1,
                        detailRepeating);

                AppendTriangle(
                    x0,
                    z0,
                    h00 +
                        heightOffset,
                    uv00,
                    maskUv00,
                    detailUv00,
                    x1,
                    z1,
                    h11 +
                        heightOffset,
                    uv11,
                    maskUv11,
                    detailUv11,
                    x1,
                    z0,
                    h10 +
                        heightOffset,
                    uv10,
                    maskUv10,
                    detailUv10,
                    color,
                    vertices);

                AppendTriangle(
                    x0,
                    z0,
                    h00 +
                        heightOffset,
                    uv00,
                    maskUv00,
                    detailUv00,
                    x0,
                    z1,
                    h01 +
                        heightOffset,
                    uv01,
                    maskUv01,
                    detailUv01,
                    x1,
                    z1,
                    h11 +
                        heightOffset,
                    uv11,
                    maskUv11,
                    detailUv11,
                    color,
                    vertices);
            }
        }

        var layerVertexCount =
            vertices.Count -
            layerStart;

        if (layerVertexCount > 0)
        {
            AppendBatch(
                batches,
                layerStart,
                layerVertexCount,
                texturePath,
                maskTexturePath,
                detailTexturePath,
                additiveLightMap,
                terrainLayerIndex);
        }
    }

    private static string? ResolveTerrainLightMapPath(
        OmsiMapDescriptor map,
        OmsiTileReference tile)
    {
        var mapFilePath =
            Path.GetFullPath(
                Path.Combine(
                    map.DirectoryPath,
                    tile.RelativeMapPath));

        var candidates =
            new[]
            {
                mapFilePath + ".LM.bmp",
                mapFilePath + ".LM",
                mapFilePath + ".LM.dds"
            };

        return candidates
            .FirstOrDefault(
                File.Exists);
    }

    private static Vector2 CreateUv(
        double localX,
        double localZ,
        double repeating)
    {
        var safeRepeating =
            double.IsFinite(
                repeating) &&
            repeating > 0
                ? repeating
                : 1.0;

        return new Vector2(
            (float)(
                localX /
                300.0 *
                safeRepeating),
            (float)(
                localZ /
                300.0 *
                safeRepeating));
    }

    private static Vector2 CreateMaskUv(
        double localX,
        double localZ) =>
        new(
            (float)(
                localX /
                300.0),
            (float)(
                localZ /
                300.0));

    private static void AppendBatch(
        List<NativeMaterialBatch> batches,
        int startVertex,
        int vertexCount,
        string? texturePath,
        string? maskTexturePath,
        string? detailTexturePath,
        bool additiveLightMap,
        int? terrainLayerIndex)
    {
        if (
            batches.Count > 0)
        {
            var previous =
                batches[^1];

            if (
                previous.StartVertex +
                    previous.VertexCount ==
                    startVertex &&
                string.Equals(
                    previous.TexturePath,
                    texturePath,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    previous.MaskTexturePath,
                    maskTexturePath,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    previous.DetailTexturePath,
                    detailTexturePath,
                    StringComparison.OrdinalIgnoreCase) &&
                previous.AdditiveLightMap ==
                    additiveLightMap &&
                previous.TerrainLayerIndex ==
                    terrainLayerIndex)
            {
                batches[^1] =
                    previous with
                    {
                        VertexCount =
                            previous.VertexCount +
                            vertexCount
                    };

                return;
            }
        }

        batches.Add(
            new NativeMaterialBatch(
                startVertex,
                vertexCount,
                texturePath,
                maskTexturePath,
                DetailTexturePath:
                    detailTexturePath,
                AdditiveLightMap:
                    additiveLightMap,
                TerrainLayerIndex:
                    terrainLayerIndex));
    }

    private static void AppendTriangle(
        double x0,
        double z0,
        float height0,
        Vector2 uv0,
        Vector2 maskUv0,
        Vector2 detailUv0,
        double x1,
        double z1,
        float height1,
        Vector2 uv1,
        Vector2 maskUv1,
        Vector2 detailUv1,
        double x2,
        double z2,
        float height2,
        Vector2 uv2,
        Vector2 maskUv2,
        Vector2 detailUv2,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x0,
                    height0,
                    (float)z0),
                color,
                uv0,
                maskUv0,
                detailUv0));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x1,
                    height1,
                    (float)z1),
                color,
                uv1,
                maskUv1,
                detailUv1));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x2,
                    height2,
                    (float)z2),
                color,
                uv2,
                maskUv2,
                detailUv2));
    }

    private static Vector4 GetTerrainColor(
        float height)
    {
        var normalized =
            Math.Clamp(
                0.5f +
                height /
                120.0f,
                0.0f,
                1.0f);

        return new Vector4(
            0.10f +
                normalized *
                0.08f,
            0.20f +
                normalized *
                0.18f,
            0.10f +
                normalized *
                0.08f,
            1.0f);
    }
}
