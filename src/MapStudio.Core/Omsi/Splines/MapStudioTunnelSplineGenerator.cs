using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Splines;

public sealed record MapStudioTunnelSpec(
    string Name,
    int LaneCount,
    double LaneWidthMeters,
    double InnerHeightMeters,
    double ShoulderWidthMeters,
    int ArchSegments,
    bool OneWay = false)
{
    public MapStudioTunnelSpec Normalize()
    {
        var name =
            string.IsNullOrWhiteSpace(
                Name)
                ? "Tunnel"
                : Name.Trim();

        var laneCount =
            Math.Clamp(
                LaneCount,
                1,
                8);

        var laneWidth =
            Math.Clamp(
                double.IsFinite(
                    LaneWidthMeters)
                    ? LaneWidthMeters
                    : 3.5,
                2.5,
                6.0);

        var shoulder =
            Math.Clamp(
                double.IsFinite(
                    ShoulderWidthMeters)
                    ? ShoulderWidthMeters
                    : 0.75,
                0,
                5.0);

        var roadHalf =
            laneCount *
            laneWidth /
            2.0;

        var innerHalf =
            roadHalf +
            shoulder;

        var minimumHeight =
            Math.Max(
                3.5,
                innerHalf *
                    0.65);

        var innerHeight =
            Math.Clamp(
                double.IsFinite(
                    InnerHeightMeters)
                    ? InnerHeightMeters
                    : 5.2,
                minimumHeight,
                15.0);

        return this with
        {
            Name = name,
            LaneCount = laneCount,
            LaneWidthMeters = laneWidth,
            InnerHeightMeters =
                innerHeight,
            ShoulderWidthMeters =
                shoulder,
            ArchSegments =
                Math.Clamp(
                    ArchSegments,
                    4,
                    24)
        };
    }
}

public sealed record MapStudioTunnelSplineResult(
    string PackDirectory,
    string SplinePath,
    string RelativeSplinePath,
    string? BackupDirectory,
    IReadOnlyList<string> TexturePaths);

public sealed class MapStudioTunnelSplineGenerator
{
    public const string PackFolderName =
        "MapStudio_Tunnels";

    private static readonly Encoding
        SplineEncoding =
            Encoding.ASCII;

