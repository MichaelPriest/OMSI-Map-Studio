using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileElementIdScanner
{
    private static readonly IReadOnlyDictionary<
        string,
        int>
        IdDataLineByKeyword =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["object"] = 2,
                ["attachObj"] = 2,
                ["splineAttachement"] = 2,
                ["splineAttachment"] = 2,
                ["splineAttachement_repeater"] = 4,
                ["splineAttachment_repeater"] = 4,
                ["spline"] = 2,
                ["spline_h"] = 2
            };

    public static int FindMaxUsedId(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var maxId = 0;

        foreach (var section in
            document.Sections)
        {
            if (!IdDataLineByKeyword
                .TryGetValue(
                    section.Keyword,
                    out var dataIndex))
            {
                continue;
            }

            var dataLines =
                section.DataLines.ToArray();

            if (
                dataLines.Length <=
                    dataIndex ||
                !int.TryParse(
                    dataLines[
                        dataIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var id))
            {
                continue;
            }

            if (id > maxId)
            {
                maxId = id;
            }
        }

        return maxId;
    }
}
