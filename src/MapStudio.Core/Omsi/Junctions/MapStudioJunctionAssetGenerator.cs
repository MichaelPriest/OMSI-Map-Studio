using System.Globalization;
using System.Numerics;
using System.Text;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Core.Omsi.Junctions;

public sealed record MapStudioJunctionArm(
    double AngleDegrees,
    double WidthMeters,
    int LaneCount = 2,
    bool OneWay = false,
    double LaneWidthMeters = 3.5);

public sealed record MapStudioJunctionSpec(
    string Name,
    IReadOnlyList<MapStudioJunctionArm> Arms,
    double SurfaceHeightMeters = 0.08)
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
                                    4.5)
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
                        : 0.08,
                    0,
                    1)
        };
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

            await MapStudioGeneratedTextureFactory
                .EnsureBmpAsync(
                    textureDirectory,
                    "ms_junction_asphalt.bmp",
                    128,
                    128,
                    static (x, y) =>
                    {
                        var aggregate =
                            (
                                x * 19 +
                                y * 23 +
                                (
                                    x ^
                                    y
                                )
                            ) %
                            21;

                        var seam =
                            x %
                                32 ==
                            0 ||
                            y %
                                32 ==
                            0;

                        var value =
                            (byte)(
                                seam
                                    ? 46
                                    : 58 +
                                        aggregate);

                        return new MapStudioGeneratedRgb(
                            value,
                            value,
                            (byte)Math.Min(
                                255,
                                value +
                                2));
                    },
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

        var centerX =
            outline.Average(
                point =>
                    point.X);

        var centerZ =
            outline.Average(
                point =>
                    point.Z);

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
                    "ms_junction_asphalt.bmp")
            ]);
    }

    private static IReadOnlyList<
        JunctionOutlinePoint>
        BuildJunctionOutline(
            MapStudioJunctionSpec spec,
            double extent)
    {
        var candidates =
            new List<
                JunctionOutlinePoint>(
                    spec.Arms.Count *
                    2);

        foreach (
            var arm in
                spec.Arms)
        {
            var radians =
                arm.AngleDegrees *
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

            var halfWidth =
                arm.WidthMeters /
                2.0;

            candidates.Add(
                new JunctionOutlinePoint(
                    forwardX *
                        extent +
                    lateralX *
                        halfWidth,
                    forwardZ *
                        extent +
                    lateralZ *
                        halfWidth));

            candidates.Add(
                new JunctionOutlinePoint(
                    forwardX *
                        extent -
                    lateralX *
                        halfWidth,
                    forwardZ *
                        extent -
                    lateralZ *
                        halfWidth));
        }

        return ConvexHull(
            candidates);
    }

    private static IReadOnlyList<
        JunctionOutlinePoint>
        ConvexHull(
            IEnumerable<
                JunctionOutlinePoint>
                points)
    {
        var ordered =
            points
                .Distinct()
                .OrderBy(
                    point =>
                        point.X)
                .ThenBy(
                    point =>
                        point.Z)
                .ToArray();

        if (ordered.Length <= 3)
        {
            return ordered;
        }

        var lower =
            new List<
                JunctionOutlinePoint>();

        foreach (
            var point in
                ordered)
        {
            while (
                lower.Count >= 2 &&
                Cross(
                    lower[^2],
                    lower[^1],
                    point) <=
                0)
            {
                lower.RemoveAt(
                    lower.Count -
                    1);
            }

            lower.Add(
                point);
        }

        var upper =
            new List<
                JunctionOutlinePoint>();

        for (
            var index =
                ordered.Length -
                1;
            index >= 0;
            index--)
        {
            var point =
                ordered[index];

            while (
                upper.Count >= 2 &&
                Cross(
                    upper[^2],
                    upper[^1],
                    point) <=
                0)
            {
                upper.RemoveAt(
                    upper.Count -
                    1);
            }

            upper.Add(
                point);
        }

        lower.RemoveAt(
            lower.Count -
            1);

        upper.RemoveAt(
            upper.Count -
            1);

        lower.AddRange(
            upper);

        return lower;
    }

    private static double Cross(
        JunctionOutlinePoint origin,
        JunctionOutlinePoint a,
        JunctionOutlinePoint b) =>
        (
            a.X -
            origin.X
        ) *
        (
            b.Z -
            origin.Z
        ) -
        (
            a.Z -
            origin.Z
        ) *
        (
            b.X -
            origin.X
        );

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

            if (from.OneWay)
            {
                continue;
            }

            var fromPoint =
                PointOnRay(
                    from.AngleDegrees,
                    radius);

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

                var toPoint =
                    PointOnRay(
                        to.AngleDegrees,
                        radius);

                var dx =
                    toPoint.X -
                    fromPoint.X;

                var dz =
                    toPoint.Z -
                    fromPoint.Z;

                var length =
                    Math.Sqrt(
                        dx * dx +
                        dz * dz);

                if (length < 0.5)
                {
                    continue;
                }

                var rotation =
                    Math.Atan2(
                        dx,
                        dz) *
                    180.0 /
                    Math.PI;

                var laneWidth =
                    Math.Clamp(
                        Math.Min(
                            from.LaneWidthMeters,
                            to.LaneWidthMeters) -
                        0.3,
                        2.0,
                        4.2);

                paths.Add(
                    new JunctionPath(
                        fromPoint.X,
                        fromPoint.Z,
                        rotation,
                        length,
                        laneWidth));
            }
        }

        return paths;
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
            @"model\junction.o3d");

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
        float v)
    {
        positions.Add(
            position.X);

        positions.Add(
            position.Y);

        positions.Add(
            position.Z);

        normals.Add(
            0);

        normals.Add(
            1);

        normals.Add(
            0);

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