    public async Task<MapStudioTunnelSplineResult>
        GenerateAsync(
            string omsiRoot,
            MapStudioTunnelSpec spec,
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

        var packDirectory =
            Path.Combine(
                root,
                "Splines",
                PackFolderName);

        var textureDirectory =
            Path.Combine(
                packDirectory,
                "Texture");

        Directory.CreateDirectory(
            textureDirectory);

        var texturePaths =
            await EnsureTexturesAsync(
                    textureDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

        var fileStem =
            "ms_tunnel_" +
            SanitizeFileStem(
                normalized.Name);

        var splinePath =
            Path.Combine(
                packDirectory,
                fileStem +
                ".sli");

        string? backupDirectory =
            null;

        if (File.Exists(
                splinePath))
        {
            backupDirectory =
                Path.Combine(
                    root,
                    ".mapstudio",
                    "backups",
                    "tunnels",
                    DateTime.UtcNow
                        .ToString(
                            "yyyyMMdd-HHmmss",
                            CultureInfo
                                .InvariantCulture) +
                    "-" +
                    Guid.NewGuid()
                        .ToString("N")[..8]);

            Directory.CreateDirectory(
                backupDirectory);

            File.Copy(
                splinePath,
                Path.Combine(
                    backupDirectory,
                    Path.GetFileName(
                        splinePath)),
                overwrite:
                    true);
        }

        var temporaryPath =
            splinePath +
            ".tmp-" +
            Guid.NewGuid()
                .ToString("N");

        try
        {
            await File.WriteAllTextAsync(
                    temporaryPath,
                    BuildSpline(
                        normalized),
                    SplineEncoding,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                temporaryPath,
                splinePath,
                overwrite:
                    true);

            var manifestPath =
                Path.Combine(
                    packDirectory,
                    fileStem +
                    ".mapstudio.txt");

            await File.WriteAllTextAsync(
                    manifestPath,
                    BuildManifest(
                        normalized),
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            return new MapStudioTunnelSplineResult(
                packDirectory,
                splinePath,
                @"Splines\" +
                PackFolderName +
                @"\" +
                Path.GetFileName(
                    splinePath),
                backupDirectory,
                texturePaths);
        }
        finally
        {
            if (File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }
    }

    public string BuildSpline(
        MapStudioTunnelSpec spec)
    {
        ArgumentNullException.ThrowIfNull(
            spec);

        var tunnel =
            spec.Normalize();

        var builder =
            new StringBuilder();

        AppendTexture(
            builder,
            "ms_tunnel_asphalt.bmp");

        AppendTexture(
            builder,
            "ms_tunnel_concrete.bmp");

        AppendTexture(
            builder,
            "ms_tunnel_marking.bmp");

        var roadWidth =
            tunnel.LaneCount *
            tunnel.LaneWidthMeters;

        var roadHalf =
            roadWidth /
            2.0;

        var innerHalf =
            roadHalf +
            tunnel.ShoulderWidthMeters;

        const double roadHeight =
            0.10;

        AppendProfileSegment(
            builder,
            0,
            -roadHalf,
            roadHeight,
            roadHalf,
            roadHeight,
            0.20);

        if (
            tunnel.ShoulderWidthMeters >
            0.01)
        {
            AppendProfileSegment(
                builder,
                1,
                -innerHalf,
                roadHeight +
                    0.04,
                -roadHalf,
                roadHeight +
                    0.04,
                0.25);

            AppendProfileSegment(
                builder,
                1,
                roadHalf,
                roadHeight +
                    0.04,
                innerHalf,
                roadHeight +
                    0.04,
                0.25);
        }

        for (
            var lane = 1;
            lane < tunnel.LaneCount;
            lane++)
        {
            var x =
                -roadHalf +
                lane *
                tunnel.LaneWidthMeters;

            AppendProfileSegment(
                builder,
                2,
                x -
                    0.06,
                roadHeight +
                    0.012,
                x +
                    0.06,
                roadHeight +
                    0.012,
                1.0);
        }

        var archRise =
            Math.Min(
                innerHalf,
                tunnel.InnerHeightMeters *
                    0.55);

        var wallTop =
            tunnel.InnerHeightMeters -
            archRise;

        AppendProfileSegment(
            builder,
            1,
            -innerHalf,
            roadHeight,
            -innerHalf,
            wallTop,
            0.20);

        for (
            var segment = 0;
            segment < tunnel.ArchSegments;
            segment++)
        {
            var startAngle =
                Math.PI -
                segment *
                Math.PI /
                tunnel.ArchSegments;

            var endAngle =
                Math.PI -
                (
                    segment +
                    1
                ) *
                Math.PI /
                tunnel.ArchSegments;

            var x1 =
                innerHalf *
                Math.Cos(
                    startAngle);

            var z1 =
                wallTop +
                archRise *
                Math.Sin(
                    startAngle);

            var x2 =
                innerHalf *
                Math.Cos(
                    endAngle);

            var z2 =
                wallTop +
                archRise *
                Math.Sin(
                    endAngle);

            AppendProfileSegment(
                builder,
                1,
                x1,
                z1,
                x2,
                z2,
                0.20);
        }

        AppendProfileSegment(
            builder,
            1,
            innerHalf,
            wallTop,
            innerHalf,
            roadHeight,
            0.20);

        for (
            var lane = 0;
            lane < tunnel.LaneCount;
            lane++)
        {
            var center =
                -roadHalf +
                tunnel.LaneWidthMeters *
                (
                    lane +
                    0.5
                );

            var direction =
                tunnel.OneWay
                    ? 0
                    : center >=
                        0
                        ? 0
                        : 1;

            AppendPath(
                builder,
                center,
                roadHeight,
                Math.Max(
                    2.5,
                    tunnel.LaneWidthMeters -
                    0.3),
                direction);
        }

        return NormalizeLineEndings(
            builder.ToString());
    }

    private static void AppendTexture(
        StringBuilder builder,
        string name)
    {
        builder.AppendLine(
            "[texture]");

        builder.AppendLine(
            name);
    }

    private static void AppendProfileSegment(
        StringBuilder builder,
        int textureIndex,
        double x1,
        double z1,
        double x2,
        double z2,
        double textureScale)
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
                x1));

        builder.AppendLine(
            Format(
                z1));

        builder.AppendLine(
            "0.005");

        builder.AppendLine(
            Format(
                textureScale));

        builder.AppendLine(
            "[profilepnt]");

        builder.AppendLine(
            Format(
                x2));

        builder.AppendLine(
            Format(
                z2));

        builder.AppendLine(
            "0.995");

        builder.AppendLine(
            Format(
                textureScale));
    }

