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
            asset.Meshes.Count == 0 &&
            asset.Tree is null)
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

        if (
            asset.Tree is
                { } tree)
        {
            var before =
                vertices.Count;

            AppendTreePreview(
                tree,
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
        BuildModel(
            OmsiO3dGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(
            geometry);

        if (
            !geometry.IsLoaded ||
            geometry.Positions.Length <
                3 ||
            geometry.Indices.Length <
                3)
        {
            return
                NativeAssetPreviewGeometry
                    .Error(
                        geometry.ErrorCode ??
                        "previewModelNotRenderable");
        }

        var vertices =
            new List<NativeMapVertex>(
                Math.Max(
                    3,
                    geometry.Indices.Length));

        AppendGeometry(
            geometry,
            Matrix4x4.Identity,
            vertices);

        return
            CreateResult(
                vertices,
                sourceMeshCount:
                    vertices.Count > 0
                        ? 1
                        : 0,
                vertices.Count == 0
                    ? "previewModelNoTriangles"
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

    private static void AppendTreePreview(
        OmsiSceneryTreeDefinition tree,
        List<NativeMapVertex> output)
    {
        var height =
            (float)Math.Max(
                0.5,
                (
                    tree.MinimumHeight +
                    tree.MaximumHeight
                ) /
                2.0);

        var aspect =
            (float)Math.Max(
                0.05,
                (
                    tree.MinimumAspect +
                    tree.MaximumAspect
                ) /
                2.0);

        var halfWidth =
            height *
            aspect *
            0.5f;

        var bottom =
            Vector3.Zero;

        var top =
            Vector3.UnitY *
            height;

        var treeColor =
            new Vector4(
                0.35f,
                0.68f,
                0.30f,
                1.0f);

        var leftX =
            bottom -
            Vector3.UnitX *
            halfWidth;

        var rightX =
            bottom +
            Vector3.UnitX *
            halfWidth;

        var topLeftX =
            top -
            Vector3.UnitX *
            halfWidth;

        var topRightX =
            top +
            Vector3.UnitX *
            halfWidth;

        AppendQuad(
            leftX,
            topLeftX,
            topRightX,
            rightX,
            treeColor,
            output);

        AppendQuad(
            rightX,
            topRightX,
            topLeftX,
            leftX,
            treeColor,
            output);

        var leftZ =
            bottom -
            Vector3.UnitZ *
            halfWidth;

        var rightZ =
            bottom +
            Vector3.UnitZ *
            halfWidth;

        var topLeftZ =
            top -
            Vector3.UnitZ *
            halfWidth;

        var topRightZ =
            top +
            Vector3.UnitZ *
            halfWidth;

        AppendQuad(
            leftZ,
            topLeftZ,
            topRightZ,
            rightZ,
            treeColor,
            output);

        AppendQuad(
            rightZ,
            topRightZ,
            topLeftZ,
            leftZ,
            treeColor,
            output);
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

        AppendGeometry(
            geometry,
            transform,
            output);
    }

    private static void AppendGeometry(
        OmsiO3dGeometry geometry,
        Matrix4x4 transform,
        List<NativeMapVertex> output)
    {
        if (
            !geometry.IsLoaded ||
            geometry.Positions.Length <
                3 ||
            geometry.Indices.Length <
                3)
        {
            return;
        }

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
                var sourceCorner =
                    corner switch
                    {
                        0 => 0,
                        1 => 2,
                        2 => 1,
                        _ => corner
                    };

                var sourceIndex =
                    checked(
                        (int)
                            geometry.Indices[
                                baseIndex +
                                sourceCorner]);

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
