using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiTileReader
{
    public async Task<OmsiTileSummary> ReadSummaryAsync(
        string tilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tilePath);

        if (!File.Exists(tilePath))
        {
            return OmsiTileSummary.Missing;
        }

        var document = await OmsiConfigParser.ParseFileAsync(tilePath, cancellationToken);

        var attachmentCount =
            document.FindSections("splineAttachement").Count() +
            document.FindSections("splineAttachment").Count();

        return new OmsiTileSummary(
            Exists: true,
            ObjectCount: document.FindSections("object").Count(),
            SplineCount: document.FindSections("spline").Count(),
            SplineAttachmentCount: attachmentCount);
    }
}
