using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusSceneryConversionOptions(
    string MeshPrefix = "object",
    bool GenerateCollider = false,
    double TerrainOffset = 0,
    bool FlipTextureV = false);

public static class
    ProtonBusOmsiSceneryGeometryConverter
{
    public static ProtonBusExportMesh Build(
        OmsiTileReference tile,
        OmsiPlacedObject placedObject,
        OmsiO3dGeometry geometry,
        OmsiSceneryMeshTransform?
            localTransform = null,
        int meshOrdinal = 0,
        ProtonBusSceneryConversionOptions?
            options = null)
    {
        ArgumentNullException.ThrowIfNull(
            tile);

        ArgumentNullException.ThrowIfNull(
            placedObject);

        ArgumentNullException.ThrowIfNull(
            geometry);

        options ??=
            new();

        localTransform ??=
            OmsiSceneryMeshTransform
                .Identity;

        if (
            !geometry.IsLoaded)
        {
            throw new ArgumentException(
                geometry.ErrorCode is null
                    ? "OMSI scenery geometry is not loaded."
                    : $"OMSI scenery geometry is not loaded: {geometry.ErrorCode}",
                nameof(geometry));
        }

        var vertexCount =
            geometry.Positions.Length /
            3;

        if (
            vertexCount <=
                0 ||
            geometry.Positions.Length !=
                vertexCount *
                3)
        {
            throw new ArgumentException(
                "OMSI scenery geometry contains an invalid position buffer.",
                nameof(geometry));
        }

        if (
            geometry.Indices.Length %
                3 !=
            0)
        {
            throw new ArgumentException(
                "OMSI scenery geometry index buffer is not triangular.",
                nameof(geometry));
        }

        var hasUvs =
            geometry.Uvs.Length >=
            vertexCount *
            2;

        var worldTransform =
            CreateLocalTransform(
                localTransform) *
            CreateObjectTransform(
                tile,
                placedObject,
                options.TerrainOffset);

        var vertices =
            new ProtonBusExportVertex[
                vertexCount];

        for (
            var vertexIndex = 0;
            vertexIndex <
                vertexCount;
            vertexIndex++)
        {
            var offset =
                vertexIndex *
                3;

            var position =
                Vector3.Transform(
                    new(
                        geometry.Positions[
                            offset],
                        geometry.Positions[
                            offset + 1],
                        geometry.Positions[
                            offset + 2]),
                    worldTransform);

            var uv =
                hasUvs
                    ? new Vector2(
                        geometry.Uvs[
                            vertexIndex *
                            2],
                        options.FlipTextureV
                            ? 1.0f -
                              geometry.Uvs[
                                  vertexIndex *
                                      2 +
                                  1]
                            : geometry.Uvs[
                                vertexIndex *
                                    2 +
                                1])
                    : Vector2.Zero;

            vertices[
                vertexIndex] =
                new(
                    position,
                    uv);
        }

        var materials =
            BuildMaterials(
                geometry,
                placedObject.ObjectId,
                meshOrdinal);

        var fallbackMaterialName =
            materials.Count >
                0
                ? materials[0].Name
                : $"object_{placedObject.ObjectId}_{meshOrdinal}_material_0";

        if (
            materials.Count ==
            0)
        {
            materials.Add(
                new(
                    fallbackMaterialName));
        }

        var triangleCount =
            geometry.Indices.Length /
            3;

        var triangles =
            new List<
                ProtonBusExportTriangle>(
                    triangleCount);

        for (
            var triangleIndex = 0;
            triangleIndex <
                triangleCount;
            triangleIndex++)
        {
            var offset =
                triangleIndex *
                3;

            var index0 =
                checked(
                    (int)
                        geometry.Indices[
                            offset]);

            // OMSI O3D uses the opposite winding from the Map Studio
            // renderer convention, so normalize it here. The 3DS writer
            // performs the separate handedness flip for Proton/Blender.
            var index1 =
                checked(
                    (int)
                        geometry.Indices[
                            offset +
                            2]);

            var index2 =
                checked(
                    (int)
                        geometry.Indices[
                            offset +
                            1]);

            ValidateIndex(
                index0,
                vertexCount,
                triangleIndex);

            ValidateIndex(
                index1,
                vertexCount,
                triangleIndex);

            ValidateIndex(
                index2,
                vertexCount,
                triangleIndex);

            var materialName =
                ResolveMaterialName(
                    geometry,
                    materials,
                    triangleIndex,
                    fallbackMaterialName);

            triangles.Add(
                new(
                    index0,
                    index1,
                    index2,
                    materialName));
        }

        var collider =
            options.GenerateCollider
                ? ProtonBusMeshNameTags
                    .Collider
                : string.Empty;

        return new(
            $"{options.MeshPrefix}_{tile.X}_{tile.Y}_{placedObject.ObjectId}_{meshOrdinal}{collider}",
            vertices,
            triangles.ToArray(),
            materials.ToArray());
    }

    private static List<
        ProtonBusExportMaterial>
        BuildMaterials(
            OmsiO3dGeometry geometry,
            int objectId,
            int meshOrdinal)
    {
        var output =
            new List<
                ProtonBusExportMaterial>(
                    geometry.Materials.Count);

        for (
            var index = 0;
            index <
                geometry.Materials.Count;
            index++)
        {
            var source =
                geometry.Materials[
                    index];

            var materialName =
                $"object_{objectId}_{meshOrdinal}_material_{index}";

            var textureName =
                string.IsNullOrWhiteSpace(
                    source.TextureName)
                    ? null
                    : ProtonBusTextureNamePlanner
                        .ToPortablePngName(
                            source.TextureName,
                            $"object_{objectId}_{meshOrdinal}_{index}");

            var emissive =
                Math.Abs(
                    source.EmissionR) >
                    0.001f ||
                Math.Abs(
                    source.EmissionG) >
                    0.001f ||
                Math.Abs(
                    source.EmissionB) >
                    0.001f;

            output.Add(
                new(
                    materialName,
                    textureName,
                    Transparent:
                        source.DiffuseA <
                        0.999f,
                    Emissive:
                        emissive,
                    DiffuseColor:
                        new(
                            source.DiffuseR,
                            source.DiffuseG,
                            source.DiffuseB),
                    Opacity:
                        source.DiffuseA));
        }

        return output;
    }

    private static string ResolveMaterialName(
        OmsiO3dGeometry geometry,
        IReadOnlyList<
            ProtonBusExportMaterial>
            materials,
        int triangleIndex,
        string fallback)
    {
        if (
            triangleIndex <
                0 ||
            triangleIndex >=
                geometry
                    .TriangleMaterialIndices
                    .Length)
        {
            return fallback;
        }

        var index =
            geometry
                .TriangleMaterialIndices[
                    triangleIndex];

        return index <
            materials.Count
                ? materials[
                    index]
                    .Name
                : fallback;
    }

    private static void ValidateIndex(
        int index,
        int vertexCount,
        int triangleIndex)
    {
        if (
            index >=
                0 &&
            index <
                vertexCount)
        {
            return;
        }

        throw new ArgumentException(
            $"OMSI scenery triangle {triangleIndex} references vertex {index}, outside 0..{vertexCount - 1}.");
    }

    private static Matrix4x4
        CreateObjectTransform(
            OmsiTileReference tile,
            OmsiPlacedObject placedObject,
            double terrainOffset)
    {
        var worldX =
            OmsiTileGrid.GetOriginX(
                tile.X) +
            placedObject.X;

        var worldY =
            placedObject.Z +
            terrainOffset;

        var worldZ =
            OmsiTileGrid.GetOriginZ(
                tile.Y) +
            placedObject.Y;

        return
            Matrix4x4
                .CreateFromYawPitchRoll(
                    DegreesToRadians(
                        placedObject
                            .Rotation),
                    DegreesToRadians(
                        placedObject
                            .Pitch),
                    DegreesToRadians(
                        placedObject
                            .Bank)) *
            Matrix4x4
                .CreateTranslation(
                    (float)worldX,
                    (float)worldY,
                    (float)worldZ);
    }

    private static Matrix4x4
        CreateLocalTransform(
            OmsiSceneryMeshTransform
                transform) =>
        Matrix4x4
            .CreateScale(
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

    private static float DegreesToRadians(
        double value) =>
        (float)(
            value *
            Math.PI /
            180.0);
}
