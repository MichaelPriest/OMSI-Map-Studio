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
        "1.0.0";

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
                                64,
                                64,
                                static (x, y) =>
                                {
                                    var noise =
                                        (
                                            x * 17 +
                                            y * 31
                                        ) %
                                        13;

                                    var value =
                                        (byte)(
                                            58 +
                                            noise);

                                    return new Rgb(
                                        value,
                                        value,
                                        (byte)(
                                            value +
                                            2));
                                })
                    ),
                    (
                        Name:
                            "ms_sidewalk.bmp",
                        Data:
                            CreateTexture(
                                64,
                                64,
                                static (x, y) =>
                                {
                                    var joint =
                                        x %
                                            16 ==
                                        0 ||
                                        y %
                                            16 ==
                                        0;

                                    var value =
                                        (byte)(
                                            joint
                                                ? 124
                                                : 162);

                                    return new Rgb(
                                        value,
                                        value,
                                        (byte)(
                                            value -
                                            4));
                                })
                    ),
                    (
                        Name:
                            "ms_marking.bmp",
                        Data:
                            CreateTexture(
                                8,
                                8,
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
        CreateVariants() =>
        [
            RoadVariant.CreateRoad(
                "ms_road_oneway_3_5m.sli",
                laneCount: 1,
                laneWidth: 3.5,
                sidewalkWidth: 0,
                oneWay: true),
            RoadVariant.CreateRoad(
                "ms_road_2lane_7m.sli",
                laneCount: 2,
                laneWidth: 3.5,
                sidewalkWidth: 0),
            RoadVariant.CreateRoad(
                "ms_road_2lane_7m_sidewalk.sli",
                laneCount: 2,
                laneWidth: 3.5,
                sidewalkWidth: 2.0),
            RoadVariant.CreateRoad(
                "ms_avenue_4lane_14m_sidewalk.sli",
                laneCount: 4,
                laneWidth: 3.5,
                sidewalkWidth: 2.0),
            RoadVariant.CreateDividedRoad(
                "ms_avenue_divided_4lane.sli",
                lanesPerDirection: 2,
                laneWidth: 3.5,
                medianWidth: 2.0,
                sidewalkWidth: 2.0),
            RoadVariant.CreatePedestrian(
                "ms_pedestrian_3m.sli",
                width: 3.0)
        ];

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

    private sealed record RoadVariant(
        string FileName,
        IReadOnlyList<RoadSurface> Surfaces,
        IReadOnlyList<RoadPath> Paths)
    {
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
                paths);
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
                paths);
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
                ]);
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
