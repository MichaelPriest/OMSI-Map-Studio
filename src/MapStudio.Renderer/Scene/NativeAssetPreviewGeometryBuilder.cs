using System.Numerics;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Renderer.Scene;

public sealed class NativeAssetPreviewGeometryBuilder
{
    private static readonly Vector4
        DefaultObjectColor =
            new(
                0.62f,
                0.68f,
                0.72f,
                1.0f);

    private static readonly Vector4
        DefaultSplineColor =
            new(
                0.42f,
                0.46f,
                0.49f,
                1.0f);

    public NativeAssetPreviewGeometry
        BuildScenery(
            NativeSceneryAsset asset)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        if (
            asset.Meshes.Count == 0)
        {
            return
                NativeAssetPreviewGeometry
                    .Error(
                        asset.ErrorCode ??
                        "previewNoRenderableMeshes");
        }

        var vertices =
            new List<NativeMapVertex>(
                16_384);

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

        var meshCount = 0;

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
                mesh,
                vertices);

            if (
                vertices.Count >
                before)
            {
                meshCount++;
            }
        }

        return
            CreateResult(
                vertices,
                meshCount,
                vertices.Count == 0
                    ? "previewNoTriangles"
                    : null);
    }

    public NativeAssetPreviewGeometry
        BuildSpline(
            NativeSplineAsset asset,
            float previewLength =
                30.0f)
    {
        ArgumentNullException.ThrowIfNull(
            asset);

        if (
            !asset.Definition.Exists ||
            asset.Definition
                .Surfaces.Count == 0)
        {
            return
                NativeAssetPreviewGeometry
                    .Error(
                        asset.ErrorCode ??
                        "previewNoSplineProfile");
        }

        previewLength =
            Math.Clamp(
                previewLength,
                2.0f,
                100.0f);

        var vertices =
            new List<NativeMapVertex>(
                4096);

        var segments =
            Math.Clamp(
                (int)MathF.Ceiling(
                    previewLength /
                    2.5f),
                2,
                64);

        foreach (
            var surface in
                asset.Definition
                    .Surfaces)
        {
            for (
                var segment = 0;
                segment < segments;
                segment++)
            {
                var z0 =
                    previewLength *
                    segment /
                    segments;

                var z1 =
                    previewLength *
                    (segment + 1) /
                    segments;

                var a =
                    new Vector3(
                        (float)
                            surface.From.X,
                        (float)
                            surface.From.Z,
                        z0);

                var b =
                    new Vector3(
                        (float)
                            surface.From.X,
                        (float)
                            surface.From.Z,
                        z1);

                var c =
                    new Vector3(
                        (float)
                            surface.To.X,
                        (float)
                            surface.To.Z,
                        z1);

                var d =
                    new Vector3(
                        (float)
                            surface.To.X,
                        (float)
                            surface.To.Z,
                        z0);

                AppendQuad(
                    a,
                    b,
                    c,
                    d,
                    DefaultSplineColor,
                    vertices);
            }
        }

        return
            CreateResult(
                vertices,
                asset.Definition
                    .Surfaces.Count,
                vertices.Count == 0
                    ? "previewNoSplineTriangles"
                    : null);
    }

    private static void AppendMesh(
        NativeSceneryMeshAsset mesh,
        List<NativeMapVertex> output)
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

        var transform =
            CreateMeshTransform(
                mesh.Transform);

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
                triangle *
                3;

            var triangleVertices =
                new Vector3[3];

            var valid = true;

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
                    sourceIndex *
                    3;

                if (
                    positionOffset + 2 >=
                    geometry.Positions
                        .Length)
                {
                    valid = false;
                    break;
                }

                triangleVertices[
                    corner] =
                    Vector3.Transform(
                        new Vector3(
                            geometry.Positions[
                                positionOffset],
                            geometry.Positions[
                                positionOffset +
                                1],
                            geometry.Positions[
                                positionOffset +
                                2]),
                        transform);
            }

            if (!valid)
            {
                continue;
            }

            output.Add(
                new NativeMapVertex(
                    triangleVertices[0],
                    color));

            output.Add(
                new NativeMapVertex(
                    triangleVertices[1],
                    color));

            output.Add(
                new NativeMapVertex(
                    triangleVertices[2],
                    color));
        }
    }

    private static Matrix4x4
        CreateMeshTransform(
            OmsiSceneryMeshTransform
                transform) =>
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
            return
                DefaultObjectColor;
        }

        var materialIndex =
            geometry
                .TriangleMaterialIndices[
                    triangle];

        if (
            materialIndex >=
            geometry.Materials.Count)
        {
            return
                DefaultObjectColor;
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

    private static void AppendQuad(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector4 color,
        List<NativeMapVertex> output)
    {
        output.Add(
            new NativeMapVertex(
                a,
                color));

        output.Add(
            new NativeMapVertex(
                b,
                color));

        output.Add(
            new NativeMapVertex(
                c,
                color));

        output.Add(
            new NativeMapVertex(
                a,
                color));

        output.Add(
            new NativeMapVertex(
                c,
                color));

        output.Add(
            new NativeMapVertex(
                d,
                color));
    }

    private static NativeAssetPreviewGeometry
        CreateResult(
            List<NativeMapVertex>
                vertices,
            int sourceMeshCount,
            string? errorCode)
    {
        if (
            vertices.Count == 0)
        {
            return new NativeAssetPreviewGeometry(
                Array.Empty<
                    NativeMapVertex>(),
                Vector3.Zero,
                Vector3.Zero,
                sourceMeshCount,
                errorCode ??
                    "previewEmpty");
        }

        var minimum =
            new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);

        var maximum =
            new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);

        foreach (
            var vertex in vertices)
        {
            minimum =
                Vector3.Min(
                    minimum,
                    vertex.Position);

            maximum =
                Vector3.Max(
                    maximum,
                    vertex.Position);
        }

        return new NativeAssetPreviewGeometry(
            vertices.ToArray(),
            minimum,
            maximum,
            sourceMeshCount,
            errorCode);
    }
}
