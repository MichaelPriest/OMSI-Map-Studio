using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Splines;

public sealed class OmsiSplineDefinitionReader
{
    public async Task<OmsiSplineDefinition> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return OmsiSplineDefinition.Missing;
        }

        var document =
            await OmsiConfigParser.ParseFileAsync(
                path,
                cancellationToken);

        return Read(document);
    }

    public OmsiSplineDefinition Read(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var textures =
            document.FindSections("texture")
                .Select(
                    static section =>
                        section.DataLines
                            .FirstOrDefault())
                .Where(
                    static value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Select(
                    static value =>
                        value!)
                .ToArray();

        var alphaModes =
            new int[textures.Length];

        var currentTextureIndex = -1;

        foreach (var section in
            document.Sections)
        {
            if (string.Equals(
                    section.Keyword,
                    "texture",
                    StringComparison.OrdinalIgnoreCase))
            {
                currentTextureIndex++;
                continue;
            }

            if (
                currentTextureIndex < 0 ||
                currentTextureIndex >=
                    alphaModes.Length ||
                !string.Equals(
                    section.Keyword,
                    "matl_alpha",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value =
                section.DataLines
                    .FirstOrDefault();

            if (
                int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var alphaMode))
            {
                alphaModes[
                    currentTextureIndex] =
                    Math.Clamp(
                        alphaMode,
                        0,
                        2);
            }
        }

        var surfaces =
            new List<OmsiSplineSurface>();

        for (
            var index = 0;
            index < document.Sections.Count;
            index++)
        {
            var section =
                document.Sections[index];

            if (!string.Equals(
                    section.Keyword,
                    "profile",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var textureValue =
                section.DataLines
                    .FirstOrDefault();

            if (!int.TryParse(
                    textureValue,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var textureIndex) ||
                textureIndex < 0)
            {
                continue;
            }

            var points =
                new List<OmsiSplineProfilePoint>();

            for (
                var nextIndex = index + 1;
                nextIndex < document.Sections.Count;
                nextIndex++)
            {
                var next =
                    document.Sections[nextIndex];

                if (string.Equals(
                        next.Keyword,
                        "profile",
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (!string.Equals(
                        next.Keyword,
                        "profilepnt",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var point =
                    TryReadPoint(next);

                if (point is not null)
                {
                    points.Add(point);
                }
            }

            if (points.Count < 2)
            {
                continue;
            }

            for (
                var pointIndex = 0;
                pointIndex + 1 < points.Count;
                pointIndex++)
            {
                surfaces.Add(
                    new OmsiSplineSurface(
                        TextureIndex:
                            textureIndex,
                        TextureName:
                            textureIndex <
                                textures.Length
                                ? textures[
                                    textureIndex]
                                : null,
                        AlphaMode:
                            textureIndex <
                                alphaModes.Length
                                ? alphaModes[
                                    textureIndex]
                                : 0,
                        From:
                            points[pointIndex],
                        To:
                            points[
                                pointIndex + 1]));
            }
        }

        return new OmsiSplineDefinition(
            Exists: true,
            Textures: textures,
            Surfaces: surfaces)
        {
            Paths =
                ReadPaths(
                    document)
        };
    }

    private static IReadOnlyList<
        OmsiSplinePathDefinition>
        ReadPaths(
            OmsiConfigDocument document)
    {
        var paths =
            new List<
                OmsiSplinePathDefinition>();

        foreach (
            var section in
                document.FindSections(
                    "path"))
        {
            var values =
                section.DataLines
                    .Take(5)
                    .ToArray();

            if (
                values.Length < 5 ||
                !int.TryParse(
                    values[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var type) ||
                type is < 0 or > 3 ||
                !TryParseDouble(
                    values[1],
                    out var x) ||
                !TryParseDouble(
                    values[2],
                    out var z) ||
                !TryParseDouble(
                    values[3],
                    out var width) ||
                width < 0 ||
                !int.TryParse(
                    values[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var direction) ||
                direction is < 0 or > 2)
            {
                continue;
            }

            paths.Add(
                new OmsiSplinePathDefinition(
                    type,
                    x,
                    z,
                    width,
                    direction));
        }

        return paths;
    }

    private static OmsiSplineProfilePoint?
        TryReadPoint(
            OmsiConfigSection section)
    {
        var values =
            section.DataLines
                .Take(4)
                .ToArray();

        if (values.Length < 4 ||
            !TryParseDouble(
                values[0],
                out var x) ||
            !TryParseDouble(
                values[1],
                out var z) ||
            !TryParseDouble(
                values[2],
                out var textureX) ||
            !TryParseDouble(
                values[3],
                out var textureScale))
        {
            return null;
        }

        return new OmsiSplineProfilePoint(
            X: x,
            Z: z,
            TextureX: textureX,
            TextureScale: textureScale);
    }

    private static bool TryParseDouble(
        string value,
        out double result) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out result);
}
