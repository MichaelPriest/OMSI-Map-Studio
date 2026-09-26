using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.ProtonBus;

public static class ProtonBusCoordinateSpace
{
    /// <summary>
    /// Converts the Map Studio/renderer Y-up space into the Blender-style
    /// Z-up space historically used by Proton Bus 3DS content.
    /// Proton Bus documentation states that X keeps its axis while Blender
    /// Y maps to Unity/Proton Z and Blender Z maps to Unity/Proton Y.
    /// </summary>
    public static Vector3 ToProton3ds(
        Vector3 mapStudioPosition) =>
        new(
            mapStudioPosition.X,
            mapStudioPosition.Z,
            mapStudioPosition.Y);

    /// <summary>
    /// Swapping Y and Z changes handedness, so triangle winding must be
    /// reversed when positions are transformed to Proton/Blender 3DS space.
    /// </summary>
    public static ProtonBusExportTriangle
        ToProton3ds(
            ProtonBusExportTriangle triangle) =>
        triangle with
        {
            B = triangle.C,
            C = triangle.B
        };
}

public static class ProtonBusTextureNamePlanner
{
    public static string ToPortablePngName(
        string? sourceTexture,
        string fallback)
    {
        var fileName =
            string.IsNullOrWhiteSpace(
                sourceTexture)
                ? fallback
                : Path.GetFileNameWithoutExtension(
                    sourceTexture);

        if (
            string.IsNullOrWhiteSpace(
                fileName))
        {
            fileName =
                fallback;
        }

        var safe =
            new string(
                fileName
                    .Select(
                        character =>
                            char.IsAsciiLetterOrDigit(
                                character) ||
                            character is
                                '_' or
                                '-'
                                ? character
                                : '_')
                    .ToArray())
                .Trim(
                    '_');

        if (
            string.IsNullOrWhiteSpace(
                safe))
        {
            safe =
                fallback;
        }

        return
            safe +
            ".png";
    }
}

public sealed record ProtonBusSplineTessellationOptions(
    double MaximumSegmentLength = 4.0,
    bool GenerateCollider = true,
    string MeshPrefix = "spline");

