using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Splines;

public sealed record MapStudioRoadKitInstallResult(
    string PackDirectory,
    string? BackupDirectory,
    IReadOnlyList<string> SplineRelativePaths,
    IReadOnlyList<string> TexturePaths);

public sealed class MapStudioRoadKitGenerator
{
    public const string PackFolderName =
        "MapStudio_RoadKit";

    public const string PackVersion =
        "1.3.0";

    private static readonly Encoding
        SplineEncoding =
            Encoding.ASCII;

    public async Task<MapStudioRoadKitInstallResult>
        InstallOrUpdateAsync(
            string omsiRoot,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        var root =
            Path.GetFullPath(
                omsiRoot);

        Directory.CreateDirectory(
            root);

        var splinesRoot =
            Path.Combine(
                root,
                "Splines");

        Directory.CreateDirectory(
            splinesRoot);

        var packDirectory =
            Path.Combine(
                splinesRoot,
                PackFolderName);

        string? backupDirectory =
            null;

        if (
            Directory.Exists(
                packDirectory) &&
            Directory.EnumerateFileSystemEntries(
                    packDirectory)
                .Any())
        {
            backupDirectory =
                Path.Combine(
                    root,
                    ".mapstudio",
                    "backups",
                    "roadkit",
                    DateTime.UtcNow
                        .ToString(
                            "yyyyMMdd-HHmmss",
                            CultureInfo
                                .InvariantCulture) +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N")[..8]);

            CopyDirectory(
                packDirectory,
                backupDirectory);
        }

        var temporaryDirectory =
            Path.Combine(
                splinesRoot,
                PackFolderName +
                ".tmp-" +
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            temporaryDirectory);

        try
        {
            var textureDirectory =
                Path.Combine(
                    temporaryDirectory,
                    "Texture");

            Directory.CreateDirectory(
                textureDirectory);

            var textureFiles =
                new[]
                {
                    (
                        Name:
                            "ms_asphalt.bmp",
                        Data:
                            CreateTexture(
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

                                    var aggregate =
                                        (
                                            x *
                                                37 +
                                            y *
                                                61
                                        ) %
                                            113 ==
                                        0
                                            ? 16
                                            : 0;

                                    var wear =
                                        x is
                                            > 48 and < 76 or
                                            > 178 and < 206
                                            ? -5
                                            : 0;

                                    var value =
                                        Math.Clamp(
                                            48 +
                                            fine /
                                                2 +
                                            coarse /
                                                3 +
                                            aggregate +
                                            wear,
                                            32,
                                            82);

                                    return new Rgb(
                                        (byte)value,
                                        (byte)value,
                                        (byte)Math.Min(
                                            255,
                                            value +
                                                3));
                                })
                    ),
                    (
                        Name:
                            "ms_sidewalk.bmp",
                        Data:
                            CreateTexture(
                                256,
                                256,
                                static (x, y) =>
                                {
                                    var shiftedX =
                                        (
                                            x +
                                            (
                                                y /
                                                32
                                            ) %
                                            2 *
                                            16
                                        ) %
                                        32;

                                    var joint =
                                        shiftedX <
                                            2 ||
                                        y %
                                            32 <
                                            2;

                                    var grain =
                                        Math.Abs(
                                            (
                                                x *
                                                    17 +
                                                y *
                                                    31
                                            ) %
                                            13);

                                    var value =
                                        joint
                                            ? 104 +
                                              grain /
                                                  3
                                            : 150 +
                                              grain;

                                    return new Rgb(
                                        (byte)Math.Clamp(
                                            value,
                                            0,
                                            255),
                                        (byte)Math.Clamp(
                                            value,
                                            0,
                                            255),
                                        (byte)Math.Clamp(
                                            value -
                                                6,
                                            0,
                                            255));
                                })
                    ),
                    (
                        Name:
                            "ms_marking.bmp",
                        Data:
                            CreateTexture(
                                64,
                                64,
                                static (_, _) =>
                                    new Rgb(
                                        238,
                                        236,
                                        218))
                    ),
                    (
                        Name:
                            "ms_median.bmp",
                        Data:
                            CreateTexture(
                                64,
                                64,
                                static (x, y) =>
                                {
                                    var noise =
                                        (
                                            x * 11 +
                                            y * 7
                                        ) %
                                        17;

                                    return new Rgb(
                                        (byte)(
                                            74 +
                                            noise /
                                            3),
                                        (byte)(
                                            112 +
                                            noise),
                                        (byte)(
                                            67 +
                                            noise /
                                            2));
                                })
                    )
                };

            foreach (
                var texture in
                    textureFiles)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                await File
                    .WriteAllBytesAsync(
                        Path.Combine(
                            textureDirectory,
                            texture.Name),
                        texture.Data,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var variants =
                CreateVariants();

            foreach (
                var variant in
                    variants)
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                var source =
                    BuildSpline(
                        variant);

                await File.WriteAllTextAsync(
                        Path.Combine(
                            temporaryDirectory,
                            variant.FileName),
                        source,
                        SplineEncoding,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            await File.WriteAllTextAsync(
                    Path.Combine(
                        temporaryDirectory,
                        "mapstudio-roadkit.txt"),
                    BuildManifest(
                        variants),
                    SplineEncoding,
                    cancellationToken)
                .ConfigureAwait(false);

            if (
                Directory.Exists(
                    packDirectory))
            {
                Directory.Delete(
                    packDirectory,
                    recursive: true);
            }

            Directory.Move(
                temporaryDirectory,
                packDirectory);

            temporaryDirectory =
                string.Empty;

            return new MapStudioRoadKitInstallResult(
                packDirectory,
                backupDirectory,
                variants
                    .Select(
                        variant =>
                            @"Splines\" +
                            PackFolderName +
                            @"\" +
                            variant.FileName)
                    .ToArray(),
                textureFiles
                    .Select(
                        texture =>
                            Path.Combine(
                                packDirectory,
                                "Texture",
                                texture.Name))
                    .ToArray());
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

    private static IReadOnlyList<RoadVariant>
        CreateVariants()
    {
        var variants =
            new List<RoadVariant>();

        foreach (
            var profile in
                MapStudioStandardRoadCatalog
                    .Profiles)
        {
            var ground =
                CreateGroundVariant(
                    profile);

            variants.Add(
                ground);

            if (profile.IsPedestrian)
            {
                continue;
            }

            variants.Add(
                ground.WithStructure(
                    MapStudioStandardRoadCatalog
                        .GetBridgeFileName(
                            profile),
                    RoadStructureKind
                        .Bridge));

            variants.Add(
                ground.WithStructure(
                    MapStudioStandardRoadCatalog
                        .GetTunnelFileName(
                            profile),
                    RoadStructureKind
                        .Tunnel));
        }

        return variants;
    }

    private static RoadVariant CreateGroundVariant(
        MapStudioStandardRoadProfile profile)
    {
        if (profile.IsPedestrian)
        {
            return RoadVariant
                .CreatePedestrian(
                    profile.FileName,
                    profile
                        .CarriagewayWidthMeters);
        }

        if (profile.IsDivided)
        {
            return RoadVariant
                .CreateDividedRoad(
                    profile.FileName,
                    profile
                        .LanesPerDirection,
                    profile
                        .LaneWidthMeters,
                    profile
                        .MedianWidthMeters,
                    profile
                        .SidewalkWidthMeters);
        }

        return RoadVariant
            .CreateRoad(
                profile.FileName,
                profile.LaneCount,
                profile
                    .LaneWidthMeters,
                profile
                    .SidewalkWidthMeters,
                profile.OneWay);
    }

    private static string BuildSpline(
        RoadVariant variant)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "[texture]");
        builder.AppendLine(
            "ms_asphalt.bmp");
        builder.AppendLine(
            "[texture]");
        builder.AppendLine(
            "ms_sidewalk.bmp");
        builder.AppendLine(
            "[texture]");
        builder.AppendLine(
            "ms_marking.bmp");
        builder.AppendLine(
            "[texture]");
        builder.AppendLine(
            "ms_median.bmp");

        foreach (
            var surface in
                variant.Surfaces)
        {
            AppendProfile(
                builder,
                surface.TextureIndex,
                surface.Left,
                surface.Right,
                surface.Height,
                surface.Stretch);
        }

        foreach (
            var segment in
                variant.StructureSegments)
        {
            AppendProfileSegment(
                builder,
                segment.TextureIndex,
                segment.Left,
                segment.LeftHeight,
                segment.Right,
                segment.RightHeight,
                segment.Stretch);
        }

        foreach (
            var path in
                variant.Paths)
        {
            builder.AppendLine(
                "[path]");
            builder.AppendLine(
                path.Type.ToString(
                    CultureInfo
                        .InvariantCulture));
            builder.AppendLine(
                Format(
                    path.X));
            builder.AppendLine(
                Format(
                    path.Z));
            builder.AppendLine(
                Format(
                    path.Width));
            builder.AppendLine(
                path.Direction.ToString(
                    CultureInfo
                        .InvariantCulture));
        }

        return builder
            .ToString()
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);
    }

    private static void AppendProfile(
        StringBuilder builder,
        int textureIndex,
        double left,
        double right,
        double height,
        double stretch)
    {
        builder.AppendLine(
            "[profile]");
        builder.AppendLine(
            textureIndex.ToString(
                CultureInfo
                    .InvariantCulture));
        builder.AppendLine(
            "[profilepnt]");
        builder.AppendLine(
            Format(left));
        builder.AppendLine(
            Format(height));
        builder.AppendLine(
            "0.005");
        builder.AppendLine(
            Format(stretch));
        builder.AppendLine(
            "[profilepnt]");
        builder.AppendLine(
            Format(right));
        builder.AppendLine(
            Format(height));
        builder.AppendLine(
            "0.995");
        builder.AppendLine(
            Format(stretch));
    }

    private static void AppendProfileSegment(
        StringBuilder builder,
        int textureIndex,
        double left,
        double leftHeight,
        double right,
        double rightHeight,
        double stretch)
    {
        builder.AppendLine(
            "[profile]");
        builder.AppendLine(
            textureIndex.ToString(
                CultureInfo
                    .InvariantCulture));
        builder.AppendLine(
            "[profilepnt]");
        builder.AppendLine(
            Format(
                left));
        builder.AppendLine(
            Format(
                leftHeight));
        builder.AppendLine(
            "0.005");
        builder.AppendLine(
            Format(
                stretch));
        builder.AppendLine(
            "[profilepnt]");
        builder.AppendLine(
            Format(
                right));
        builder.AppendLine(
            Format(
                rightHeight));
        builder.AppendLine(
            "0.995");
        builder.AppendLine(
            Format(
                stretch));
    }

    private static string BuildManifest(
        IReadOnlyList<RoadVariant> variants)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "OMSI Map Studio Road Kit");
        builder.AppendLine(
            "Version=" +
            PackVersion);
        builder.AppendLine(
            "Generated=procedural");
        builder.AppendLine(
            "License=Original Map Studio content");
        builder.AppendLine(
            "Files:");

