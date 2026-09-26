using System.Globalization;
using System.Numerics;
using System.Text;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Core.Omsi.Junctions;

public sealed record MapStudioJunctionArm(
    double AngleDegrees,
    double WidthMeters,
    int LaneCount = 2,
    bool OneWay = false,
    double LaneWidthMeters = 3.5,
    int InboundLaneCount = -1,
    int OutboundLaneCount = -1);

public enum MapStudioJunctionStructureKind
{
    Ground,
    Bridge,
    Tunnel
}

public sealed record MapStudioJunctionSpec(
    string Name,
    IReadOnlyList<MapStudioJunctionArm> Arms,
    double SurfaceHeightMeters = 0.102,
    MapStudioJunctionStructureKind StructureKind =
        MapStudioJunctionStructureKind.Ground)
{
    public MapStudioJunctionSpec Normalize()
    {
        if (
            Arms is null ||
            Arms.Count < 3 ||
            Arms.Count > 8)
        {
            throw new InvalidDataException(
                "junctionArmCountInvalid");
        }

        var normalizedArms =
            Arms
                .Select(
                    arm =>
                        arm with
                        {
                            AngleDegrees =
                                NormalizeAngle(
                                    arm.AngleDegrees),
                            WidthMeters =
                                Math.Clamp(
                                    double.IsFinite(
                                        arm.WidthMeters)
                                        ? arm.WidthMeters
                                        : 7.0,
                                    2.0,
                                    40.0),
                            LaneCount =
                                Math.Clamp(
                                    arm.LaneCount,
                                    1,
                                    8),
                            LaneWidthMeters =
                                Math.Clamp(
                                    double.IsFinite(
                                        arm.LaneWidthMeters)
                                        ? arm.LaneWidthMeters
                                        : 3.5,
                                    2.0,
                                    4.5),
                            InboundLaneCount =
                                NormalizeDirectionalLaneCount(
                                    arm.InboundLaneCount,
                                    arm.LaneCount,
                                    arm.OneWay,
                                    inbound:
                                        true),
                            OutboundLaneCount =
                                NormalizeDirectionalLaneCount(
                                    arm.OutboundLaneCount,
                                    arm.LaneCount,
                                    arm.OneWay,
                                    inbound:
                                        false)
                        })
                .OrderBy(
                    arm =>
                        arm.AngleDegrees)
                .ToArray();

        return this with
        {
            Name =
                string.IsNullOrWhiteSpace(
                    Name)
                    ? "Junction"
                    : Name.Trim(),
            Arms =
                normalizedArms,
            SurfaceHeightMeters =
                Math.Clamp(
                    double.IsFinite(
                        SurfaceHeightMeters)
                        ? SurfaceHeightMeters
                        : 0.102,
                    0,
                    1),
            StructureKind =
                Enum.IsDefined(
                    typeof(
                        MapStudioJunctionStructureKind),
                    StructureKind)
                    ? StructureKind
                    : MapStudioJunctionStructureKind
                        .Ground
        };
    }

    private static int NormalizeDirectionalLaneCount(
        int value,
        int laneCount,
        bool oneWay,
        bool inbound)
    {
        if (value >= 0)
        {
            return Math.Clamp(
                value,
                0,
                Math.Max(
                    1,
                    laneCount));
        }

        if (oneWay)
        {
            return inbound
                ? 0
                : Math.Max(
                    1,
                    laneCount);
        }

        if (laneCount <= 1)
        {
            return 1;
        }

        return inbound
            ? laneCount /
                2
            : laneCount -
              laneCount /
                2;
    }

    private static double NormalizeAngle(
        double angle)
    {
        if (!double.IsFinite(angle))
        {
            return 0;
        }

        angle %= 360.0;

        if (angle < 0)
        {
            angle += 360.0;
        }

        return angle;
    }
}

public sealed record MapStudioJunctionAssetResult(
    string ObjectDirectory,
    string SceneryObjectPath,
    string MeshPath,
    int InternalPathCount,
    string? BackupDirectory);

public sealed class MapStudioJunctionAssetGenerator
{
    public const string RootFolderName =
        "MapStudio_Junctions";

    private const int
        JunctionOutlineSampleCount =
            144;

    private const double
        JunctionMovementDegreesPerSegment =
            22.5;

    private const int
        JunctionMovementMaximumSegments =
            8;