public static class ProtonBusOmsiSplineTessellator
{
    public static ProtonBusExportMesh Build(
        OmsiTileReference tile,
        OmsiPlacedSpline spline,
        OmsiSplineDefinition definition,
        ProtonBusSplineTessellationOptions?
            options = null)
    {
        ArgumentNullException.ThrowIfNull(
            tile);

        ArgumentNullException.ThrowIfNull(
            spline);

        ArgumentNullException.ThrowIfNull(
            definition);

        options ??=
            new();

        if (
            !double.IsFinite(
                options
                    .MaximumSegmentLength) ||
            options.MaximumSegmentLength <=
                0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum segment length must be finite and greater than zero.");
        }

        var length =
            Math.Max(
                0.0,
                spline.Length);

        var surfaces =
            definition
                .Surfaces;

        if (
            length <
                0.001 ||
            surfaces.Count ==
                0)
        {
            return new(
                BuildMeshName(
                    tile,
                    spline,
                    options),
                [],
                [],
                []);
        }

        var segmentCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    length /
                    options
                        .MaximumSegmentLength));

        var vertices =
            new List<
                ProtonBusExportVertex>(
                    segmentCount *
                    surfaces.Count *
                    4);

        var triangles =
            new List<
                ProtonBusExportTriangle>(
                    segmentCount *
                    surfaces.Count *
                    2);

        var materials =
            new Dictionary<
                string,
                ProtonBusExportMaterial>(
                    StringComparer.Ordinal);

        for (
            var surfaceIndex = 0;
            surfaceIndex <
                surfaces.Count;
            surfaceIndex++)
        {
            var surface =
                surfaces[
                    surfaceIndex];

            var materialName =
                $"{options.MeshPrefix}_{tile.X}_{tile.Y}_{spline.SplineId}_material_{surface.TextureIndex}_{surfaceIndex}";

            if (
                !materials.ContainsKey(
                    materialName))
            {
                var textureName =
                    ProtonBusTextureNamePlanner
                        .ToPortablePngName(
                            surface.TextureName,
                            $"spline_{surface.TextureIndex}");

                materials.Add(
                    materialName,
                    new(
                        materialName,
                        textureName,
                        Transparent:
                            surface.AlphaMode !=
                            0));
            }

            for (
                var segment = 0;
                segment <
                    segmentCount;
                segment++)
            {
                var distance0 =
                    length *
                    segment /
                    segmentCount;

                var distance1 =
                    length *
                    (segment +
                     1) /
                    segmentCount;

                var frame0 =
                    GetFrame(
                        tile,
                        spline,
                        distance0);

                var frame1 =
                    GetFrame(
                        tile,
                        spline,
                        distance1);

                var left0 =
                    TransformProfilePoint(
                        frame0,
                        surface.From);

                var left1 =
                    TransformProfilePoint(
                        frame1,
                        surface.From);

                var right1 =
                    TransformProfilePoint(
                        frame1,
                        surface.To);

                var right0 =
                    TransformProfilePoint(
                        frame0,
                        surface.To);

                var baseVertex =
                    vertices.Count;

                vertices.Add(
                    new(
                        left0,
                        CreateUv(
                            surface.From,
                            distance0)));

                vertices.Add(
                    new(
                        left1,
                        CreateUv(
                            surface.From,
                            distance1)));

                vertices.Add(
                    new(
                        right1,
                        CreateUv(
                            surface.To,
                            distance1)));

                vertices.Add(
                    new(
                        right0,
                        CreateUv(
                            surface.To,
                            distance0)));

                triangles.Add(
                    new(
                        baseVertex,
                        baseVertex + 1,
                        baseVertex + 2,
                        materialName));

                triangles.Add(
                    new(
                        baseVertex,
                        baseVertex + 2,
                        baseVertex + 3,
                        materialName));
            }
        }

        return new(
            BuildMeshName(
                tile,
                spline,
                options),
            vertices.ToArray(),
            triangles.ToArray(),
            materials
                .Values
                .ToArray());
    }

    private static string BuildMeshName(
        OmsiTileReference tile,
        OmsiPlacedSpline spline,
        ProtonBusSplineTessellationOptions
            options)
    {
        var collider =
            options.GenerateCollider
                ? ProtonBusMeshNameTags
                    .Collider
                : string.Empty;

        return
            $"{options.MeshPrefix}_{tile.X}_{tile.Y}_{spline.SplineId}{collider}";
    }

    private static ProtonBusSplineFrame
        GetFrame(
            OmsiTileReference tile,
            OmsiPlacedSpline spline,
            double distance)
    {
        var length =
            Math.Max(
                0.0,
                spline.Length);

        var clampedDistance =
            Math.Clamp(
                distance,
                0.0,
                length);

        var yaw =
            spline.Rotation *
            Math.PI /
            180.0;

        var hasCurve =
            Math.Abs(
                spline.Radius) >
            0.001;

        var curveAngle =
            hasCurve
                ? clampedDistance /
                  spline.Radius
                : 0.0;

        var localX =
            hasCurve
                ? spline.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          curveAngle)
                  )
                : 0.0;

        var localZ =
            hasCurve
                ? spline.Radius *
                  Math.Sin(
                      curveAngle)
                : clampedDistance;

        var cosYaw =
            Math.Cos(
                yaw);

        var sinYaw =
            Math.Sin(
                yaw);

        var startX =
            OmsiTileGrid.GetOriginX(
                tile.X) +
            spline.X;

        var startZ =
            OmsiTileGrid.GetOriginZ(
                tile.Y) +
            spline.Y;

        var worldX =
            startX +
            localX *
                cosYaw +
            localZ *
                sinYaw;

        var worldZ =
            startZ -
            localX *
                sinYaw +
            localZ *
                cosYaw;

        var heading =
            yaw +
            curveAngle;

        var forward =
            Vector3.Normalize(
                new(
                    (float)Math.Sin(
                        heading),
                    0,
                    (float)Math.Cos(
                        heading)));

        var lateral =
            new Vector3(
                forward.Z,
                0,
                -forward.X);

        var worldY =
            spline.Z +
            GetGradientRise(
                spline.GradientStart,
                spline.GradientEnd,
                length,
                clampedDistance);

        return new(
            new(
                (float)worldX,
                (float)worldY,
                (float)worldZ),
            lateral);
    }

    private static Vector3
        TransformProfilePoint(
            ProtonBusSplineFrame frame,
            OmsiSplineProfilePoint point) =>
        frame.Center +
        frame.Lateral *
            (float)point.X +
        Vector3.UnitY *
            (float)point.Z;

    private static Vector2 CreateUv(
        OmsiSplineProfilePoint point,
        double distance) =>
        new(
            (float)point.TextureX,
            (float)(
                distance *
                point.TextureScale));

    internal static double
        GetGradientRise(
            double gradientStart,
            double gradientEnd,
            double length,
            double distance)
    {
        if (
            length <=
            0)
        {
            return 0;
        }

        var clampedDistance =
            Math.Clamp(
                distance,
                0.0,
                length);

        var startSlope =
            gradientStart /
            100.0;

        var slopeDelta =
            (
                gradientEnd -
                gradientStart
            ) /
            100.0;

        return
            startSlope *
                clampedDistance +
            0.5 *
                slopeDelta *
                clampedDistance *
                clampedDistance /
                length;
    }

    private readonly record struct
        ProtonBusSplineFrame(
            Vector3 Center,
            Vector3 Lateral);
}

public sealed record ProtonBusTerrainTessellationOptions(
    string MeshPrefix = "terrain",
    string MaterialName = "terrain",
    string? TextureFileName = null,
    bool GenerateCollider = true);

public static class ProtonBusOmsiTerrainTessellator
{
    public static ProtonBusExportMesh Build(
        OmsiTileReference tile,
        OmsiTerrainGrid terrain,
        ProtonBusTerrainTessellationOptions?
            options = null)
    {
        ArgumentNullException.ThrowIfNull(
            tile);

        ArgumentNullException.ThrowIfNull(
            terrain);

        options ??=
            new();

        if (
            terrain.CellCount <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(terrain),
                "Terrain must contain at least one cell.");
        }