        foreach (
            var variant in
                variants)
        {
            builder.AppendLine(
                variant.FileName);
        }

        return builder
            .ToString()
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);
    }

    private static string Format(
        double value) =>
        value.ToString(
            "0.###",
            CultureInfo
                .InvariantCulture);

    private static byte[] CreateTexture(
        int width,
        int height,
        Func<int, int, Rgb> pixel)
    {
        var rowStride =
            (
                width *
                3 +
                3
            ) &
            ~3;

        var pixelBytes =
            rowStride *
            height;

        var data =
            new byte[
                54 +
                pixelBytes];

        data[0] =
            (byte)'B';
        data[1] =
            (byte)'M';

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    2,
                    4),
                data.Length);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    10,
                    4),
                54);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    14,
                    4),
                40);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    18,
                    4),
                width);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    22,
                    4),
                height);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                data.AsSpan(
                    26,
                    2),
                1);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                data.AsSpan(
                    28,
                    2),
                24);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    34,
                    4),
                pixelBytes);

        for (
            var y = 0;
            y < height;
            y++)
        {
            var targetY =
                height -
                1 -
                y;

            var rowOffset =
                54 +
                targetY *
                rowStride;

            for (
                var x = 0;
                x < width;
                x++)
            {
                var color =
                    pixel(
                        x,
                        y);

                var offset =
                    rowOffset +
                    x *
                    3;

                data[offset] =
                    color.B;
                data[offset + 1] =
                    color.G;
                data[offset + 2] =
                    color.R;
            }
        }

        return data;
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
            var relative =
                Path.GetRelativePath(
                    source,
                    directory);

            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    relative));
        }

        foreach (
            var file in
                Directory.EnumerateFiles(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            var relative =
                Path.GetRelativePath(
                    source,
                    file);

            var target =
                Path.Combine(
                    destination,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    target)!);

            File.Copy(
                file,
                target,
                overwrite: true);
        }
    }

    private readonly record struct Rgb(
        byte R,
        byte G,
        byte B);

    private sealed record RoadSurface(
        int TextureIndex,
        double Left,
        double Right,
        double Height,
        double Stretch);

    private sealed record RoadPath(
        int Type,
        double X,
        double Z,
        double Width,
        int Direction);

    private sealed record RoadProfileSegment(
        int TextureIndex,
        double Left,
        double LeftHeight,
        double Right,
        double RightHeight,
        double Stretch);

    private enum RoadStructureKind
    {
        Bridge,
        Tunnel
    }

    private sealed record RoadVariant(
        string FileName,
        IReadOnlyList<RoadSurface> Surfaces,
        IReadOnlyList<RoadPath> Paths,
        IReadOnlyList<RoadProfileSegment>
            StructureSegments)
    {
        public RoadVariant WithStructure(
            string fileName,
            RoadStructureKind kind)
        {
            var minimumX =
                Surfaces.Min(
                    surface =>
                        Math.Min(
                            surface.Left,
                            surface.Right));

            var maximumX =
                Surfaces.Max(
                    surface =>
                        Math.Max(
                            surface.Left,
                            surface.Right));

            var roadHeight =
                Surfaces
                    .Where(
                        surface =>
                            surface.TextureIndex ==
                                0)
                    .Select(
                        surface =>
                            surface.Height)
                    .DefaultIfEmpty(
                        0.10)
                    .Max();

            IReadOnlyList<RoadProfileSegment>
                structure =
                    kind switch
                    {
                        RoadStructureKind.Bridge =>
                        [
                            new(
                                1,
                                minimumX,
                                roadHeight -
                                    0.30,
                                maximumX,
                                roadHeight -
                                    0.30,
                                0.25),
                            new(
                                1,
                                minimumX,
                                roadHeight,
                                minimumX,
                                roadHeight +
                                    1.10,
                                0.20),
                            new(
                                1,
                                maximumX,
                                roadHeight,
                                maximumX,
                                roadHeight +
                                    1.10,
                                0.20)
                        ],
                        RoadStructureKind.Tunnel =>
                        [
                            new(
                                1,
                                minimumX,
                                roadHeight,
                                minimumX,
                                roadHeight +
                                    4.20,
                                0.25),
                            new(
                                1,
                                minimumX,
                                roadHeight +
                                    4.20,
                                maximumX,
                                roadHeight +
                                    4.20,
                                0.25),
                            new(
                                1,
                                maximumX,
                                roadHeight +
                                    4.20,
                                maximumX,
                                roadHeight,
                                0.25)
                        ],
                        _ =>
                            []
                    };

            return this with
            {
                FileName =
                    fileName,
                StructureSegments =
                    structure
            };
        }

        public static RoadVariant CreateRoad(
            string fileName,
            int laneCount,
            double laneWidth,
            double sidewalkWidth,
            bool oneWay = false)
        {
            var roadWidth =
                laneCount *
                laneWidth;

            var roadHalf =
                roadWidth /
                2.0;

            var surfaces =
                new List<RoadSurface>
                {
                    new(
                        0,
                        -roadHalf,
                        roadHalf,
                        0.10,
                        0.20)
                };

            if (
                sidewalkWidth >
                0)
            {
                surfaces.Add(
                    new RoadSurface(
                        1,
                        -roadHalf -
                        sidewalkWidth,
                        -roadHalf,
                        0.25,
                        0.25));

                surfaces.Add(
                    new RoadSurface(
                        1,
                        roadHalf,
                        roadHalf +
                        sidewalkWidth,
                        0.25,
                        0.25));
            }

            for (
                var lane = 1;
                lane < laneCount;
                lane++)
            {
                var x =
                    -roadHalf +
                    lane *
                    laneWidth;

                surfaces.Add(
                    new RoadSurface(
                        2,
                        x -
                        0.06,
                        x +
                        0.06,
                        0.105,
                        1.0));
            }

            var paths =
                new List<RoadPath>();

            for (
                var lane = 0;
                lane < laneCount;
                lane++)
            {
                var center =
                    -roadHalf +
                    laneWidth *
                    (
                        lane +
                        0.5
                    );

                var direction =
                    oneWay
                        ? 0
                        : center >=
                            0
                            ? 0
                            : 1;

                paths.Add(
                    new RoadPath(
                        0,
                        center,
                        0.10,
                        Math.Max(
                            2.5,
                            laneWidth -
                            0.3),
                        direction));
            }

            AddSidewalkPaths(
                paths,
                roadHalf,
                sidewalkWidth);

            return new RoadVariant(
                fileName,
                surfaces,
                paths,
                []);
        }

        public static RoadVariant
            CreateDividedRoad(
                string fileName,
                int lanesPerDirection,
                double laneWidth,
                double medianWidth,
                double sidewalkWidth)
        {
            var carriagewayWidth =
                lanesPerDirection *
                laneWidth;

            var halfMedian =
                medianWidth /
                2.0;

            var outside =
                halfMedian +
                carriagewayWidth;

            var surfaces =
                new List<RoadSurface>
                {
                    new(
                        0,
                        -outside,
                        -halfMedian,
                        0.10,
                        0.20),
                    new(
                        0,
                        halfMedian,
                        outside,
                        0.10,
                        0.20),
                    new(
                        3,
                        -halfMedian,
                        halfMedian,
                        0.25,
                        0.25)
                };

            if (
                sidewalkWidth >
                0)
            {
                surfaces.Add(
                    new RoadSurface(
                        1,
                        -outside -
                        sidewalkWidth,
                        -outside,
                        0.25,
                        0.25));

                surfaces.Add(
                    new RoadSurface(
                        1,
                        outside,
                        outside +
                        sidewalkWidth,
                        0.25,
                        0.25));
            }

            var paths =
                new List<RoadPath>();

            for (
                var lane = 0;
                lane < lanesPerDirection;
                lane++)
            {
                var offset =
                    halfMedian +
                    laneWidth *
                    (
                        lane +
                        0.5
                    );

                paths.Add(
                    new RoadPath(
                        0,
                        offset,
                        0.10,
                        laneWidth -
                        0.3,
                        0));

                paths.Add(
                    new RoadPath(
                        0,
                        -offset,
                        0.10,
                        laneWidth -
                        0.3,
                        1));

                if (
                    lane >
                    0)
                {
                    var separator =
                        halfMedian +
                        lane *
                        laneWidth;

                    surfaces.Add(
                        new RoadSurface(
                            2,
                            separator -
                            0.06,
                            separator +
                            0.06,
                            0.105,
                            1.0));

                    surfaces.Add(
                        new RoadSurface(
                            2,
                            -separator -
                            0.06,
                            -separator +
                            0.06,
                            0.105,
                            1.0));
                }
            }

            AddSidewalkPaths(
                paths,
                outside,
                sidewalkWidth);

            return new RoadVariant(
                fileName,
                surfaces,
                paths,
                []);
        }

        public static RoadVariant
            CreatePedestrian(
                string fileName,
                double width)
        {
            var half =
                width /
                2.0;

            return new RoadVariant(
                fileName,
                [
                    new RoadSurface(
                        1,
                        -half,
                        half,
                        0.20,
                        0.25)
                ],
                [
                    new RoadPath(
                        1,
                        0,
                        0.20,
                        width *
                        0.9,
                        2)
                ],
                []);
        }

        private static void AddSidewalkPaths(
            ICollection<RoadPath> paths,
            double roadHalf,
            double sidewalkWidth)
        {
            if (
                sidewalkWidth <=
                0)
            {
                return;
            }

            var center =
                roadHalf +
                sidewalkWidth /
                2.0;

            var width =
                Math.Max(
                    0.8,
                    sidewalkWidth -
                    0.2);

            paths.Add(
                new RoadPath(
                    1,
                    -center,
                    0.25,
                    width,
                    2));

            paths.Add(
                new RoadPath(
                    1,
                    center,
                    0.25,
                    width,
                    2));
        }
    }
}
