using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MapStudio.Core.Omsi.Splines;

public sealed record MapStudioBridgeSpec(
    string Name,
    int LaneCount,
    double LaneWidthMeters,
    double SidewalkWidthMeters,
    double DeckThicknessMeters,
    bool OneWay = false)
{
    public MapStudioBridgeSpec Normalize()
    {
        var name =
            string.IsNullOrWhiteSpace(
                Name)
                ? "Bridge"
                : Name.Trim();

        return this with
        {
            Name = name,
            LaneCount =
                Math.Clamp(
                    LaneCount,
                    1,
                    8),
            LaneWidthMeters =
                Math.Clamp(
                    double.IsFinite(
                        LaneWidthMeters)
                        ? LaneWidthMeters
                        : 3.5,
                    2.5,
                    6.0),
            SidewalkWidthMeters =
                Math.Clamp(
                    double.IsFinite(
                        SidewalkWidthMeters)
                        ? SidewalkWidthMeters
                        : 1.5,
                    0,
                    5.0),
            DeckThicknessMeters =
                Math.Clamp(
                    double.IsFinite(
                        DeckThicknessMeters)
                        ? DeckThicknessMeters
                        : 0.55,
                    0.2,
                    2.0)
        };
    }
}

public sealed record MapStudioBridgeSplineResult(
    string PackDirectory,
    string SplinePath,
    string RelativeSplinePath,
    string? BackupDirectory,
    IReadOnlyList<string> TexturePaths);

public sealed class MapStudioBridgeSplineGenerator
{
    public const string PackFolderName =
        "MapStudio_Bridges";

    public async Task<MapStudioBridgeSplineResult>
        GenerateAsync(
            string contentRoot,
            MapStudioBridgeSpec spec,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentRoot);

        ArgumentNullException.ThrowIfNull(
            spec);

        var normalized =
            spec.Normalize();

        var root =
            Path.GetFullPath(
                contentRoot);

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

