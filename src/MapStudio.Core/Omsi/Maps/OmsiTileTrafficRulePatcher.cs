using System.Globalization;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiTileTrafficRulePatcher
{
    public byte[] Patch(
        OmsiConfigDocument document,
        bool splineOwner,
        int sourceSectionOrdinal,
        IReadOnlyList<OmsiTrafficRule> rules)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            rules);

        if (
            sourceSectionOrdinal < 0 ||
            rules.Count > 4096)
        {
            throw new InvalidDataException(
                "trafficRuleOwnerInvalid");
        }

        var ownerSections =
            splineOwner
                ? document.Sections
                    .Where(
                        OmsiSplineFieldLayout
                            .IsSplineSection)
                    .ToArray()
                : document
                    .FindSections(
                        "object")
                    .ToArray();

        if (
            sourceSectionOrdinal >=
                ownerSections.Length)
        {
            throw new InvalidDataException(
                "trafficRuleOwnerInvalid");
        }

        var owner =
            ownerSections[
                sourceSectionOrdinal];

        var ownerDocumentIndex =
            -1;

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
                ownerDocumentIndex =
                    index;
                break;
            }
        }

        if (ownerDocumentIndex < 0)
        {
            throw new InvalidDataException(
                "trafficRuleOwnerInvalid");
        }

        var boundaryIndex =
            document.Sections.Count;

        for (
            var index =
                ownerDocumentIndex + 1;
            index <
                document.Sections.Count;
            index++)
        {
            if (IsPlacementBoundary(
                    document.Sections[
                        index]))
            {
                boundaryIndex =
                    index;
                break;
            }
        }

        var remove =
            new bool[
                document.Lines.Count];

        var insertionLine =
            boundaryIndex <
                document.Sections.Count
                ? document.Sections[
                    boundaryIndex]
                    .KeywordLineIndex
                : document.Lines.Count;

        for (
            var index =
                ownerDocumentIndex + 1;
            index <
                boundaryIndex;
            index++)
        {
            var section =
                document.Sections[
                    index];

            if (
                !string.Equals(
                    section.Keyword,
                    "rule",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    section.Keyword,
                    "kill_rule",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var end =
                index + 1 <
                    document.Sections.Count
                    ? document.Sections[
                        index + 1]
                        .KeywordLineIndex
                    : document.Lines.Count;

            for (
                var line =
                    section.KeywordLineIndex;
                line < end &&
                line < remove.Length;
                line++)
            {
                remove[line] =
                    true;
            }
        }

        var compacted =
            new List<string>(
                document.Lines.Count);

        var adjustedInsertion =
            0;

        for (
            var index = 0;
            index <
                document.Lines.Count;
            index++)
        {
            if (
                index <
                    insertionLine &&
                !remove[index])
            {
                adjustedInsertion++;
            }

            if (!remove[index])
            {
                compacted.Add(
                    document.Lines[
                        index]);
            }
        }

        var generated =
            BuildRuleLines(
                rules);

        if (generated.Count > 0)
        {
            if (
                adjustedInsertion >
                    0 &&
                compacted[
                    adjustedInsertion -
                    1].Length !=
                    0)
            {
                generated.Insert(
                    0,
                    string.Empty);
            }

            if (
                adjustedInsertion <
                    compacted.Count &&
                compacted[
                    adjustedInsertion].Length !=
                    0)
            {
                generated.Add(
                    string.Empty);
            }

            compacted.InsertRange(
                adjustedInsertion,
                generated);
        }

        return new OmsiConfigDocument(
            compacted,
            Array.Empty<
                OmsiConfigSection>(),
            document.NewLine,
            document.HasTrailingNewLine,
            document.TextEncoding,
            document.HasByteOrderMark)
            .ToBytes();
    }

    private static List<string> BuildRuleLines(
        IReadOnlyList<OmsiTrafficRule> rules)
    {
        var result =
            new List<string>();

        foreach (
            var rule in rules)
        {
            if (
                rule.PathIndex is not
                    int pathIndex ||
                pathIndex < 0 ||
                string.IsNullOrWhiteSpace(
                    rule.RuleName) ||
                rule.VehicleGroupIndex is not
                    int groupIndex ||
                groupIndex < 0)
            {
                throw new InvalidDataException(
                    "trafficRuleInvalid");
            }

            var value =
                string.IsNullOrWhiteSpace(
                    rule.RawValue)
                    ? rule.NumericValue
                        ?.ToString(
                            "G17",
                            CultureInfo
                                .InvariantCulture) ??
                      "0"
                    : rule.RawValue.Trim();

            result.Add(
                rule.IsKillRule
                    ? "[kill_rule]"
                    : "[rule]");

            result.Add(
                pathIndex.ToString(
                    CultureInfo.InvariantCulture));

            result.Add(
                rule.RuleName.Trim());

            result.Add(
                value);

            result.Add(
                groupIndex.ToString(
                    CultureInfo.InvariantCulture));

            result.Add(
                string.Empty);
        }

        while (
            result.Count > 0 &&
            result[^1].Length ==
                0)
        {
            result.RemoveAt(
                result.Count -
                1);
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
}
