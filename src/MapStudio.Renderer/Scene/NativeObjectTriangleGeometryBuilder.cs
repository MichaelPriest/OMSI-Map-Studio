using System.Numerics;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public readonly record struct NativeTriangleRange(
    int StartVertex,
    int VertexCount);

public readonly record struct NativeMaterialBatch(
    int StartVertex,
    int VertexCount,
    string? TexturePath,
    string? MaskTexturePath = null,
    string? NightTexturePath = null,
    string? LightTexturePath = null,
    int? AlphaMode = null,
    bool NoZWrite = false,
    bool NoZCheck = false);

public sealed record NativeObjectTriangleGeometry(
    NativeMapVertex[] Vertices,
    NativeMapVertex[] PickingVertices,
    IReadOnlyDictionary<
        MapStudio.Renderer.Picking.PickingId,
        NativeTriangleRange> Ranges,
    IReadOnlyList<
        NativeMaterialBatch> MaterialBatches,
    int LoadedObjectCount,
    int LoadedMeshCount)
{
    public int TriangleCount =>
        Vertices.Length / 3;

    public int TexturedBatchCount =>
        MaterialBatches.Count(
            batch =>
                !string.IsNullOrWhiteSpace(
                    batch.TexturePath));
}

public sealed class NativeObjectTriangleGeometryBuilder
{
    private static readonly Vector4
        DefaultColor =
            new(
                0.62f,
                0.68f,
                0.72f,
                1.0f);