        var textures =
            await EnsureTexturesAsync(
                    textureDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

        var fileName =
            "ms_bridge_" +
            SanitizeFileStem(
                normalized.Name) +
            ".sli";

        var splinePath =
            Path.Combine(
                packDirectory,
                fileName);

        var manifestPath =
            Path.Combine(
                packDirectory,
                Path.GetFileNameWithoutExtension(
                    fileName) +
                ".mapstudio.txt");

        string? backupDirectory =
            null;

        if (
            File.Exists(
                splinePath) ||
            File.Exists(
                manifestPath))
        {
            backupDirectory =
                Path.Combine(
                    root,
                    ".mapstudio",
                    "backups",
                    "bridges",
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

            if (File.Exists(
                    splinePath))
            {
                File.Copy(
                    splinePath,
                    Path.Combine(
                        backupDirectory,
                        Path.GetFileName(
                            splinePath)),
                    overwrite:
                        true);
            }

            if (File.Exists(
                    manifestPath))
            {
                File.Copy(
                    manifestPath,
                    Path.Combine(
                        backupDirectory,
                        Path.GetFileName(
                            manifestPath)),
                    overwrite:
                        true);
            }
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
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            File.Move(
                temporaryPath,
                splinePath,
                overwrite:
                    true);

            await File.WriteAllTextAsync(
                    manifestPath,
                    BuildManifest(
                        normalized),
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
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

        return new MapStudioBridgeSplineResult(
            packDirectory,
            splinePath,
            @"Splines\" +
            PackFolderName +
            @"\" +
            fileName,
            backupDirectory,
            textures);
    }

    public string BuildSpline(
        MapStudioBridgeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(
            spec);

        var bridge =
            spec.Normalize();

        var roadWidth =
            bridge.LaneCount *
            bridge.LaneWidthMeters;

        var roadHalf =
            roadWidth /
            2.0;

        var outerHalf =
            roadHalf +
            bridge.SidewalkWidthMeters;

        var top =
            bridge.DeckThicknessMeters;

        var builder =
            new StringBuilder();

        AppendTexture(
            builder,
            "ms_bridge_asphalt.bmp");

        AppendTexture(
            builder,
            "ms_bridge_concrete.bmp");

        AppendTexture(
            builder,
            "ms_bridge_marking.bmp");

        // Structural deck / underside.
        AppendProfile(
            builder,
            1,
            -outerHalf,
            0,
            outerHalf,
            0,
            0.18);

        AppendProfile(
            builder,
            1,
            -outerHalf,
            0,
            -outerHalf,
            top +
                0.10,
            0.18);

        AppendProfile(
            builder,
            1,
            outerHalf,
            top +
                0.10,
            outerHalf,
            0,
            0.18);

        // Drivable surface.
        AppendProfile(
            builder,
            0,
            -roadHalf,
            top,
            roadHalf,
            top,
            0.20);

        if (
            bridge.SidewalkWidthMeters >
            0.01)
        {
            AppendProfile(
                builder,
                1,
                -outerHalf,
                top +
                    0.10,
                -roadHalf,
                top +
                    0.10,
                0.25);

            AppendProfile(
                builder,
                1,
                roadHalf,
                top +
                    0.10,
                outerHalf,
                top +
                    0.10,
                0.25);
        }

        for (
            var lane = 1;
            lane < bridge.LaneCount;
            lane++)
        {
            var x =
                -roadHalf +
                lane *
                bridge.LaneWidthMeters;

            AppendProfile(
                builder,
                2,
                x -
                    0.06,
                top +
                    0.012,
                x +
                    0.06,
                top +
                    0.012,
                1.0);
        }

        for (
            var lane = 0;
            lane < bridge.LaneCount;
            lane++)
        {
            var center =
                -roadHalf +
                bridge.LaneWidthMeters *
                (
                    lane +
                    0.5
                );

            var direction =
                bridge.OneWay
                    ? 0
                    : center >=
                        0
                        ? 0
                        : 1;

            AppendPath(
                builder,
                center,
                top,
                Math.Max(
                    2.5,
                    bridge.LaneWidthMeters -
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

    private static void AppendProfile(
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
                CultureInfo.InvariantCulture));

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
                CultureInfo.InvariantCulture));
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
                        "ms_bridge_asphalt.bmp",
                    Data:
                        CreateTexture(
                            64,
                            64,
                            static (x, y) =>
                            {
                                var noise =
                                    (
                                        x * 17 +
                                        y * 23
                                    ) %
                                    14;

                                var value =
                                    (byte)(
                                        57 +
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
                        "ms_bridge_concrete.bmp",
                    Data:
                        CreateTexture(
                            64,
                            64,
                            static (x, y) =>
                            {
                                var seam =
                                    x %
                                        24 ==
                                    0 ||
                                    y %
                                        24 ==
                                    0;

                                var noise =
                                    (
                                        x * 5 +
                                        y * 11
                                    ) %
                                    10;

                                var value =
                                    (byte)(
                                        (
                                            seam
                                                ? 118
                                                : 154
                                        ) +
                                        noise);

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
                        "ms_bridge_marking.bmp",
                    Data:
                        CreateTexture(
                            8,
                            8,
                            static (_, _) =>
                                new Rgb(
                                    238,
                                    236,
                                    218))
                )
            };

        var paths =
            new List<string>(
                textures.Length);

        foreach (
            var texture in textures)
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
                await File.WriteAllBytesAsync(
                        path,
                        texture.Data,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            paths.Add(
                path);
        }

        return paths;
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
            checked(
                rowStride *
                height);

        var output =
            new byte[
                checked(
                    54 +
                    pixelBytes)];

        output[0] =
            (byte)'B';

        output[1] =
            (byte)'M';

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    2,
                    4),
                output.Length);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    10,
                    4),
                54);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    14,
                    4),
                40);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    18,
                    4),
                width);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
                    22,
                    4),
                height);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                output.AsSpan(
                    26,
                    2),
                1);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                output.AsSpan(
                    28,
                    2),
                24);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                output.AsSpan(
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

            var row =
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
                    row +
                    x *
                    3;

                output[offset] =
                    color.B;

                output[
                    offset +
                    1] =
                    color.G;

                output[
                    offset +
                    2] =
                    color.R;
            }
        }

        return output;
    }

    private static string BuildManifest(
        MapStudioBridgeSpec spec)
    {
        var bridge =
            spec.Normalize();

        return NormalizeLineEndings(
            string.Join(
                "\n",
                [
                    "OMSI Map Studio Bridge",
                    "Generated=procedural",
                    "License=Original Map Studio content",
                    "Name=" +
                        bridge.Name,
                    "LaneCount=" +
                        bridge.LaneCount
                            .ToString(
                                CultureInfo
                                    .InvariantCulture),
                    "LaneWidthMeters=" +
                        Format(
                            bridge.LaneWidthMeters),
                    "SidewalkWidthMeters=" +
                        Format(
                            bridge.SidewalkWidthMeters),
                    "DeckThicknessMeters=" +
                        Format(
                            bridge.DeckThicknessMeters),
                    "OneWay=" +
                        bridge.OneWay
                            .ToString(
                                CultureInfo
                                    .InvariantCulture)
                ]));
    }

    private static string SanitizeFileStem(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();

        var result =
            new string(
                value
                    .ToLowerInvariant()
                    .Select(
                        character =>
                            invalid.Contains(
                                character) ||
                            char.IsWhiteSpace(
                                character)
                                ? '_'
                                : character)
                    .ToArray())
                .Trim(
                    '_');

        return string.IsNullOrWhiteSpace(
                result)
            ? "bridge"
            : result;
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
            CultureInfo.InvariantCulture);

    private readonly record struct Rgb(
        byte R,
        byte G,
        byte B);
}