        var sampleCount =
            terrain.SampleCount;

        if (
            terrain.Heights.Count !=
            sampleCount *
            sampleCount)
        {
            throw new ArgumentException(
                "Terrain height count does not match its cell grid.",
                nameof(terrain));
        }

        var spacing =
            OmsiTileGrid.TileSize /
            terrain.CellCount;

        var originX =
            OmsiTileGrid.GetOriginX(
                tile.X);

        var originZ =
            OmsiTileGrid.GetOriginZ(
                tile.Y);

        var vertices =
            new ProtonBusExportVertex[
                sampleCount *
                sampleCount];

        for (
            var row = 0;
            row <
                sampleCount;
            row++)
        {
            for (
                var column = 0;
                column <
                    sampleCount;
                column++)
            {
                var index =
                    row *
                        sampleCount +
                    column;

                var height =
                    terrain.Heights[
                        index];

                if (
                    !float.IsFinite(
                        height))
                {
                    throw new ArgumentException(
                        $"Terrain sample {index} is not finite.",
                        nameof(terrain));
                }

                var localX =
                    column *
                    spacing;

                var localZ =
                    row *
                    spacing;

                vertices[
                    index] =
                    new(
                        new(
                            (float)(
                                originX +
                                localX),
                            height,
                            (float)(
                                originZ +
                                localZ)),
                        new(
                            (float)(
                                localX /
                                OmsiTileGrid
                                    .TileSize),
                            (float)(
                                localZ /
                                OmsiTileGrid
                                    .TileSize)));
            }
        }

        var materialName =
            $"{options.MaterialName}_{tile.X}_{tile.Y}";

        var triangles =
            new List<
                ProtonBusExportTriangle>(
                    terrain.CellCount *
                    terrain.CellCount *
                    2);

        for (
            var row = 0;
            row <
                terrain.CellCount;
            row++)
        {
            for (
                var column = 0;
                column <
                    terrain.CellCount;
                column++)
            {
                var topLeft =
                    row *
                        sampleCount +
                    column;

                var topRight =
                    topLeft +
                    1;

                var bottomLeft =
                    topLeft +
                    sampleCount;

                var bottomRight =
                    bottomLeft +
                    1;

                triangles.Add(
                    new(
                        topLeft,
                        bottomRight,
                        topRight,
                        materialName));

                triangles.Add(
                    new(
                        topLeft,
                        bottomLeft,
                        bottomRight,
                        materialName));
            }
        }

        var texture =
            string.IsNullOrWhiteSpace(
                options.TextureFileName)
                ? null
                : ProtonBusTextureNamePlanner
                    .ToPortablePngName(
                        options.TextureFileName,
                        "terrain");

        var collider =
            options.GenerateCollider
                ? ProtonBusMeshNameTags
                    .Collider
                : string.Empty;

        return new(
            $"{options.MeshPrefix}_{tile.X}_{tile.Y}{collider}",
            vertices,
            triangles.ToArray(),
            [
                new(
                    materialName,
                    texture)
            ]);
    }
}


public static class ProtonBusOmsiTerrainSampler
{
    public static double GetHeightAtLocalPoint(
        OmsiTerrainGrid? terrain,
        double localX,
        double localZ)
    {
        if (
            terrain is null ||
            terrain.CellCount <=
                0)
        {
            return 0;
        }

        var cellCount =
            terrain.CellCount;

        var sampleCount =
            cellCount +
            1;

        if (
            terrain.Heights.Count !=
            sampleCount *
            sampleCount)
        {
            return 0;
        }

        var gridX =
            Math.Clamp(
                localX /
                OmsiTileGrid.TileSize *
                cellCount,
                0,
                cellCount);

        var gridZ =
            Math.Clamp(
                localZ /
                OmsiTileGrid.TileSize *
                cellCount,
                0,
                cellCount);

        var column0 =
            (int)Math.Floor(
                gridX);

        var row0 =
            (int)Math.Floor(
                gridZ);

        var column1 =
            Math.Min(
                cellCount,
                column0 +
                1);

        var row1 =
            Math.Min(
                cellCount,
                row0 +
                1);

        var fractionX =
            gridX -
            column0;

        var fractionZ =
            gridZ -
            row0;

        var height00 =
            terrain.Heights[
                row0 *
                    sampleCount +
                column0];

        var height10 =
            terrain.Heights[
                row0 *
                    sampleCount +
                column1];

        var height01 =
            terrain.Heights[
                row1 *
                    sampleCount +
                column0];

        var height11 =
            terrain.Heights[
                row1 *
                    sampleCount +
                column1];

        if (
            !float.IsFinite(
                height00) ||
            !float.IsFinite(
                height10) ||
            !float.IsFinite(
                height01) ||
            !float.IsFinite(
                height11))
        {
            return 0;
        }

        var top =
            height00 +
            (
                height10 -
                height00
            ) *
            fractionX;

        var bottom =
            height01 +
            (
                height11 -
                height01
            ) *
            fractionX;

        return
            top +
            (
                bottom -
                top
            ) *
            fractionZ;
    }
}
