using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiTileReader
{
    private readonly OmsiTerrainReader _terrainReader =
        new();

    private readonly OmsiTerrainRenderDataReader
        _terrainRenderDataReader =
            new();

    public async Task<OmsiTileContent> ReadContentAsync(
        string tilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilePath);

        if (!File.Exists(tilePath))
        {
            return OmsiTileContent.Missing;
        }

        var document =
            await OmsiConfigParser.ParseFileAsync(
                tilePath,
                cancellationToken);

        var content =
            ReadContent(
                document);

        var terrainPath =
            tilePath + ".terrain";

        var terrainFileExists =
            File.Exists(
                terrainPath);

        var terrainFileSize =
            terrainFileExists
                ? new FileInfo(
                    terrainPath)
                    .Length
                : 0;

        OmsiTerrainGrid? terrain =
            null;

        if (terrainFileExists)
        {
            try
            {
                terrain =
                    await _terrainReader
                        .ReadAsync(
                            terrainPath,
                            cancellationToken);
            }
            catch (InvalidDataException)
            {
                terrain = null;
            }
        }

        var terrainRenderDataPath =
            terrainPath + "_0.rdy";

        var terrainRenderData =
            File.Exists(
                terrainRenderDataPath)
                ? _terrainRenderDataReader
                    .Read(
                        terrainRenderDataPath)
                : null;

        return content with
        {
            Terrain = terrain,
            TerrainRenderData =
                terrainRenderData,
            Summary =
                content.Summary with
                {
                    TerrainFileExists =
                        terrainFileExists,
                    TerrainFileSize =
                        terrainFileSize
                }
        };
    }

    public static OmsiTileContent ReadContent(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var attachmentCount =
            document.FindSections("splineAttachement").Count() +
            document.FindSections("splineAttachment").Count();

        var splineCount =
            document.FindSections("spline").Count() +
            document.FindSections("spline_h").Count();

        var summary = new OmsiTileSummary(
            Exists: true,
            ObjectCount:
                document.FindSections("object").Count(),
            SplineCount:
                splineCount,
            SplineAttachmentCount:
                attachmentCount,
            TerrainMarkerPresent:
                document.FindFirstSection(
                    "terrain") is not null);

        return new OmsiTileContent(
            summary,
            ReadObjects(document),
            ReadSplines(document));
    }

    public async Task<OmsiTileSummary> ReadSummaryAsync(
        string tilePath,
        CancellationToken cancellationToken = default) =>
        (await ReadContentAsync(
            tilePath,
            cancellationToken)).Summary;

    public async Task<IReadOnlyList<OmsiPlacedObject>>
        ReadObjectsAsync(
            string tilePath,
            CancellationToken cancellationToken = default) =>
        (await ReadContentAsync(
            tilePath,
            cancellationToken)).Objects;

    public static IReadOnlyList<OmsiPlacedObject> ReadObjects(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var objects = new List<OmsiPlacedObject>();

        var objectSections =
            document.FindSections("object")
                .ToArray();

        for (
            var sectionOrdinal = 0;
            sectionOrdinal < objectSections.Length;
            sectionOrdinal++)
        {
            var section =
                objectSections[sectionOrdinal];

            var values =
                section.DataLines.ToArray();

            if (values.Length < 9 ||
                !int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var objectId) ||
                !TryParseDouble(values[3], out var x) ||
                !TryParseDouble(values[4], out var y) ||
                !TryParseDouble(values[5], out var z) ||
                !TryParseDouble(
                    values[6],
                    out var rotation) ||
                !TryParseDouble(
                    values[7],
                    out var pitch) ||
                !TryParseDouble(
                    values[8],
                    out var bank))
            {
                continue;
            }

            objects.Add(new OmsiPlacedObject(
                HeaderValue: values[0],
                SceneryObjectPath: values[1],
                ObjectId: objectId,
                X: x,
                Y: y,
                Z: z,
                Rotation: rotation,
                Pitch: pitch,
                Bank: bank,
                ExtraValues:
                    values.Skip(9).ToArray())
                {
                    SourceSectionOrdinal =
                        sectionOrdinal
                });
        }

        return objects;
    }

    public static IReadOnlyList<OmsiPlacedSpline> ReadSplines(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var splines =
            new List<OmsiPlacedSpline>();

        var sourceSectionOrdinal = 0;

        foreach (var section in document.Sections)
        {
            var isSpline =
                string.Equals(
                    section.Keyword,
                    "spline",
                    StringComparison.OrdinalIgnoreCase);

            var isHeightSpline =
                string.Equals(
                    section.Keyword,
                    "spline_h",
                    StringComparison.OrdinalIgnoreCase);

            if (!isSpline && !isHeightSpline)
            {
                continue;
            }

            var currentSectionOrdinal =
                sourceSectionOrdinal++;

            var values =
                section.DataLines.ToArray();

            if (values.Length < 13 ||
                !int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var splineId) ||
                !int.TryParse(
                    values[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var previousSplineId) ||
                !int.TryParse(
                    values[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var nextSplineId) ||
                !TryParseDouble(values[5], out var x) ||
                !TryParseDouble(values[6], out var z) ||
                !TryParseDouble(values[7], out var y) ||
                !TryParseDouble(
                    values[8],
                    out var rotation) ||
                !TryParseDouble(
                    values[9],
                    out var length) ||
                !TryParseDouble(
                    values[10],
                    out var radius) ||
                !TryParseDouble(
                    values[11],
                    out var gradientStart) ||
                !TryParseDouble(
                    values[12],
                    out var gradientEnd))
            {
                continue;
            }

            splines.Add(new OmsiPlacedSpline(
                HeaderValue: values[0],
                SplinePath: values[1],
                SplineId: splineId,
                PreviousSplineId: previousSplineId,
                NextSplineId: nextSplineId,
                X: x,
                Z: z,
                Y: y,
                Rotation: rotation,
                Length: length,
                Radius: radius,
                GradientStart: gradientStart,
                GradientEnd: gradientEnd,
                IsHeightSpline: isHeightSpline,
                ExtraValues:
                    values.Skip(13).ToArray())
                {
                    SourceSectionOrdinal =
                        currentSectionOrdinal
                });
        }

        return splines;
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