    public async Task<MapStudioJunctionAssetResult>
        GenerateAsync(
            string omsiRoot,
            MapStudioJunctionSpec spec,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentNullException.ThrowIfNull(
            spec);

        var normalized =
            spec.Normalize();

        var root =
            Path.GetFullPath(
                omsiRoot);

        var sceneryRoot =
            Path.Combine(
                root,
                "Sceneryobjects",
                RootFolderName);

        Directory.CreateDirectory(
            sceneryRoot);

        var assetName =
            SanitizeName(
                normalized.Name);

        var objectDirectory =
            Path.Combine(
                sceneryRoot,
                assetName);

        string? backupDirectory =
            null;

        if (
            Directory.Exists(
                objectDirectory) &&
            Directory
                .EnumerateFileSystemEntries(
                    objectDirectory)
                .Any())
        {
            backupDirectory =
                Path.Combine(
                    root,
                    ".mapstudio",
                    "backups",
                    "junctions",
                    assetName +
                    "-" +
                    DateTime.UtcNow
                        .ToString(
                            "yyyyMMdd-HHmmss",
                            CultureInfo.InvariantCulture));

            CopyDirectory(
                objectDirectory,
                backupDirectory);
        }

        var temporaryDirectory =
            objectDirectory +
            ".tmp-" +
            Guid.NewGuid()
                .ToString("N");

        Directory.CreateDirectory(
            temporaryDirectory);

        try
        {
            var modelDirectory =
                Path.Combine(
                    temporaryDirectory,
                    "model");

            var textureDirectory =
                Path.Combine(
                    temporaryDirectory,
                    "Texture");

            Directory.CreateDirectory(
                modelDirectory);

            Directory.CreateDirectory(
                textureDirectory);

            await EnsureJunctionAsphaltAsync(
                    root,
                    textureDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

            await EnsureJunctionSidewalkAsync(
                    root,
                    textureDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

            await EnsureJunctionMarkingAsync(
                    root,
                    textureDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

            var geometry =
                BuildGeometry(
                    normalized);

            var meshPath =
                Path.Combine(
                    modelDirectory,
                    "junction.o3d");

            await new OmsiO3dGeometryWriter()
                .WriteAsync(
                    meshPath,
                    geometry,
                    cancellationToken)
                .ConfigureAwait(false);

            var paths =
                BuildInternalPaths(
                    normalized);

            var scoPath =
                Path.Combine(
                    temporaryDirectory,
                    assetName +
                    ".sco");

            await File.WriteAllTextAsync(
                    scoPath,
                    BuildSceneryObject(
                        normalized,
                        paths),
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            await File.WriteAllTextAsync(
                    Path.Combine(
                        temporaryDirectory,
                        "mapstudio-junction.txt"),
                    BuildManifest(
                        normalized,
                        paths.Count),
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            if (
                Directory.Exists(
                    objectDirectory))
            {
                Directory.Delete(
                    objectDirectory,
                    recursive: true);
            }

            Directory.Move(
                temporaryDirectory,
                objectDirectory);

            temporaryDirectory =
                string.Empty;

            return new MapStudioJunctionAssetResult(
                objectDirectory,
                Path.Combine(
                    objectDirectory,
                    assetName +
                    ".sco"),
                Path.Combine(
                    objectDirectory,
                    "model",
                    "junction.o3d"),
                paths.Count,
                backupDirectory);
        }
        finally
        {
            if (
                !string.IsNullOrWhiteSpace(
                    temporaryDirectory) &&
                Directory.Exists(
                    temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }

    private static async Task EnsureJunctionAsphaltAsync(
        string omsiRoot,
        string textureDirectory,
        CancellationToken cancellationToken)
    {
        var target =
            Path.Combine(
                textureDirectory,
                "ms_junction_asphalt.bmp");

        var roadKitAsphalt =
            Path.Combine(
                omsiRoot,
                "Splines",
                MapStudioRoadKitGenerator
                    .PackFolderName,
                "Texture",
                "ms_asphalt.bmp");

        if (File.Exists(
                roadKitAsphalt))
        {
            File.Copy(
                roadKitAsphalt,
                target,
                overwrite:
                    true);

            return;
        }

        await MapStudioGeneratedTextureFactory
            .EnsureBmpAsync(
                textureDirectory,
                "ms_junction_asphalt.bmp",
                256,
                256,
                static (x, y) =>
                {
                    var fine =
                        Math.Abs(
                            (
                                x *
                                    73 ^
                                y *
                                    151 ^
                                x *
                                    y *
                                    17
                            ) %
                            19);

                    var coarse =
                        Math.Abs(
                            (
                                x /
                                    4 *
                                    29 +
                                y /
                                    4 *
                                    43
                            ) %
                            17);

                    var value =
                        Math.Clamp(
                            48 +
                            fine /
                                2 +
                            coarse /
                                3,
                            32,
                            82);

                    return new MapStudioGeneratedRgb(
                        (byte)value,
                        (byte)value,
                        (byte)Math.Min(
                            255,
                            value +
                                3));
                },
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async Task EnsureJunctionSidewalkAsync(
        string omsiRoot,
        string textureDirectory,
        CancellationToken cancellationToken)
    {
        var target =
            Path.Combine(
                textureDirectory,
                "ms_junction_sidewalk.bmp");

        var roadKitSidewalk =
            Path.Combine(
                omsiRoot,
                "Splines",
                MapStudioRoadKitGenerator
                    .PackFolderName,
                "Texture",
                "ms_sidewalk.bmp");

        if (File.Exists(
                roadKitSidewalk))
        {
            File.Copy(
                roadKitSidewalk,
                target,
                overwrite:
                    true);

            return;
        }

        await MapStudioGeneratedTextureFactory
            .EnsureBmpAsync(
                textureDirectory,
                "ms_junction_sidewalk.bmp",
                128,
                128,
                static (x, y) =>
                {
                    var joint =
                        (
                            x % 32 <= 1 ||
                            y % 32 <= 1
                        )
                            ? -14
                            : 0;

                    var grain =
                        Math.Abs(
                            (
                                x *
                                    41 ^
                                y *
                                    67
                            ) %
                            11) -
                        5;

                    var value =
                        Math.Clamp(
                            142 +
                            joint +
                            grain,
                            104,
                            174);

                    return new MapStudioGeneratedRgb(
                        (byte)value,
                        (byte)value,
                        (byte)Math.Min(
                            255,
                            value +
                                2));
                },
                cancellationToken)
            .ConfigureAwait(false);
    }


    private static async Task EnsureJunctionMarkingAsync(
        string omsiRoot,
        string textureDirectory,
        CancellationToken cancellationToken)
    {
        var target =
            Path.Combine(
                textureDirectory,
                "ms_junction_marking.bmp");

        var roadKitMarking =
            Path.Combine(
                omsiRoot,
                "Splines",
                MapStudioRoadKitGenerator
                    .PackFolderName,
                "Texture",
                "ms_marking.bmp");

        if (File.Exists(
                roadKitMarking))
        {
            File.Copy(
                roadKitMarking,
                target,
                overwrite:
                    true);

            return;
        }

        await MapStudioGeneratedTextureFactory
            .EnsureBmpAsync(
                textureDirectory,
                "ms_junction_marking.bmp",
                64,
                64,
                static (_, _) =>
                    new MapStudioGeneratedRgb(
                        238,
                        236,
                        218),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public OmsiO3dGeometry BuildGeometry(
        MapStudioJunctionSpec spec)
    {
        var normalized =
            spec.Normalize();

        var extent =
            MapStudioJunctionGeometrySizing
                .ResolveSurfaceExtentMeters(
                    normalized.Arms
                        .Select(
                            arm =>
                                arm.WidthMeters));

        var outline =
            BuildJunctionOutline(
                normalized,
                extent);

        if (outline.Count < 3)
        {
            throw new InvalidDataException(
                "junctionOutlineInvalid");
        }

        // The radial footprint is star-shaped around the actual
        // junction origin. Keep the fan center fixed at the origin
        // so concave cut-backs between road mouths triangulate safely.
        const double centerX =
            0;

        const double centerZ =
            0;

        var positions =
            new List<float>(
                (
                    outline.Count +
                    1
                ) *
                3);

        var normals =
            new List<float>(
                (
                    outline.Count +
                    1
                ) *
                3);

        var uvs =
            new List<float>(
                (
                    outline.Count +
                    1
                ) *
                2);

        var indices =
            new List<uint>(
                outline.Count *
                3);

        var triangleMaterials =
            new List<ushort>(
                outline.Count);

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)centerX,
                (float)normalized
                    .SurfaceHeightMeters,
                (float)centerZ),
            (float)(
                0.5 +
                centerX /
                (
                    extent *
                    2.0
                )),
            (float)(
                0.5 -
                centerZ /
                (
                    extent *
                    2.0
                )));

        foreach (
            var point in
                outline)
        {
            AddVertex(
                positions,
                normals,
                uvs,
                new Vector3(
                    (float)point.X,
                    (float)normalized
                        .SurfaceHeightMeters,
                    (float)point.Z),
                (float)(
                    0.5 +
                    point.X /
                    (
                        extent *
                        2.0
                    )),
                (float)(
                    0.5 -
                    point.Z /
                    (
                        extent *
                        2.0
                    )));
        }

        for (
            var index = 0;
            index < outline.Count;
            index++)
        {
            var current =
                checked(
                    (uint)(
                        index +
                        1));

            var next =
                checked(
                    (uint)(
                        (
                            index +
                            1
                        ) %
                        outline.Count +
                        1));

            indices.Add(
                0);

            indices.Add(
                current);

            indices.Add(
                next);

            triangleMaterials.Add(
                0);
        }

        AppendStructuralJunctionSurfaces(
            normalized,
            outline,
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials);

        AppendSidewalkMouthSurfaces(
            normalized,
            extent,
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials);

        AppendLaneMouthMarkings(
            normalized,
            extent,
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials);

        return new OmsiO3dGeometry(
            true,
            null,
            positions.ToArray(),
            normals.ToArray(),
            uvs.ToArray(),
            indices.ToArray(),
            triangleMaterials.ToArray(),
            [
                new OmsiO3dMaterial(
                    0.30f,
                    0.31f,
                    0.32f,
                    1,
                    0.03f,
                    0.03f,
                    0.03f,
                    0,
                    0,
                    0,
                    8,
                    "ms_junction_asphalt.bmp"),
                new OmsiO3dMaterial(
                    1,
                    1,
                    1,
                    1,
                    0.05f,
                    0.05f,
                    0.05f,
                    0,
                    0,
                    0,
                    4,
                    "ms_junction_marking.bmp"),
                new OmsiO3dMaterial(
                    0.62f,
                    0.62f,
                    0.62f,
                    1,
                    0.02f,
                    0.02f,
                    0.02f,
                    0,
                    0,
                    0,
                    4,
                    "ms_junction_sidewalk.bmp")
            ]);
    }


    private static void AppendStructuralJunctionSurfaces(
        MapStudioJunctionSpec spec,
        IReadOnlyList<JunctionOutlinePoint> outline,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        switch (spec.StructureKind)
        {
            case MapStudioJunctionStructureKind.Bridge:
                var lowerHeight =
                    spec.SurfaceHeightMeters -
                    0.30;

                AppendHorizontalFootprint(
                    outline,
                    lowerHeight,
                    normalY:
                        -1,
                    reverseWinding:
                        true,
                    materialIndex:
                        2,
                    positions,
                    normals,
                    uvs,
                    indices,
                    triangleMaterials);

                AppendOutlineSkirt(
                    outline,
                    spec.SurfaceHeightMeters,
                    lowerHeight,
                    materialIndex:
                        2,
                    positions,
                    normals,
                    uvs,
                    indices,
                    triangleMaterials);

                break;

            case MapStudioJunctionStructureKind.Tunnel:
                AppendHorizontalFootprint(
                    outline,
                    spec.SurfaceHeightMeters +
                        4.20,
                    normalY:
                        -1,
                    reverseWinding:
                        true,
                    materialIndex:
                        2,
                    positions,
                    normals,
                    uvs,
                    indices,
                    triangleMaterials);

                break;
        }
    }

    private static void AppendHorizontalFootprint(
        IReadOnlyList<JunctionOutlinePoint> outline,
        double height,
        float normalY,
        bool reverseWinding,
        ushort materialIndex,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var baseIndex =
            checked(
                (uint)(
                    positions.Count /
                    3));

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                0,
                (float)height,
                0),
            new Vector3(
                0,
                normalY,
                0),
            0.5f,
            0.5f);

        foreach (var point in outline)
        {
            AddVertex(
                positions,
                normals,
                uvs,
                new Vector3(
                    (float)point.X,
                    (float)height,
                    (float)point.Z),
                new Vector3(
                    0,
                    normalY,
                    0),
                (float)(
                    0.5 +
                    point.X /
                    20.0),
                (float)(
                    0.5 -
                    point.Z /
                    20.0));
        }

        for (
            var index = 0;
            index < outline.Count;
            index++)
        {
            var current =
                baseIndex +
                checked(
                    (uint)(
                        index +
                        1));

            var next =
                baseIndex +
                checked(
                    (uint)(
                        (
                            index +
                            1
                        ) %
                        outline.Count +
                        1));

            indices.Add(
                baseIndex);

            indices.Add(
                reverseWinding
                    ? next
                    : current);

            indices.Add(
                reverseWinding
                    ? current
                    : next);

            triangleMaterials.Add(
                materialIndex);
        }
    }

    private static void AppendOutlineSkirt(
        IReadOnlyList<JunctionOutlinePoint> outline,
        double topHeight,
        double bottomHeight,
        ushort materialIndex,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        for (
            var index = 0;
            index < outline.Count;
            index++)
        {
            var current =
                outline[index];

            var next =
                outline[
                    (
                        index +
                        1
                    ) %
                    outline.Count];

            var midpoint =
                new Vector3(
                    (float)(
                        current.X +
                        next.X),
                    0,
                    (float)(
                        current.Z +
                        next.Z));

            var normal =
                midpoint.LengthSquared() >
                    0.000001f
                    ? Vector3.Normalize(
                        midpoint)
                    : Vector3.UnitX;

            var baseIndex =
                checked(
                    (uint)(
                        positions.Count /
                        3));

            AddVertex(
                positions,
                normals,
                uvs,
                new Vector3(
                    (float)current.X,
                    (float)topHeight,
                    (float)current.Z),
                normal,
                0,
                0);

            AddVertex(
                positions,
                normals,
                uvs,
                new Vector3(
                    (float)current.X,
                    (float)bottomHeight,
                    (float)current.Z),
                normal,
                0,
                1);

            AddVertex(
                positions,
                normals,
                uvs,
                new Vector3(
                    (float)next.X,
                    (float)bottomHeight,
                    (float)next.Z),
                normal,
                1,
                1);

            AddVertex(
                positions,
                normals,
                uvs,
                new Vector3(
                    (float)next.X,
                    (float)topHeight,
                    (float)next.Z),
                normal,
                1,
                0);

            indices.Add(
                baseIndex);
            indices.Add(
                baseIndex +
                1);
            indices.Add(
                baseIndex +
                2);

            triangleMaterials.Add(
                materialIndex);

            indices.Add(
                baseIndex);
            indices.Add(
                baseIndex +
                2);
            indices.Add(
                baseIndex +
                3);

            triangleMaterials.Add(
                materialIndex);
        }
    }

    private static void AppendSidewalkMouthSurfaces(
        MapStudioJunctionSpec spec,
        double extent,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var startDistance =
            Math.Clamp(
                extent *
                    0.42,
                1.25,
                Math.Max(
                    1.25,
                    extent -
                        0.25));

        var endDistance =
            Math.Max(
                startDistance +
                    0.10,
                extent -
                    0.03);

        foreach (
            var arm in spec.Arms)
        {
            var laneWidth =
                Math.Clamp(
                    arm.LaneWidthMeters,
                    2.0,
                    4.5);

            var carriagewayWidth =
                Math.Min(
                    arm.WidthMeters,
                    arm.LaneCount *
                        laneWidth);

            var sidewalkWidth =
                Math.Max(
                    0,
                    arm.WidthMeters -
                        carriagewayWidth) /
                2.0;

            if (
                sidewalkWidth <
                0.10)
            {
                continue;
            }

            var lateralCenter =
                carriagewayWidth /
                    2.0 +
                sidewalkWidth /
                    2.0;

            AppendSidewalkMouthQuad(
                arm.AngleDegrees,
                startDistance,
                endDistance,
                lateralCenter,
                sidewalkWidth,
                spec.SurfaceHeightMeters +
                    0.002,
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials);

            AppendSidewalkMouthQuad(
                arm.AngleDegrees,
                startDistance,
                endDistance,
                -lateralCenter,
                sidewalkWidth,
                spec.SurfaceHeightMeters +
                    0.002,
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials);
        }
    }

    private static void AppendSidewalkMouthQuad(
        double angleDegrees,
        double startDistance,
        double endDistance,
        double lateralCenter,
        double sidewalkWidth,
        double height,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var halfWidth =
            sidewalkWidth /
            2.0;

        var startLeft =
            PointOnArmLane(
                angleDegrees,
                startDistance,
                lateralCenter -
                    halfWidth);

        var startRight =
            PointOnArmLane(
                angleDegrees,
                startDistance,
                lateralCenter +
                    halfWidth);

        var endLeft =
            PointOnArmLane(
                angleDegrees,
                endDistance,
                lateralCenter -
                    halfWidth);

        var endRight =
            PointOnArmLane(
                angleDegrees,
                endDistance,
                lateralCenter +
                    halfWidth);

        var baseIndex =
            checked(
                (uint)(
                    positions.Count /
                    3));

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)startLeft.X,
                (float)height,
                (float)startLeft.Z),
            0,
            1);

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)startRight.X,
                (float)height,
                (float)startRight.Z),
            1,
            1);

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)endLeft.X,
                (float)height,
                (float)endLeft.Z),
            0,
            0);

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)endRight.X,
                (float)height,
                (float)endRight.Z),
            1,
            0);

        indices.Add(
            baseIndex);

        indices.Add(
            baseIndex +
            2);

        indices.Add(
            baseIndex +
            3);

        triangleMaterials.Add(
            2);

        indices.Add(
            baseIndex);

        indices.Add(
            baseIndex +
            3);

        indices.Add(
            baseIndex +
            1);

        triangleMaterials.Add(
            2);
    }

    private static void AppendLaneMouthMarkings(
        MapStudioJunctionSpec spec,
        double extent,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        const double markingWidth =
            0.12;

        var markingLength =
            Math.Clamp(
                extent *
                    0.45,
                1.25,
                3.5);

        var startDistance =
            Math.Max(
                0.25,
                extent -
                    markingLength);

        var endDistance =
            Math.Max(
                startDistance +
                    0.1,
                extent -
                    0.05);

        foreach (
            var arm in spec.Arms)
        {
            if (
                arm.LaneCount <=
                1)
            {
                continue;
            }

            var laneWidth =
                Math.Clamp(
                    arm.LaneWidthMeters,
                    2.0,
                    4.5);

            var roadWidth =
                arm.LaneCount *
                laneWidth;

            for (
                var laneBoundary = 1;
                laneBoundary <
                    arm.LaneCount;
                laneBoundary++)
            {
                var lateralOffset =
                    -roadWidth /
                        2.0 +
                    laneBoundary *
                        laneWidth;

                AppendLaneMouthMarkingQuad(
                    arm.AngleDegrees,
                    startDistance,
                    endDistance,
                    lateralOffset,
                    markingWidth,
                    spec.SurfaceHeightMeters +
                        0.004,
                    positions,
                    normals,
                    uvs,
                    indices,
                    triangleMaterials);
            }
        }
    }

    private static void AppendLaneMouthMarkingQuad(
        double angleDegrees,
        double startDistance,
        double endDistance,
        double lateralOffset,
        double markingWidth,
        double height,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var halfWidth =
            markingWidth /
            2.0;

        var startLeft =
            PointOnArmLane(
                angleDegrees,
                startDistance,
                lateralOffset -
                    halfWidth);

        var startRight =
            PointOnArmLane(
                angleDegrees,
                startDistance,
                lateralOffset +
                    halfWidth);

        var endLeft =
            PointOnArmLane(
                angleDegrees,
                endDistance,
                lateralOffset -
                    halfWidth);

        var endRight =
            PointOnArmLane(
                angleDegrees,
                endDistance,
                lateralOffset +
                    halfWidth);

        var baseIndex =
            checked(
                (uint)(
                    positions.Count /
                    3));

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)startLeft.X,
                (float)height,
                (float)startLeft.Z),
            0,
            1);

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)startRight.X,
                (float)height,
                (float)startRight.Z),
            1,
            1);

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)endLeft.X,
                (float)height,
                (float)endLeft.Z),
            0,
            0);

        AddVertex(
            positions,
            normals,
            uvs,
            new Vector3(
                (float)endRight.X,
                (float)height,
                (float)endRight.Z),
            1,
            0);

        indices.Add(
            baseIndex);

        indices.Add(
            baseIndex +
            2);

        indices.Add(
            baseIndex +
            3);

        triangleMaterials.Add(
            1);

        indices.Add(
            baseIndex);

        indices.Add(
            baseIndex +
            3);

        indices.Add(
            baseIndex +
            1);

        triangleMaterials.Add(
            1);
    }

    private static IReadOnlyList<
        JunctionOutlinePoint>
        BuildJunctionOutline(
            MapStudioJunctionSpec spec,
            double extent)
    {
        var points =
            new List<
                JunctionOutlinePoint>(
                    JunctionOutlineSampleCount);

        for (
            var index = 0;
            index <
                JunctionOutlineSampleCount;
            index++)
        {
            var angleDegrees =
                index *
                360.0 /
                JunctionOutlineSampleCount;

            var radius =
                ResolveJunctionOutlineRadius(
                    spec,
                    extent,
                    angleDegrees);

            var radians =
                angleDegrees *
                Math.PI /
                180.0;

            points.Add(
                new JunctionOutlinePoint(
                    Math.Sin(
                        radians) *
                    radius,
                    Math.Cos(
                        radians) *
                    radius));
        }

        return points;
    }

    private static double
        ResolveJunctionOutlineRadius(
            MapStudioJunctionSpec spec,
            double extent,
            double angleDegrees)
    {
        var radians =
            angleDegrees *
            Math.PI /
            180.0;

        var rayX =
            Math.Sin(
                radians);

        var rayZ =
            Math.Cos(
                radians);

        var minimumHalfWidth =
            spec.Arms
                .Min(
                    arm =>
                        arm.WidthMeters /
                        2.0);

        var maximumHalfWidth =
            spec.Arms
                .Max(
                    arm =>
                        arm.WidthMeters /
                        2.0);

        // A small central pad keeps unusual/clustered arm layouts
        // connected while the outer footprint still follows the
        // real union of each finite road mouth rectangle.
        var bestRadius =
            Math.Clamp(
                minimumHalfWidth *
                    0.75,
                0.75,
                4.0);

        foreach (
            var arm in spec.Arms)
        {
            var armRadians =
                arm.AngleDegrees *
                Math.PI /
                180.0;

            var forwardX =
                Math.Sin(
                    armRadians);

            var forwardZ =
                Math.Cos(
                    armRadians);

            var lateralX =
                Math.Cos(
                    armRadians);

            var lateralZ =
                -Math.Sin(
                    armRadians);

            var forwardProjection =
                rayX *
                    forwardX +
                rayZ *
                    forwardZ;

            if (
                forwardProjection <
                -0.0000001)
            {
                continue;
            }

            var lateralProjection =
                Math.Abs(
                    rayX *
                        lateralX +
                    rayZ *
                        lateralZ);

            var longitudinalLimit =
                forwardProjection >
                    0.0000001
                    ? extent /
                        forwardProjection
                    : double
                        .PositiveInfinity;

            var lateralLimit =
                lateralProjection >
                    0.0000001
                    ? (
                        arm.WidthMeters /
                        2.0
                    ) /
                    lateralProjection
                    : double
                        .PositiveInfinity;

            var armRadius =
                Math.Min(
                    longitudinalLimit,
                    lateralLimit);

            if (
                double.IsFinite(
                    armRadius) &&
                armRadius >
                    bestRadius)
            {
                bestRadius =
                    armRadius;
            }
        }

        var maximumRadius =
            Math.Sqrt(
                extent *
                    extent +
                maximumHalfWidth *
                    maximumHalfWidth);

        return Math.Clamp(
            bestRadius,
            0.25,
            maximumRadius);
    }

    private static List<JunctionPath>
        BuildInternalPaths(
            MapStudioJunctionSpec spec)
    {
        var radius =
            MapStudioJunctionGeometrySizing
                .ResolvePathRadiusMeters(
                    spec.Arms
                        .Select(
                            arm =>
                                arm.WidthMeters));

        var paths =
            new List<JunctionPath>();

        for (
            var fromIndex = 0;
            fromIndex <
                spec.Arms.Count;
            fromIndex++)
        {
            var from =
                spec.Arms[
                    fromIndex];

            var inboundOffsets =
                ResolveLaneOffsets(
                    from,
                    inbound:
                        true);

            if (
                inboundOffsets.Count ==
                0)
            {
                continue;
            }

            for (
                var toIndex = 0;
                toIndex <
                    spec.Arms.Count;
                toIndex++)
            {
                if (
                    fromIndex ==
                    toIndex)
                {
                    continue;
                }

                var to =
                    spec.Arms[
                        toIndex];

                var outboundOffsets =
                    ResolveLaneOffsets(
                        to,
                        inbound:
                            false);

                if (
                    outboundOffsets.Count ==
                    0)
                {
                    continue;
                }

                var laneWidth =
                    Math.Clamp(
                        Math.Min(
                            from.LaneWidthMeters,
                            to.LaneWidthMeters) -
                        0.3,
                        2.0,
                        4.2);

                var connectionCount =
                    Math.Max(
                        inboundOffsets.Count,
                        outboundOffsets.Count);

                for (
                    var connectionIndex = 0;
                    connectionIndex <
                        connectionCount;
                    connectionIndex++)
                {
                    var sourceLaneIndex =
                        ResolveLaneConnectionIndex(
                            connectionIndex,
                            connectionCount,
                            inboundOffsets.Count);

                    var targetLaneIndex =
                        ResolveLaneConnectionIndex(
                            connectionIndex,
                            connectionCount,
                            outboundOffsets.Count);

                    var fromPoint =
                        PointOnArmLane(
                            from.AngleDegrees,
                            radius,
                            inboundOffsets[
                                sourceLaneIndex]);

                    var toPoint =
                        PointOnArmLane(
                            to.AngleDegrees,
                            radius,
                            outboundOffsets[
                                targetLaneIndex]);

                    if (
                        Distance(
                            fromPoint,
                            toPoint) <
                        0.5)
                    {
                        continue;
                    }

                    AppendMovementPaths(
                        paths,
                        fromPoint,
                        toPoint,
                        from.AngleDegrees,
                        to.AngleDegrees,
                        laneWidth);
                }
            }
        }

        return paths;
    }

    private static IReadOnlyList<double>
        ResolveLaneOffsets(
            MapStudioJunctionArm arm,
            bool inbound)
    {
        var laneCount =
            inbound
                ? arm.InboundLaneCount
                : arm.OutboundLaneCount;

        if (
            laneCount <=
            0)
        {
            return Array.Empty<double>();
        }

        var oppositeLaneCount =
            inbound
                ? arm.OutboundLaneCount
                : arm.InboundLaneCount;

        var laneWidth =
            Math.Clamp(
                arm.LaneWidthMeters,
                2.0,
                4.5);

        var offsets =
            new double[
                laneCount];

        if (
            arm.OneWay ||
            oppositeLaneCount <=
                0)
        {
            var first =
                -(
                    laneCount -
                    1
                ) *
                laneWidth /
                2.0;

            for (
                var index = 0;
                index <
                    laneCount;
                index++)
            {
                offsets[
                    index] =
                    first +
                    index *
                    laneWidth;
            }

            return offsets;
        }

        var direction =
            inbound
                ? -1.0
                : 1.0;

        for (
            var index = 0;
            index <
                laneCount;
            index++)
        {
            offsets[
                index] =
                direction *
                (
                    index +
                    0.5
                ) *
                laneWidth;
        }

        return offsets;
    }

    private static int
        ResolveLaneConnectionIndex(
            int connectionIndex,
            int connectionCount,
            int laneCount)
    {
        if (
            laneCount <=
                1 ||
            connectionCount <=
                1)
        {
            return 0;
        }

        return Math.Clamp(
            (int)Math.Round(
                connectionIndex *
                (
                    laneCount -
                    1
                ) /
                (double)(
                    connectionCount -
                    1
                ),
                MidpointRounding
                    .AwayFromZero),
            0,
            laneCount -
                1);
    }

    private static (
        double X,
        double Z
    ) PointOnArmLane(
        double angleDegrees,
        double distance,
        double lateralOffset)
    {
        var radians =
            angleDegrees *
            Math.PI /
            180.0;

        var forwardX =
            Math.Sin(
                radians);

        var forwardZ =
            Math.Cos(
                radians);

        var lateralX =
            Math.Cos(
                radians);

        var lateralZ =
            -Math.Sin(
                radians);

        return
            (
                forwardX *
                    distance +
                lateralX *
                    lateralOffset,
                forwardZ *
                    distance +
                lateralZ *
                    lateralOffset
            );
    }

    private static double Distance(
        (
            double X,
            double Z
        ) first,
        (
            double X,
            double Z
        ) second)
    {
        var dx =
            second.X -
            first.X;

        var dz =
            second.Z -
            first.Z;

        return Math.Sqrt(
            dx * dx +
            dz * dz);
    }

    private static void AppendMovementPaths(
        ICollection<JunctionPath> paths,
        (
            double X,
            double Z
        ) start,
        (
            double X,
            double Z
        ) end,
        double fromArmAngleDegrees,
        double toArmAngleDegrees,
        double laneWidth)
    {
        var inboundHeading =
            NormalizeAngleDegrees(
                fromArmAngleDegrees +
                180.0);

        var outboundHeading =
            NormalizeAngleDegrees(
                toArmAngleDegrees);

        var turnDegrees =
            Math.Abs(
                NormalizeSignedAngleDegrees(
                    outboundHeading -
                    inboundHeading));

        var segmentCount =
            turnDegrees <=
                1.0
                ? 1
                : Math.Clamp(
                    (int)Math.Ceiling(
                        turnDegrees /
                        JunctionMovementDegreesPerSegment),
                    2,
                    JunctionMovementMaximumSegments);

        var control =
            ResolveMovementControlPoint(
                start,
                end,
                inboundHeading,
                outboundHeading);

        if (
            segmentCount ==
            1)
        {
            AppendStraightJunctionPath(
                paths,
                start,
                end,
                laneWidth);

            return;
        }

        var previous =
            start;

        for (
            var segmentIndex = 1;
            segmentIndex <=
                segmentCount;
            segmentIndex++)
        {
            var amount =
                segmentIndex /
                (double)segmentCount;

            // Quadratic Bezier using the intersection of the real
            // inbound/outbound lane tangents as the control point.
            // This preserves lane alignment at both road mouths.
            var oneMinus =
                1.0 -
                amount;

            var current =
                (
                    X:
                        oneMinus *
                            oneMinus *
                            start.X +
                        2.0 *
                            oneMinus *
                            amount *
                            control.X +
                        amount *
                            amount *
                            end.X,
                    Z:
                        oneMinus *
                            oneMinus *
                            start.Z +
                        2.0 *
                            oneMinus *
                            amount *
                            control.Z +
                        amount *
                            amount *
                            end.Z
                );

            AppendStraightJunctionPath(
                paths,
                previous,
                current,
                laneWidth);

            previous =
                current;
        }
    }

    private static (
        double X,
        double Z
    ) ResolveMovementControlPoint(
        (
            double X,
            double Z
        ) start,
        (
            double X,
            double Z
        ) end,
        double inboundHeadingDegrees,
        double outboundHeadingDegrees)
    {
        var inboundRadians =
            inboundHeadingDegrees *
            Math.PI /
            180.0;

        var outboundRadians =
            outboundHeadingDegrees *
            Math.PI /
            180.0;

        var inboundDirection =
            (
                X:
                    Math.Sin(
                        inboundRadians),
                Z:
                    Math.Cos(
                        inboundRadians)
            );

        var outboundDirection =
            (
                X:
                    Math.Sin(
                        outboundRadians),
                Z:
                    Math.Cos(
                        outboundRadians)
            );

        var denominator =
            Cross2D(
                inboundDirection,
                outboundDirection);

        if (
            Math.Abs(
                denominator) <
            0.000001)
        {
            return
                (
                    (
                        start.X +
                        end.X
                    ) /
                    2.0,
                    (
                        start.Z +
                        end.Z
                    ) /
                    2.0
                );
        }

        var between =
            (
                X:
                    end.X -
                    start.X,
                Z:
                    end.Z -
                    start.Z
            );

        var amount =
            Cross2D(
                between,
                outboundDirection) /
            denominator;

        return
            (
                start.X +
                    inboundDirection.X *
                    amount,
                start.Z +
                    inboundDirection.Z *
                    amount
            );
    }

    private static double Cross2D(
        (
            double X,
            double Z
        ) first,
        (
            double X,
            double Z
        ) second) =>
        first.X *
            second.Z -
        first.Z *
            second.X;

    private static void AppendStraightJunctionPath(
        ICollection<JunctionPath> paths,
        (
            double X,
            double Z
        ) start,
        (
            double X,
            double Z
        ) end,
        double laneWidth)
    {
        var dx =
            end.X -
            start.X;

        var dz =
            end.Z -
            start.Z;

        var length =
            Math.Sqrt(
                dx * dx +
                dz * dz);

        if (
            !double.IsFinite(
                length) ||
            length <
                0.05)
        {
            return;
        }

        var rotation =
            Math.Atan2(
                dx,
                dz) *
            180.0 /
            Math.PI;

        paths.Add(
            new JunctionPath(
                start.X,
                start.Z,
                rotation,
                length,
                laneWidth));
    }

    private static double
        NormalizeAngleDegrees(
            double angle)
    {
        angle %=
            360.0;

        if (
            angle <
            0)
        {
            angle +=
                360.0;
        }

        return angle;
    }

    private static double
        NormalizeSignedAngleDegrees(
            double angle)
    {
        angle %=
            360.0;

        if (
            angle >
            180.0)
        {
            angle -=
                360.0;
        }
        else if (
            angle <=
            -180.0)
        {
            angle +=
                360.0;
        }

        return angle;
    }

    private static string BuildSceneryObject(
        MapStudioJunctionSpec spec,
        IReadOnlyList<JunctionPath>
            paths)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "[friendlyname]");

        builder.AppendLine(
            spec.Name);

        builder.AppendLine(
            "[groups]");

        builder.AppendLine(
            "2");

        builder.AppendLine(
            "MapStudio");

        builder.AppendLine(
            "Procedural Junctions");

        builder.AppendLine(
            "[mesh]");

        builder.AppendLine(
            "junction.o3d");

        foreach (
            var path in paths)
        {
            builder.AppendLine(
                "[path]");

            builder.AppendLine(
                Format(
                    path.X));

            builder.AppendLine(
                "0");

            builder.AppendLine(
                Format(
                    path.Z));

            builder.AppendLine(
                Format(
                    path.Rotation));

            builder.AppendLine(
                "0");

            builder.AppendLine(
                Format(
                    path.Length));

            builder.AppendLine(
                "0");

            builder.AppendLine(
                "0");

            builder.AppendLine(
                "0");

            builder.AppendLine(
                Format(
                    path.Width));

            builder.AppendLine(
                "0");

            builder.AppendLine(
                "0");
        }

        return builder
            .ToString()
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);
    }

    private static string BuildManifest(
        MapStudioJunctionSpec spec,
        int pathCount) =>
        $"OMSI Map Studio Junction\n" +
        $"Name={spec.Name}\n" +
        $"Arms={spec.Arms.Count}\n" +
        $"Structure={spec.StructureKind}\n" +
        $"InternalPaths={pathCount}\n";

    private static (
        double X,
        double Z
    ) PointOnRay(
        double angleDegrees,
        double distance)
    {
        var radians =
            angleDegrees *
            Math.PI /
            180.0;

        return
            (
                Math.Sin(
                    radians) *
                distance,
                Math.Cos(
                    radians) *
                distance
            );
    }

    private static void AddVertex(
        ICollection<float> positions,
        ICollection<float> normals,
        ICollection<float> uvs,
        Vector3 position,
        float u,
        float v) =>
        AddVertex(
            positions,
            normals,
            uvs,
            position,
            Vector3.UnitY,
            u,
            v);

    private static void AddVertex(
        ICollection<float> positions,
        ICollection<float> normals,
        ICollection<float> uvs,
        Vector3 position,
        Vector3 normal,
        float u,
        float v)
    {
        positions.Add(
            position.X);

        positions.Add(
            position.Y);

        positions.Add(
            position.Z);

        normals.Add(
            normal.X);

        normals.Add(
            normal.Y);

        normals.Add(
            normal.Z);

        uvs.Add(
            u);

        uvs.Add(
            v);
    }

    private static string Format(
        double value) =>
        value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);

    private static string SanitizeName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var cleaned =
            new string(
                value
                    .Select(
                        character =>
                            invalid.Contains(
                                character) ||
                            char.IsControl(
                                character)
                                ? '_'
                                : character)
                    .ToArray())
                .Trim();

        cleaned =
            string.Join(
                "_",
                cleaned.Split(
                    ' ',
                    StringSplitOptions
                        .RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(
            cleaned)
            ? "Junction"
            : cleaned;
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        Directory.CreateDirectory(
            destination);

        foreach (
            var directory in
                Directory.EnumerateDirectories(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    Path.GetRelativePath(
                        source,
                        directory)));
        }

        foreach (
            var file in
                Directory.EnumerateFiles(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            var target =
                Path.Combine(
                    destination,
                    Path.GetRelativePath(
                        source,
                        file));

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    target)!);

            File.Copy(
                file,
                target,
                overwrite:
                    true);
        }
    }

    private readonly record struct JunctionOutlinePoint(
        double X,
        double Z);

    private sealed record JunctionPath(
        double X,
        double Z,
        double Rotation,
        double Length,
        double Width);
}
