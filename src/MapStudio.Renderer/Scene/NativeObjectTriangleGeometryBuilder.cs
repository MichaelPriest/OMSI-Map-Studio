using System.Numerics;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public readonly record struct NativeTriangleRange(
    int StartVertex,
    int VertexCount);

public sealed record NativeObjectTriangleGeometry(
    NativeMapVertex[] Vertices,
    NativeMapVertex[] PickingVertices,
    IReadOnlyDictionary<
        MapStudio.Renderer.Picking.PickingId,
        NativeTriangleRange> Ranges,
    int LoadedObjectCount,
    int LoadedMeshCount)
{
    public int TriangleCount =>
        Vertices.Length / 3;
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

        var projection =
            NativeSceneProjection
                .FromScene(scene);

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
                    projection,
                    terrainOffset,
                    vertices,
                    pickingVertices);

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
            loadedObjects,
            loadedMeshes);
    }

    private static void AppendMesh(
        NativeObjectEntity entity,
        NativeSceneryMeshAsset mesh,
        NativeSceneProjection projection,
        double terrainOffset,
        List<NativeMapVertex> output,
        List<NativeMapVertex>
            pickingOutput)
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

        var triangleCount =
            geometry.Indices.Length /
            3;

        for (
            var triangle = 0;
            triangle < triangleCount;
            triangle++)
        {
            var color =
                GetTriangleColor(
                    geometry,
                    triangle);

            var baseIndex =
                triangle * 3;

            for (
                var corner = 0;
                corner < 3;
                corner++)
            {
                var sourceIndex =
                    checked(
                        (int)
                            geometry.Indices[
                                baseIndex +
                                corner]);

                var positionOffset =
                    sourceIndex * 3;

                if (
                    positionOffset + 2 >=
                    geometry.Positions
                        .Length)
                {
                    continue;
                }

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

                // First native geometry checkpoint: project the real O3D
                // triangles onto the same top-down map overview. The next
                // camera checkpoint will keep these world vertices and move
                // projection to the shader.
                var clip =
                    projection
                        .ProjectTopDown(
                            world.X,
                            world.Z,
                            depth:
                                Math.Clamp(
                                    0.55f -
                                    world.Y *
                                    0.0005f,
                                    0.05f,
                                    0.95f));

                output.Add(
                    new NativeMapVertex(
                        clip,
                        color));

                pickingOutput.Add(
                    new NativeMapVertex(
                        clip,
                        pickingColor));
            }
        }
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
