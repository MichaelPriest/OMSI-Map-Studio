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

        var terrainTask =
            terrainFileExists
                ? ReadTerrainSafeAsync(
                    terrainPath,
                    cancellationToken)
                : Task.FromResult<
                    OmsiTerrainGrid?>(null);

        var terrainRenderDataPath =
            terrainPath + "_0.rdy";

        var terrainRenderData =
            File.Exists(
                terrainRenderDataPath)
                ? _terrainRenderDataReader
                    .Read(
                        terrainRenderDataPath)
                : null;

        var terrainTextureMasks =
            ReadTerrainTextureMasks(
                tilePath);

        var terrain =
            await terrainTask
                .ConfigureAwait(false);

        return content with
        {
            Terrain = terrain,
            TerrainRenderData =
                terrainRenderData,
            TerrainTextureMasks =
                terrainTextureMasks,
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

    private async Task<OmsiTerrainGrid?>
        ReadTerrainSafeAsync(
            string terrainPath,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _terrainReader
                .ReadAsync(
                    terrainPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    public static IReadOnlyList<OmsiTerrainTextureMask>
        ReadTerrainTextureMasks(
            string tilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            tilePath);

        var mapDirectory =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    tilePath));

        if (
            string.IsNullOrWhiteSpace(
                mapDirectory))
        {
            return Array.Empty<
                OmsiTerrainTextureMask>();
        }

        var textureMapDirectory =
            Path.Combine(
                mapDirectory,
                "texture",
                "map");

        if (
            !Directory.Exists(
                textureMapDirectory))
        {
            return Array.Empty<
                OmsiTerrainTextureMask>();
        }

        var tileFileName =
            Path.GetFileName(
                tilePath);

        var prefix =
            tileFileName + ".";

        var masks =
            new List<
                OmsiTerrainTextureMask>();

        var maskReader =
            new OmsiTerrainTextureMaskReader();

        foreach (
            var path in Directory
                .EnumerateFiles(
                    textureMapDirectory,
                    tileFileName +
                        ".*.dds",
                    SearchOption
                        .TopDirectoryOnly))
        {
            var fileName =
                Path.GetFileName(
                    path);

            if (
                !fileName.StartsWith(
                    prefix,
                    StringComparison
                        .OrdinalIgnoreCase) ||
                !fileName.EndsWith(
                    ".dds",
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                continue;
            }

            var layerText =
                fileName[
                    prefix.Length..
                    ^4];

            if (
                !int.TryParse(
                    layerText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var layerIndex) ||
                layerIndex <= 0)
            {
                continue;
            }

            masks.Add(
                maskReader.ReadHeader(
                    layerIndex,
                    path));
        }

        return masks
            .OrderBy(
                mask =>
                    mask.LayerIndex)
            .ToArray();
    }

    public static OmsiTileContent ReadContent(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var attachments =
            ReadAttachments(document);

        var attachmentCount =
            attachments.Count(
                attachment =>
                    attachment.Kind !=
                    OmsiAttachmentKind
                        .ObjectAttachment);

        var splineCount =
            document.Sections.Count(
                OmsiSplineFieldLayout
                    .IsSplineSection);

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
            ReadSplines(document),
            Attachments:
                attachments);
    }

    public async Task<OmsiTileSummary> ReadSummaryAsync(
        string tilePath,
        CancellationToken cancellationToken = default) =>
        (await ReadContentAsync(
            tilePath,
            cancellationToken)).Summary;

    public async Task<OmsiTileSummary>
        ReadSummaryLightAsync(
            string tilePath,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            tilePath);

        if (!File.Exists(tilePath))
        {
            return OmsiTileSummary.Missing;
        }

        var document =
            await OmsiConfigParser
                .ParseFileAsync(
                    tilePath,
                    cancellationToken)
                .ConfigureAwait(false);

        var summary =
            ReadContent(
                document)
            .Summary;

        var terrainPath =
            tilePath +
            ".terrain";

        var terrainFileExists =
            File.Exists(
                terrainPath);

        var terrainFileSize =
            terrainFileExists
                ? new FileInfo(
                    terrainPath)
                    .Length
                : 0;

        return summary with
        {
            TerrainFileExists =
                terrainFileExists,
            TerrainFileSize =
                terrainFileSize
        };
    }

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
                        sectionOrdinal,
                    TrafficRules =
                        ReadTrafficRulesAfter(
                            document,
                            section)
                });
        }

        return objects;
    }

    public static IReadOnlyList<OmsiPlacedAttachment>
        ReadAttachments(
            OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var result =
            new List<OmsiPlacedAttachment>();

        var sourceOrdinal =
            0;

        foreach (
            var section in
                document.Sections)
        {
            var keyword =
                section.Keyword;

            var isObjectAttachment =
                string.Equals(
                    keyword,
                    "attachObj",
                    StringComparison
                        .OrdinalIgnoreCase);

            var isSplineAttachment =
                string.Equals(
                    keyword,
                    "splineAttachement",
                    StringComparison
                        .OrdinalIgnoreCase) ||
                string.Equals(
                    keyword,
                    "splineAttachment",
                    StringComparison
                        .OrdinalIgnoreCase);

            var isRepeater =
                string.Equals(
                    keyword,
                    "splineAttachement_repeater",
                    StringComparison
                        .OrdinalIgnoreCase) ||
                string.Equals(
                    keyword,
                    "splineAttachment_repeater",
                    StringComparison
                        .OrdinalIgnoreCase);

            if (
                !isObjectAttachment &&
                !isSplineAttachment &&
                !isRepeater)
            {
                continue;
            }

            var currentOrdinal =
                sourceOrdinal++;

            var values =
                section.DataLines
                    .ToArray();

            OmsiPlacedAttachment?
                attachment =
                    null;

            if (
                isObjectAttachment &&
                values.Length >=
                    10 &&
                int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo
                        .InvariantCulture,
                    out var attachmentId) &&
                int.TryParse(
                    values[3],
                    NumberStyles.Integer,
                    CultureInfo
                        .InvariantCulture,
                    out var parentId) &&
                int.TryParse(
                    values[5],
                    NumberStyles.Integer,
                    CultureInfo
                        .InvariantCulture,
                    out var attachPoint) &&
                TryParseDouble(
                    values[6],
                    out var rotation) &&
                TryParseDouble(
                    values[7],
                    out var pitch) &&
                TryParseDouble(
                    values[8],
                    out var bank) &&
                int.TryParse(
                    values[9],
                    NumberStyles.Integer,
                    CultureInfo
                        .InvariantCulture,
                    out var labelsCount))
            {
                attachment =
                    new OmsiPlacedAttachment(
                        OmsiAttachmentKind
                            .ObjectAttachment,
                        values[0],
                        values[1],
                        attachmentId,
                        parentId,
                        attachPoint,
                        null,
                        null,
                        null,
                        rotation,
                        pitch,
                        bank,
                        null,
                        null,
                        labelsCount,
                        values);
            }
            else if (
                isSplineAttachment &&
                values.Length >=
                    14 &&
                int.TryParse(
                    values[2],
                    NumberStyles.Integer,
                    CultureInfo
                        .InvariantCulture,
                    out var splineAttachmentId) &&
                TryParseDouble(
                    values[4],
                    out var x) &&
                TryParseDouble(
                    values[5],
                    out var z) &&
                TryParseDouble(
                    values[6],
                    out var y) &&
                TryParseDouble(
                    values[7],
                    out var splineRotation) &&
                TryParseDouble(
                    values[8],
                    out var splinePitch) &&
                TryParseDouble(
                    values[9],
                    out var splineBank) &&
                TryParseDouble(
                    values[10],
                    out var interval) &&
                TryParseDouble(
                    values[11],
                    out var distance))
            {
                attachment =
                    new OmsiPlacedAttachment(
                        OmsiAttachmentKind
                            .SplineAttachment,
                        values[0],
                        values[1],
                        splineAttachmentId,
                        null,
                        null,
                        x,
                        z,
                        y,
                        splineRotation,
                        splinePitch,
                        splineBank,
                        interval,
                        distance,
                        null,
                        values);
            }
            else if (
                isRepeater &&
                values.Length >=
                    16 &&
                int.TryParse(
                    values[4],
                    NumberStyles.Integer,
                    CultureInfo
                        .InvariantCulture,
                    out var repeaterId) &&
                TryParseDouble(
                    values[6],
                    out var repeaterX) &&
                TryParseDouble(
                    values[7],
                    out var repeaterZ) &&
                TryParseDouble(
                    values[8],
                    out var repeaterY) &&
                TryParseDouble(
                    values[9],
                    out var repeaterRotation) &&
                TryParseDouble(
                    values[10],
                    out var repeaterPitch) &&
                TryParseDouble(
                    values[11],
                    out var repeaterBank) &&
                TryParseDouble(
                    values[12],
                    out var repeaterInterval) &&
                TryParseDouble(
                    values[13],
                    out var repeaterDistance))
            {
                attachment =
                    new OmsiPlacedAttachment(
                        OmsiAttachmentKind
                            .SplineAttachmentRepeater,
                        values[0],
                        values[3],
                        repeaterId,
                        null,
                        null,
                        repeaterX,
                        repeaterZ,
                        repeaterY,
                        repeaterRotation,
                        repeaterPitch,
                        repeaterBank,
                        repeaterInterval,
                        repeaterDistance,
                        null,
                        values);
            }

            if (attachment is null)
            {
                continue;
            }

            result.Add(
                attachment with
                {
                    SourceSectionOrdinal =
                        currentOrdinal,
                    VariableParentValue =
                        ReadFollowingSectionValue(
                            document,
                            section,
                            "varparent")
                });
        }

        return result;
    }

    private static string?
        ReadFollowingSectionValue(
            OmsiConfigDocument document,
            OmsiConfigSection owner,
            string keyword)
    {
        var ownerIndex =
            document.Sections
                .Select(
                    (section, index) =>
                        (
                            section,
                            index
                        ))
                .Where(
                    pair =>
                        pair.section
                            .KeywordLineIndex ==
                        owner.KeywordLineIndex)
                .Select(
                    pair =>
                        pair.index)
                .DefaultIfEmpty(-1)
                .Single();

        if (ownerIndex < 0)
        {
            return null;
        }

        for (
            var index =
                ownerIndex + 1;
            index <
                document.Sections.Count;
            index++)
        {
            var section =
                document.Sections[index];

            if (
                string.Equals(
                    section.Keyword,
                    keyword,
                    StringComparison
                        .OrdinalIgnoreCase))
            {
                return section
                    .DataLines
                    .FirstOrDefault();
            }

            if (
                IsPlacementBoundary(
                    section))
            {
                break;
            }
        }

        return null;
    }

    public static IReadOnlyList<OmsiPlacedSpline> ReadSplines(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var splines =
            new List<OmsiPlacedSpline>();

        var sourceSectionOrdinal = 0;
        var version =
            OmsiSplineFieldLayout
                .ReadVersion(document);

        foreach (var section in document.Sections)
        {
            if (!OmsiSplineFieldLayout
                .IsSplineSection(section))
            {
                continue;
            }

            var currentSectionOrdinal =
                sourceSectionOrdinal++;

            var values =
                section.DataLines.ToArray();

            if (
                !OmsiSplineFieldLayout.TryCreate(
                    version,
                    values.Length,
                    out var layout) ||
                !int.TryParse(
                    values[layout.IdIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var splineId) ||
                !int.TryParse(
                    values[layout.PreviousIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var previousSplineId))
            {
                continue;
            }

            var nextSplineId = -1;

            if (
                layout.NextIndex is int nextIndex &&
                !int.TryParse(
                    values[nextIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out nextSplineId))
            {
                continue;
            }

            if (
                !TryParseDouble(
                    values[layout.XIndex],
                    out var x) ||
                !TryParseDouble(
                    values[layout.ZIndex],
                    out var z) ||
                !TryParseDouble(
                    values[layout.YIndex],
                    out var y) ||
                !TryParseDouble(
                    values[layout.RotationIndex],
                    out var rotation) ||
                !TryParseDouble(
                    values[layout.LengthIndex],
                    out var length) ||
                !TryParseDouble(
                    values[layout.RadiusIndex],
                    out var radius) ||
                !TryParseDouble(
                    values[layout.GradientStartIndex],
                    out var gradientStart) ||
                !TryParseDouble(
                    values[layout.GradientEndIndex],
                    out var gradientEnd))
            {
                continue;
            }

            var isHeightSpline =
                string.Equals(
                    section.Keyword,
                    "spline_h",
                    StringComparison.OrdinalIgnoreCase);

            splines.Add(new OmsiPlacedSpline(
                HeaderValue:
                    layout.HeaderIndex >= 0
                        ? values[layout.HeaderIndex]
                        : string.Empty,
                SplinePath:
                    values[layout.PathIndex],
                SplineId: splineId,
                PreviousSplineId:
                    previousSplineId,
                NextSplineId:
                    nextSplineId,
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
                    values
                        .Skip(
                            layout.ExtraStartIndex)
                        .ToArray())
                {
                    SourceSectionOrdinal =
                        currentSectionOrdinal,
                    TrafficRules =
                        ReadTrafficRulesAfter(
                            document,
                            section)
                });
        }

        return splines;
    }

    private static IReadOnlyList<
        OmsiTrafficRule>
        ReadTrafficRulesAfter(
            OmsiConfigDocument document,
            OmsiConfigSection owner)
    {
        var result =
            new List<
                OmsiTrafficRule>();

        var ownerIndex = -1;

        for (
            var index = 0;
            index <
                document.Sections.Count;
            index++)
        {
            if (
                document.Sections[index]
                    .KeywordLineIndex ==
                owner.KeywordLineIndex)
            {
                ownerIndex =
                    index;
                break;
            }
        }

        if (ownerIndex < 0)
        {
            return result;
        }

        for (
            var index =
                ownerIndex + 1;
            index <
                document.Sections.Count;
            index++)
        {
            var section =
                document.Sections[index];

            if (IsPlacementBoundary(
                    section))
            {
                break;
            }

            var isRule =
                string.Equals(
                    section.Keyword,
                    "rule",
                    StringComparison
                        .OrdinalIgnoreCase);

            var isKillRule =
                string.Equals(
                    section.Keyword,
                    "kill_rule",
                    StringComparison
                        .OrdinalIgnoreCase);

            if (
                !isRule &&
                !isKillRule)
            {
                continue;
            }

            var values =
                section.DataLines
                    .Take(4)
                    .ToArray();

            if (values.Length < 4)
            {
                continue;
            }

            int? pathIndex =
                int.TryParse(
                    values[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedPath)
                    ? parsedPath
                    : null;

            double? numericValue =
                TryParseDouble(
                    values[2],
                    out var parsedValue)
                    ? parsedValue
                    : null;

            int? vehicleGroupIndex =
                int.TryParse(
                    values[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsedGroup)
                    ? parsedGroup
                    : null;

            result.Add(
                new OmsiTrafficRule(
                    isKillRule,
                    pathIndex,
                    values[1],
                    values[2],
                    numericValue,
                    vehicleGroupIndex,
                    values));
        }

        return result;
    }

    private static bool IsPlacementBoundary(
        OmsiConfigSection section)
    {
        if (
            OmsiSplineFieldLayout
                .IsSplineSection(
                    section))
        {
            return true;
        }

        return section.Keyword
            .Equals(
                "object",
                StringComparison
                    .OrdinalIgnoreCase) ||
            section.Keyword
                .Equals(
                    "attachObj",
                    StringComparison
                        .OrdinalIgnoreCase) ||
            section.Keyword
                .Equals(
                    "splineAttachement",
                    StringComparison
                        .OrdinalIgnoreCase) ||
            section.Keyword
                .Equals(
                    "splineAttachment",
                    StringComparison
                        .OrdinalIgnoreCase) ||
            section.Keyword
                .Equals(
                    "splineAttachement_repeater",
                    StringComparison
                        .OrdinalIgnoreCase);
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
