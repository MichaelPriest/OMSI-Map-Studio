using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Splines;

public sealed class OmsiSplinePathPatcher
{
    public byte[] Patch(
        OmsiConfigDocument document,
        int sourcePathOrdinal,
        OmsiSplinePathDefinition path)
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
                "splinePathOrdinalInvalid");
        }

        var pathSections =
            document.Sections
                .Where(
                    section =>
                        string.Equals(
                            section.Keyword,
                            "path",
                            StringComparison.OrdinalIgnoreCase))
                .ToArray();

        if (
            sourcePathOrdinal >=
                pathSections.Length)
        {
            throw new InvalidDataException(
                "splinePathOrdinalInvalid");
        }

        var target =
            pathSections[
                sourcePathOrdinal];

        var sectionIndex =
            -1;

        for (
            var index = 0;
            index <
                document.Sections.Count;
            index++)
        {
            if (
                ReferenceEquals(
                    document.Sections[
                        index],
                    target))
            {
                sectionIndex =
                    index;
                break;
            }
        }

        if (sectionIndex < 0)
        {
            throw new InvalidDataException(
                "splinePathOrdinalInvalid");
        }

        var bodyEnd =
            sectionIndex + 1 <
                document.Sections.Count
                ? document.Sections[
                    sectionIndex + 1]
                    .KeywordLineIndex
                : document.Lines.Count;

        var dataLineIndexes =
            new List<int>(
                5);

        for (
            var lineIndex =
                target.KeywordLineIndex +
                1;
            lineIndex <
                bodyEnd;
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

            dataLineIndexes.Add(
                lineIndex);

            if (
                dataLineIndexes.Count ==
                    5)
            {
                break;
            }
        }

        if (
            dataLineIndexes.Count <
                5)
        {
            throw new InvalidDataException(
                "splinePathDataInvalid");
        }

        var serialized =
            new[]
            {
                path.Type.ToString(
                    CultureInfo.InvariantCulture),
                FormatDouble(
                    path.X),
                FormatDouble(
                    path.Z),
                FormatDouble(
                    path.Width),
                path.Direction.ToString(
                    CultureInfo.InvariantCulture)
            };

        var lines =
            document.Lines
                .ToArray();

        for (
            var index = 0;
            index <
                serialized.Length;
            index++)
        {
            lines[
                dataLineIndexes[
                    index]] =
                serialized[
                    index];
        }

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

    public byte[] AppendDuplicate(
        OmsiConfigDocument document,
        int sourcePathOrdinal,
        OmsiSplinePathDefinition path)
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
                "splinePathOrdinalInvalid");
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
                "splinePathOrdinalInvalid");
        }

        var lastPath =
            pathSections[^1];

        var insertAt =
            lastPath.DocumentIndex + 1 <
                document.Sections.Count
                ? document.Sections[
                    lastPath.DocumentIndex + 1]
                    .KeywordLineIndex
                : document.Lines.Count;

        var generated =
            new List<string>
            {
                "[path]",
                path.Type.ToString(
                    CultureInfo.InvariantCulture),
                FormatDouble(
                    path.X),
                FormatDouble(
                    path.Z),
                FormatDouble(
                    path.Width),
                path.Direction.ToString(
                    CultureInfo.InvariantCulture)
            };

        var lines =
            document.Lines
                .ToList();

        if (
            insertAt >
                0 &&
            lines[
                insertAt -
                    1].Length !=
                0)
        {
            generated.Insert(
                0,
                string.Empty);
        }

        if (
            insertAt <
                lines.Count &&
            generated[^1].Length !=
                0)
        {
            generated.Add(
                string.Empty);
        }

        lines.InsertRange(
            insertAt,
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
        OmsiSplinePathDefinition path)
    {
        if (
            path.Type is
                < 0 or
                > 3 ||
            path.Direction is
                < 0 or
                > 2 ||
            !double.IsFinite(
                path.X) ||
            !double.IsFinite(
                path.Z) ||
            !double.IsFinite(
                path.Width) ||
            path.Width <
                0)
        {
            throw new InvalidDataException(
                "splinePathInvalid");
        }
    }

    private static string FormatDouble(
        double value) =>
        value.ToString(
            "G17",
            CultureInfo.InvariantCulture);
}