    private static void AppendPath(
        StringBuilder builder,
        double x,
        double z,
        double width,
        int direction)
    {
        builder.AppendLine(
            "[path]");

        builder.AppendLine(
            "0");

        builder.AppendLine(
            Format(
                x));

        builder.AppendLine(
            Format(
                z));

        builder.AppendLine(
            Format(
                width));

        builder.AppendLine(
            direction.ToString(
                CultureInfo
                    .InvariantCulture));
    }

    private static string BuildManifest(
        MapStudioTunnelSpec spec)
    {
        var tunnel =
            spec.Normalize();

        return NormalizeLineEndings(
            string.Join(
                "\n",
                [
                    "OMSI Map Studio Tunnel",
                    "Generated=procedural",
                    "License=Original Map Studio content",
                    "Name=" +
                        tunnel.Name,
                    "LaneCount=" +
                        tunnel.LaneCount
                            .ToString(
                                CultureInfo
                                    .InvariantCulture),
                    "LaneWidthMeters=" +
                        Format(
                            tunnel.LaneWidthMeters),
                    "InnerHeightMeters=" +
                        Format(
                            tunnel.InnerHeightMeters),
                    "ShoulderWidthMeters=" +
                        Format(
                            tunnel.ShoulderWidthMeters),
                    "ArchSegments=" +
                        tunnel.ArchSegments
                            .ToString(
                                CultureInfo
                                    .InvariantCulture),
                    "OneWay=" +
                        tunnel.OneWay
                            .ToString(
                                CultureInfo
                                    .InvariantCulture)
                ]));
    }

    private static async Task<IReadOnlyList<string>>
        EnsureTexturesAsync(
            string textureDirectory,
            CancellationToken cancellationToken)
    {
        var textures =
            new[]
            {
                (
                    Name:
                        "ms_tunnel_asphalt.bmp",
                    Data:
                        CreateTexture(
                            64,
                            64,
                            static (x, y) =>
                            {
                                var noise =
                                    (
                                        x *
                                            19 +
                                        y *
                                            29
                                    ) %
                                    15;

                                var value =
                                    (byte)(
                                        54 +
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
                        "ms_tunnel_concrete.bmp",
                    Data:
                        CreateTexture(
                            64,
                            64,
                            static (x, y) =>
                            {
                                var joint =
                                    y %
                                        20 ==
                                    0;

                                var noise =
                                    (
                                        x *
                                            7 +
                                        y *
                                            13
                                    ) %
                                    12;

                                var value =
                                    (byte)(
                                        (
                                            joint
                                                ? 112
                                                : 146
                                        ) +
                                        noise);

                                return new Rgb(
                                    value,
                                    value,
                                    (byte)(
                                        value -
                                        3));
                            })
                ),
                (
                    Name:
                        "ms_tunnel_marking.bmp",
                    Data:
                        CreateTexture(
                            8,
                            8,
                            static (_, _) =>
                                new Rgb(
                                    236,
                                    232,
                                    208))
                )
            };

        var result =
            new List<string>(
                textures.Length);

        foreach (
            var texture in
                textures)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            var path =
                Path.Combine(
                    textureDirectory,
                    texture.Name);

            if (!File.Exists(
                    path))
            {
                await File
                    .WriteAllBytesAsync(
                        path,
                        texture.Data,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            result.Add(
                path);
        }

        return result;
    }

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

    private static string SanitizeFileStem(
        string name)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var builder =
            new StringBuilder();

        foreach (
            var character in
                name.Trim())
        {
            if (
                invalid.Contains(
                    character))
            {
                continue;
            }

            if (
                char.IsLetterOrDigit(
                    character))
            {
                builder.Append(
                    char.ToLowerInvariant(
                        character));

                continue;
            }

            if (
                character is
                    ' ' or '-' or '_')
            {
                if (
                    builder.Length >
                        0 &&
                    builder[
                        builder.Length -
                        1] !=
                    '_')
                {
                    builder.Append(
                        '_');
                }
            }
        }

        var value =
            builder
                .ToString()
                .Trim('_');

        return string.IsNullOrWhiteSpace(
                value)
            ? "tunnel"
            : value;
    }

    private static string NormalizeLineEndings(
        string value) =>
        value
            .Replace(
                "\r\n",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\r",
                "\n",
                StringComparison.Ordinal)
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);

    private static string Format(
        double value) =>
        value.ToString(
            "0.###",
            CultureInfo
                .InvariantCulture);

    private readonly record struct Rgb(
        byte R,
        byte G,
        byte B);
}
