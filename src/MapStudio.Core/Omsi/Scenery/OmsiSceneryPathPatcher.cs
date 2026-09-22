using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Scenery;

public sealed class OmsiSceneryPathPatcher
{
    private static readonly HashSet<string>
        KnownPathModifierKeywords =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "use_traffic_light",
                "switchdir",
                "crossingproblem"
            };

    private static readonly HashSet<string>
        PathBoundaryKeywords =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "path",
                "mesh",
                "tree",
                "traffic_lights_group",
                "trafficlight_group"
            };

    public byte[] Patch(
        OmsiConfigDocument document,
        int sourcePathOrdinal,
        OmsiSceneryPathDefinition path)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            path);

        ValidatePath(
            path);

        if (sourcePathOrdinal < 0)
        {
            throw new InvalidDataException(
                "sceneryPathOrdinalInvalid");
        }

        var pathSections =
            document.Sections
                .Select(
                    (
                        section,
                        documentIndex
                    ) =>
                        (
                            Section:
                                section,
                            DocumentIndex:
                                documentIndex
                        ))
                .Where(
                    value =>
                        string.Equals(
                            value.Section.Keyword,
                            "path",
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (
            sourcePathOrdinal >=
                pathSections.Length)
        {
            throw new InvalidDataException(
                "sceneryPathOrdinalInvalid");
        }

        var target =
            pathSections[
                sourcePathOrdinal];

        var targetBodyStart =
            target.Section.KeywordLineIndex +
            1;

        var targetBodyEnd =
            target.DocumentIndex + 1 <
                document.Sections.Count
                ? document.Sections[
                    target.DocumentIndex + 1]
                    .KeywordLineIndex
                : document.Lines.Count;

        var pathDataLineIndexes =
            new List<int>(
                12);

        for (
            var lineIndex =
                targetBodyStart;
            lineIndex <
                targetBodyEnd;
            lineIndex++)
        {
            var value =
                document.Lines[
                    lineIndex]
                    .Trim();

            if (
                value.Length ==
                    0 ||
                value.StartsWith(
                    '#'))
            {
                continue;
            }

            pathDataLineIndexes.Add(
                lineIndex);

            if (
                pathDataLineIndexes.Count ==
                    12)
            {
                break;
            }
        }

        if (
            pathDataLineIndexes.Count <
                12)
        {
            throw new InvalidDataException(
                "sceneryPathDataInvalid");
        }

        var serialized =
            SerializePathValues(
                path);

        var replacements =
            new Dictionary<
                int,
                string>();

        for (
            var valueIndex = 0;
            valueIndex <
                serialized.Count;
            valueIndex++)
        {
            replacements[
                pathDataLineIndexes[
                    valueIndex]] =
                serialized[
                    valueIndex];
        }

        var groupEndSectionIndex =
            document.Sections.Count;

        for (
            var sectionIndex =
                target.DocumentIndex +
                1;
            sectionIndex <
                document.Sections.Count;
            sectionIndex++)
        {
            if (
                PathBoundaryKeywords.Contains(
                    document.Sections[
                        sectionIndex]
                        .Keyword))
            {
                groupEndSectionIndex =
                    sectionIndex;
                break;
            }
        }

        var groupEndLine =
            groupEndSectionIndex <
                document.Sections.Count
                ? document.Sections[
                    groupEndSectionIndex]
                    .KeywordLineIndex
                : document.Lines.Count;

        var remove =
            new bool[
                document.Lines.Count];

        var firstKnownModifierLine =
            -1;

        for (
            var sectionIndex =
                target.DocumentIndex +
                1;
            sectionIndex <
                groupEndSectionIndex;
            sectionIndex++)
        {
            var section =
                document.Sections[
                    sectionIndex];

            if (
                !KnownPathModifierKeywords
                    .Contains(
                        section.Keyword))
            {
                continue;
            }

            if (
                firstKnownModifierLine <
                    0)
            {
                firstKnownModifierLine =
                    section.KeywordLineIndex;
            }

            var end =
                sectionIndex + 1 <
                    document.Sections.Count
                    ? document.Sections[
                        sectionIndex + 1]
                        .KeywordLineIndex
                    : document.Lines.Count;

            for (
                var lineIndex =
                    section.KeywordLineIndex;
                lineIndex <
                    end &&
                lineIndex <
                    remove.Length;
                lineIndex++)
            {
                remove[
                    lineIndex] =
                    true;
            }
        }

        var insertionLine =
            firstKnownModifierLine >=
                0
                ? firstKnownModifierLine
                : groupEndLine;

        var compacted =
            new List<string>(
                document.Lines.Count +
                12);

        var adjustedInsertion =
            0;

        for (
            var lineIndex = 0;
            lineIndex <
                document.Lines.Count;
            lineIndex++)
        {
            if (
                lineIndex <
                    insertionLine &&
                !remove[
                    lineIndex])
            {
                adjustedInsertion++;
            }

            if (
                remove[
                    lineIndex])
            {
                continue;
            }

            compacted.Add(
                replacements.TryGetValue(
                    lineIndex,
                    out var replacement)
                    ? replacement
                    : document.Lines[
                        lineIndex]);
        }

        var modifiers =
            SerializeKnownModifiers(
                path);

        if (
            modifiers.Count >
                0)
        {
            if (
                adjustedInsertion >
                    0 &&
                compacted[
                    adjustedInsertion -
                    1].Length !=
                    0)
            {
                modifiers.Insert(
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
                modifiers.Add(
                    string.Empty);
            }

            compacted.InsertRange(
                adjustedInsertion,
                modifiers);
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

    public byte[] AppendDuplicate(
        OmsiConfigDocument document,
        int sourcePathOrdinal,
        OmsiSceneryPathDefinition path)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        ArgumentNullException.ThrowIfNull(
            path);

        ValidatePath(
            path);

        if (sourcePathOrdinal < 0)
        {
            throw new InvalidDataException(
                "sceneryPathOrdinalInvalid");
        }

        var pathSections =
            document.Sections
                .Select(
                    (
                        section,
                        documentIndex
                    ) =>
                        (
                            Section:
                                section,
                            DocumentIndex:
                                documentIndex
                        ))
                .Where(
                    value =>
                        string.Equals(
                            value.Section.Keyword,
                            "path",
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (
            sourcePathOrdinal >=
                pathSections.Length)
        {
            throw new InvalidDataException(
                "sceneryPathOrdinalInvalid");
        }

        var lastPath =
            pathSections[^1];

        var groupEndLine =
            document.Lines.Count;

        for (
            var sectionIndex =
                lastPath.DocumentIndex +
                1;
            sectionIndex <
                document.Sections.Count;
            sectionIndex++)
        {
            if (
                PathBoundaryKeywords.Contains(
                    document.Sections[
                        sectionIndex]
                        .Keyword))
            {
                groupEndLine =
                    document.Sections[
                        sectionIndex]
                        .KeywordLineIndex;

                break;
            }
        }

        var generated =
            new List<string>
            {
                "[path]"
            };

        generated.AddRange(
            SerializePathValues(
                path));

        var modifiers =
            SerializeKnownModifiers(
                path);

        if (
            modifiers.Count >
                0)
        {
            generated.Add(
                string.Empty);

            generated.AddRange(
                modifiers);
        }

        var lines =
            document.Lines
                .ToList();

        if (
            groupEndLine >
                0 &&
            lines[
                groupEndLine -
                    1].Length !=
                0)
        {
            generated.Insert(
                0,
                string.Empty);
        }

        if (
            groupEndLine <
                lines.Count &&
            generated.Count >
                0 &&
            generated[^1].Length !=
                0)
        {
            generated.Add(
                string.Empty);
        }

        lines.InsertRange(
            groupEndLine,
            generated);

        return new OmsiConfigDocument(
            lines,
            Array.Empty<
                OmsiConfigSection>(),
            document.NewLine,
            document.HasTrailingNewLine,
            document.TextEncoding,
            document.HasByteOrderMark)
            .ToBytes();
    }

    private static void ValidatePath(
        OmsiSceneryPathDefinition path)
    {
        var values =
            new[]
            {
                path.X,
                path.Y,
                path.Z,
                path.Rotation,
                path.Radius,
                path.Length,
                path.GradientStart,
                path.GradientEnd,
                path.Width
            };

        if (
            values.Any(
                value =>
                    !double.IsFinite(
                        value)) ||
            path.Length <
                0 ||
            path.Width <
                0 ||
            path.TrafficLightIndex is
                < 0)
        {
            throw new InvalidDataException(
                "sceneryPathInvalid");
        }
    }

    private static IReadOnlyList<string>
        SerializePathValues(
            OmsiSceneryPathDefinition path) =>
        [
            FormatDouble(
                path.X),
            FormatDouble(
                path.Y),
            FormatDouble(
                path.Z),
            FormatDouble(
                path.Rotation),
            FormatDouble(
                path.Radius),
            FormatDouble(
                path.Length),
            FormatDouble(
                path.GradientStart),
            FormatDouble(
                path.GradientEnd),
            path.Type.ToString(
                CultureInfo.InvariantCulture),
            FormatDouble(
                path.Width),
            path.Direction.ToString(
                CultureInfo.InvariantCulture),
            path.BlinkerCode.ToString(
                CultureInfo.InvariantCulture)
        ];

    private static List<string>
        SerializeKnownModifiers(
            OmsiSceneryPathDefinition path)
    {
        var result =
            new List<string>();

        if (
            path.TrafficLightIndex is
                int trafficLightIndex)
        {
            result.Add(
                "[use_traffic_light]");

            result.Add(
                trafficLightIndex.ToString(
                    CultureInfo.InvariantCulture));

            result.Add(
                string.Empty);
        }

        if (
            path.SwitchDirection is
                int switchDirection)
        {
            result.Add(
                "[switchdir]");

            result.Add(
                switchDirection.ToString(
                    CultureInfo.InvariantCulture));

            result.Add(
                string.Empty);
        }

        if (path.CrossingProblem)
        {
            result.Add(
                "[crossingproblem]");

            result.Add(
                string.Empty);
        }

        while (
            result.Count >
                0 &&
            result[^1].Length ==
                0)
        {
            result.RemoveAt(
                result.Count -
                1);
        }

        return result;
    }

    private static string FormatDouble(
        double value) =>
        value.ToString(
            "G17",
            CultureInfo.InvariantCulture);
}
