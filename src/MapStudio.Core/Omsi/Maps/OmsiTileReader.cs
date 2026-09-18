using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiTileReader
{
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

        var attachmentCount =
            document.FindSections("splineAttachement").Count() +
            document.FindSections("splineAttachment").Count();

        var summary = new OmsiTileSummary(
            Exists: true,
            ObjectCount:
                document.FindSections("object").Count(),
            SplineCount:
                document.FindSections("spline").Count(),
            SplineAttachmentCount:
                attachmentCount);

        return new OmsiTileContent(
            summary,
            ReadObjects(document));
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

        foreach (var section in document.FindSections("object"))
        {
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
                    values.Skip(9).ToArray()));
        }

        return objects;
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