    public NativeObjectTriangleGeometry
        Build(
            NativeSceneSnapshot scene,
            IReadOnlyDictionary<
                string,
                NativeSceneryAsset>
                assets)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            assets);

        var vertices =
            new List<NativeMapVertex>(
                64_000);

        var pickingVertices =
            new List<NativeMapVertex>(
                64_000);

        var ranges =
            new Dictionary<
                MapStudio.Renderer.Picking.PickingId,
                NativeTriangleRange>();

        var materialBatches =
            new List<
                NativeMaterialBatch>();

        var loadedObjects = 0;
        var loadedMeshes = 0;

        foreach (
            var entity in scene.Objects)
        {
            var entityStart =
                vertices.Count;

            if (
                !assets.TryGetValue(
                    entity.Object
                        .SceneryObjectPath,
                    out var asset) ||
                !asset.IsLoaded)
            {
                continue;
            }

            var objectContributed =
                false;

            var terrainOffset =
                asset.UsesAbsoluteHeight
                    ? 0
                    : NativeTerrainSampler
                        .GetHeightAtObject(
                            scene,
                            entity);

            var lodThresholds =
                asset.Meshes
                    .Where(
                        mesh =>
                            mesh.LodThreshold
                                .HasValue)
                    .Select(
                        mesh =>
                            mesh.LodThreshold!
                                .Value)
                    .Distinct()
                    .OrderByDescending(
                        value => value)
                    .ToArray();

            var selectedLod =
                lodThresholds
                    .FirstOrDefault();

            foreach (
                var mesh in asset.Meshes)
            {
                if (
                    mesh.LodThreshold
                        .HasValue &&
                    lodThresholds.Length > 0 &&
                    Math.Abs(
                        mesh.LodThreshold
                            .Value -
                        selectedLod) >
                    0.000001)
                {
                    continue;
                }

                var before =
                    vertices.Count;

                AppendMesh(
                    entity,
                    mesh,
                    terrainOffset,
                    vertices,
                    pickingVertices,
                    materialBatches);

                if (
                    vertices.Count >
                    before)
                {
                    loadedMeshes++;
                    objectContributed =
                        true;
                }
            }

            if (objectContributed)
            {
                loadedObjects++;

                ranges[
                    entity.PickingId] =
                    new NativeTriangleRange(
                        entityStart,
                        vertices.Count -
                            entityStart);
            }
        }

        return new NativeObjectTriangleGeometry(
            vertices.ToArray(),
            pickingVertices.ToArray(),
            ranges,
            materialBatches.ToArray(),
            loadedObjects,
            loadedMeshes);
    }

    private static void AppendMesh(
        NativeObjectEntity entity,
        NativeSceneryMeshAsset mesh,
        double terrainOffset,
        List<NativeMapVertex> output,
        List<NativeMapVertex>
            pickingOutput,
        List<NativeMaterialBatch>
            materialBatches)
    {
        var geometry =
            mesh.Geometry;

        if (
            !geometry.IsLoaded ||
            geometry.Positions.Length <
                3 ||
            geometry.Indices.Length <
                3)
        {
            return;
        }

        var localTransform =
            CreateMeshTransform(
                mesh.Transform);

        var objectTransform =
            Matrix4x4.CreateFromYawPitchRoll(
                DegreesToRadians(
                    entity.Object
                        .Rotation),
                DegreesToRadians(
                    entity.Object
                        .Pitch),
                DegreesToRadians(
                    entity.Object
                        .Bank)) *
            Matrix4x4.CreateTranslation(
                entity.WorldX,
                entity.WorldY +
                    (float)
                        terrainOffset,
                entity.WorldZ);

        var worldTransform =
            localTransform *
            objectTransform;

        var pickingColor =
            EncodePickingColor(
                entity.PickingId);

        var vertexCount =
            geometry.Positions.Length /
            3;

        var hasUvs =
            geometry.Uvs.Length >=
            vertexCount * 2;

        var triangleCount =
            geometry.Indices.Length /
            3;

        for (
            var triangle = 0;
            triangle < triangleCount;
            triangle++)
        {
            var baseIndex =
                triangle * 3;

            var index0 =
                checked(
                    (int)
                        geometry.Indices[
                            baseIndex]);

            var index1 =
                checked(
                    (int)
                        geometry.Indices[
                            baseIndex +
                            1]);

            var index2 =
                checked(
                    (int)
                        geometry.Indices[
                            baseIndex +
                            2]);

            if (
                index0 < 0 ||
                index0 >= vertexCount ||
                index1 < 0 ||
                index1 >= vertexCount ||
                index2 < 0 ||
                index2 >= vertexCount)
            {
                continue;
            }

            var color =
                GetTriangleColor(
                    geometry,
                    triangle);

            var materialIndex =
                GetTriangleMaterialIndex(
                    geometry,
                    triangle);

            var texturePath =
                hasUvs
                    ? GetMaterialTexturePath(
                        mesh,
                        materialIndex)
                    : null;

            var nightTexturePath =
                hasUvs
                    ? GetMaterialOverridePath(
                        mesh.MaterialNightTexturePaths,
                        materialIndex)
                    : null;

            var lightTexturePath =
                hasUvs
                    ? GetMaterialOverridePath(
                        mesh.MaterialLightTexturePaths,
                        materialIndex)
                    : null;

            var alphaMode =
                GetMaterialAlphaMode(
                    mesh,
                    materialIndex);

            var noZWrite =
                GetMaterialFlag(
                    mesh.MaterialNoZWriteFlags,
                    materialIndex);

            var noZCheck =
                GetMaterialFlag(
                    mesh.MaterialNoZCheckFlags,
                    materialIndex);

            var triangleStart =
                output.Count;

            for (
                var corner = 0;
                corner < 3;
                corner++)
            {
                var sourceIndex =
                    corner switch
                    {
                        0 => index0,
                        1 => index1,
                        _ => index2
                    };

                var positionOffset =
                    sourceIndex * 3;

                var local =
                    new Vector3(
                        geometry.Positions[
                            positionOffset],
                        geometry.Positions[
                            positionOffset +
                            1],
                        geometry.Positions[
                            positionOffset +
                            2]);

                var world =
                    Vector3.Transform(
                        local,
                        worldTransform);

                var uv =
                    hasUvs
                        ? new Vector2(
                            geometry.Uvs[
                                sourceIndex *
                                2],
                            geometry.Uvs[
                                sourceIndex *
                                2 +
                                1])
                        : Vector2.Zero;

                output.Add(
                    new NativeMapVertex(
                        world,
                        color,
                        uv));

                pickingOutput.Add(
                    new NativeMapVertex(
                        world,
                        pickingColor));
            }

            AppendMaterialBatch(
                materialBatches,
                triangleStart,
                3,
                texturePath,
                nightTexturePath,
                lightTexturePath,
                alphaMode,
                noZWrite,
                noZCheck);
        }
    }

    private static void AppendMaterialBatch(
        List<NativeMaterialBatch> batches,
        int startVertex,
        int vertexCount,
        string? texturePath,
        string? nightTexturePath,
        string? lightTexturePath,
        int? alphaMode,
        bool noZWrite,
        bool noZCheck)
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
                    previous.NightTexturePath,
                    nightTexturePath,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    previous.LightTexturePath,
                    lightTexturePath,
                    StringComparison.OrdinalIgnoreCase) &&
                previous.AlphaMode ==
                    alphaMode &&
                previous.NoZWrite ==
                    noZWrite &&
                previous.NoZCheck ==
                    noZCheck)
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
                null,
                nightTexturePath,
                lightTexturePath,
                alphaMode,
                noZWrite,
                noZCheck));
    }

    private static int?
        GetTriangleMaterialIndex(
            OmsiO3dGeometry geometry,
            int triangle)
    {
        if (
            triangle < 0 ||
            triangle >=
                geometry
                    .TriangleMaterialIndices
                    .Length)
        {
            return null;
        }

        return geometry
            .TriangleMaterialIndices[
                triangle];
    }

    private static string?
        GetMaterialTexturePath(
            NativeSceneryMeshAsset mesh,
            int? materialIndex)
    {
        if (
            materialIndex is not int index ||
            index < 0 ||
            index >=
                mesh.MaterialTexturePaths
                    .Count)
        {
            return null;
        }

        return mesh
            .MaterialTexturePaths[
                index];
    }

    private static string?
        GetMaterialOverridePath(
            IReadOnlyList<string?>? paths,
            int? materialIndex)
    {
        if (
            paths is null ||
            materialIndex is not int index ||
            index < 0 ||
            index >= paths.Count)
        {
            return null;
        }

        return paths[index];
    }

    private static int?
        GetMaterialAlphaMode(
            NativeSceneryMeshAsset mesh,
            int? materialIndex)
    {
        if (
            mesh.MaterialAlphaModes is null ||
            materialIndex is not int index ||
            index < 0 ||
            index >=
                mesh.MaterialAlphaModes
                    .Count)
        {
            return null;
        }

        return mesh
            .MaterialAlphaModes[
                index];
    }

    private static bool
        GetMaterialFlag(
            IReadOnlyList<bool>? flags,
            int? materialIndex)
    {
        if (
            flags is null ||
            materialIndex is not int index ||
            index < 0 ||
            index >= flags.Count)
        {
            return false;
        }

        return flags[index];
    }

    private static Vector4
        EncodePickingColor(
            MapStudio.Renderer.Picking
                .PickingId pickingId)
    {
        var encoded =
            MapStudio.Renderer.Picking
                .PickingColorCodec
                .Encode(pickingId);

        return new Vector4(
            (encoded & 0xFF) / 255f,
            (
                (encoded >> 8) &
                0xFF
            ) / 255f,
            (
                (encoded >> 16) &
                0xFF
            ) / 255f,
            (
                (encoded >> 24) &
                0xFF
            ) / 255f);
    }

    private static Matrix4x4
        CreateMeshTransform(
            OmsiSceneryMeshTransform
                transform)
    {
        return
            Matrix4x4.CreateScale(
                (float)
                    transform.ScaleX,
                (float)
                    transform.ScaleY,
                (float)
                    transform.ScaleZ) *
            Matrix4x4
                .CreateFromYawPitchRoll(
                    DegreesToRadians(
                        transform
                            .RotationY),
                    DegreesToRadians(
                        transform
                            .RotationX),
                    DegreesToRadians(
                        transform
                            .RotationZ)) *
            Matrix4x4
                .CreateTranslation(
                    (float)
                        transform
                            .PositionX,
                    (float)
                        transform
                            .PositionY,
                    (float)
                        transform
                            .PositionZ);
    }

    private static float
        DegreesToRadians(
            double value) =>
            (float)(
                value *
                Math.PI /
                180.0);

    private static Vector4
        GetTriangleColor(
            OmsiO3dGeometry geometry,
            int triangle)
    {
        if (
            triangle >=
                geometry
                    .TriangleMaterialIndices
                    .Length)
        {
            return DefaultColor;
        }

        var materialIndex =
            geometry
                .TriangleMaterialIndices[
                    triangle];

        if (
            materialIndex >=
            geometry.Materials.Count)
        {
            return DefaultColor;
        }

        var material =
            geometry.Materials[
                materialIndex];

        return new Vector4(
            Math.Clamp(
                material.DiffuseR,
                0.08f,
                1.0f),
            Math.Clamp(
                material.DiffuseG,
                0.08f,
                1.0f),
            Math.Clamp(
                material.DiffuseB,
                0.08f,
                1.0f),
            Math.Clamp(
                material.DiffuseA,
                0.15f,
                1.0f));
    }
}
