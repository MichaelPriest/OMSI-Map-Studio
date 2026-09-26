using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileAttachmentEditor
{
    public static OmsiTileAttachmentEditResult ApplyTransforms(
        OmsiConfigDocument document,
        IReadOnlyList<OmsiAttachmentTransformEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(edits);

        if (edits.Count == 0)
        {
            return new OmsiTileAttachmentEditResult(
                document.ToBytes(),
                0);
        }

        var lines =
            document.Lines.ToArray();

        var sections =
            document.Sections
                .Where(IsAttachmentSection)
                .ToArray();

        var usedOrdinals =
            new HashSet<int>();

        var applied =
            0;

        foreach (var edit in edits)
        {
            if (
                edit.SourceSectionOrdinal < 0 ||
                edit.SourceSectionOrdinal >=
                    sections.Length ||
                !usedOrdinals.Add(
                    edit.SourceSectionOrdinal) ||
                !IsFinite(edit))
            {
                throw new InvalidDataException(
                    "invalidAttachmentSection");
            }

            var section =
                sections[
                    edit.SourceSectionOrdinal];

            if (
                !TryGetLayout(
                    section.Keyword,
                    out var kind,
                    out var pathIndex,
                    out var idIndex,
                    out var xIndex,
                    out var zIndex,
                    out var yIndex,
                    out var rotationIndex,
                    out var pitchIndex,
                    out var bankIndex,
                    out var intervalIndex,
                    out var distanceIndex,
                    out var minimumValueCount) ||
                kind !=
                    edit.Kind)
            {
                throw new InvalidDataException(
                    "attachmentKindChanged");
            }

            var dataLineIndices =
                GetDataLineIndices(
                    section);

            if (
                dataLineIndices.Count <
                    minimumValueCount)
            {
                throw new InvalidDataException(
                    "malformedAttachmentSection");
            }

            var dataValues =
                dataLineIndices
                    .Select(
                        index =>
                            lines[index]
                                .Trim())
                    .ToArray();

            if (
                !dataValues.SequenceEqual(
                    edit.ExpectedRawValues,
                    StringComparer.Ordinal) ||
                !int.TryParse(
                    dataValues[idIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var currentId) ||
                currentId !=
                    edit.AttachmentId ||
                !string.Equals(
                    dataValues[pathIndex],
                    edit.AssetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "attachmentSourceChanged");
            }

            if (
                xIndex is int xi &&
                zIndex is int zi &&
                yIndex is int yi)
            {
                if (
                    edit.X is not
                        { } x ||
                    edit.Z is not
                        { } z ||
                    edit.Y is not
                        { } y)
                {
                    throw new InvalidDataException(
                        "attachmentPositionRequired");
                }

                lines[
                    dataLineIndices[xi]] =
                    Format(x);

                lines[
                    dataLineIndices[zi]] =
                    Format(z);

                lines[
                    dataLineIndices[yi]] =
                    Format(y);
            }

            lines[
                dataLineIndices[
                    rotationIndex]] =
                Format(
                    edit.Rotation);

            lines[
                dataLineIndices[
                    pitchIndex]] =
                Format(
                    edit.Pitch);

            lines[
                dataLineIndices[
                    bankIndex]] =
                Format(
                    edit.Bank);

            if (
                intervalIndex is
                    int intervalField)
            {
                if (
                    edit.Interval is not
                        { } interval)
                {
                    throw new InvalidDataException(
                        "attachmentIntervalRequired");
                }

                lines[
                    dataLineIndices[
                        intervalField]] =
                    Format(
                        interval);
            }

            if (
                distanceIndex is
                    int distanceField)
            {
                if (
                    edit.Distance is not
                        { } distance)
                {
                    throw new InvalidDataException(
                        "attachmentDistanceRequired");
                }

                lines[
                    dataLineIndices[
                        distanceField]] =
                    Format(
                        distance);
            }

            applied++;
        }

        return new OmsiTileAttachmentEditResult(
            Encode(
                document,
                lines),
            applied);
    }

    private static bool IsAttachmentSection(
        OmsiConfigSection section) =>
        section.Keyword.Equals(
            "attachObj",
            StringComparison.OrdinalIgnoreCase) ||
        section.Keyword.Equals(
            "splineAttachement",
            StringComparison.OrdinalIgnoreCase) ||
        section.Keyword.Equals(
            "splineAttachment",
            StringComparison.OrdinalIgnoreCase) ||
        section.Keyword.Equals(
            "splineAttachement_repeater",
            StringComparison.OrdinalIgnoreCase) ||
        section.Keyword.Equals(
            "splineAttachment_repeater",
            StringComparison.OrdinalIgnoreCase);

    private static bool TryGetLayout(
        string keyword,
        out OmsiAttachmentKind kind,
        out int pathIndex,
        out int idIndex,
        out int? xIndex,
        out int? zIndex,
        out int? yIndex,
        out int rotationIndex,
        out int pitchIndex,
        out int bankIndex,
        out int? intervalIndex,
        out int? distanceIndex,
        out int minimumValueCount)
    {
        if (
            keyword.Equals(
                "attachObj",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                OmsiAttachmentKind
                    .ObjectAttachment;

            pathIndex = 1;
            idIndex = 2;
            xIndex = null;
            zIndex = null;
            yIndex = null;
            rotationIndex = 6;
            pitchIndex = 7;
            bankIndex = 8;
            intervalIndex = null;
            distanceIndex = null;
            minimumValueCount = 10;

            return true;
        }

        if (
            keyword.Equals(
                "splineAttachement",
                StringComparison.OrdinalIgnoreCase) ||
            keyword.Equals(
                "splineAttachment",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                OmsiAttachmentKind
                    .SplineAttachment;

            pathIndex = 1;
            idIndex = 2;
            xIndex = 4;
            zIndex = 5;
            yIndex = 6;
            rotationIndex = 7;
            pitchIndex = 8;
            bankIndex = 9;
            intervalIndex = 10;
            distanceIndex = 11;
            minimumValueCount = 14;

            return true;
        }

        if (
            keyword.Equals(
                "splineAttachement_repeater",
                StringComparison.OrdinalIgnoreCase) ||
            keyword.Equals(
                "splineAttachment_repeater",
                StringComparison.OrdinalIgnoreCase))
        {
            kind =
                OmsiAttachmentKind
                    .SplineAttachmentRepeater;

            pathIndex = 3;
            idIndex = 4;
            xIndex = 6;
            zIndex = 7;
            yIndex = 8;
            rotationIndex = 9;
            pitchIndex = 10;
            bankIndex = 11;
            intervalIndex = 12;
            distanceIndex = 13;
            minimumValueCount = 16;

            return true;
        }

        kind =
            default;

        pathIndex = -1;
        idIndex = -1;
        xIndex = null;
        zIndex = null;
        yIndex = null;
        rotationIndex = -1;
        pitchIndex = -1;
        bankIndex = -1;
        intervalIndex = null;
        distanceIndex = null;
        minimumValueCount = 0;

        return false;
    }

    private static List<int> GetDataLineIndices(
        OmsiConfigSection section)
    {
        var result =
            new List<int>();

        for (
            var offset = 0;
            offset <
                section.RawBodyLines.Count;
            offset++)
        {
            var value =
                section.RawBodyLines[
                    offset]
                    .Trim();

            if (
                value.Length == 0 ||
                value.StartsWith(
                    '#'))
            {
                continue;
            }

            result.Add(
                section.KeywordLineIndex +
                1 +
                offset);
        }

        return result;
    }

    private static bool IsFinite(
        OmsiAttachmentTransformEdit edit)
    {
        if (
            !double.IsFinite(
                edit.Rotation) ||
            !double.IsFinite(
                edit.Pitch) ||
            !double.IsFinite(
                edit.Bank))
        {
            return false;
        }

        if (
            edit.Kind ==
            OmsiAttachmentKind
                .ObjectAttachment)
        {
            return true;
        }

        return
            edit.X is { } x &&
            edit.Z is { } z &&
            edit.Y is { } y &&
            edit.Interval is
                { } interval &&
            edit.Distance is
                { } distance &&
            double.IsFinite(x) &&
            double.IsFinite(z) &&
            double.IsFinite(y) &&
            double.IsFinite(
                interval) &&
            double.IsFinite(
                distance);
    }

    private static string Format(
        double value) =>
        value.ToString(
            "G17",
            CultureInfo.InvariantCulture);

    private static byte[] Encode(
        OmsiConfigDocument document,
        IReadOnlyList<string> lines)
    {
        var text =
            string.Join(
                document.NewLine,
                lines);

        if (
            document.HasTrailingNewLine &&
            lines.Count > 0)
        {
            text +=
                document.NewLine;
        }

        var body =
            document.TextEncoding
                .GetBytes(text);

        if (!document.HasByteOrderMark)
        {
            return body;
        }

        var preamble =
            document.TextEncoding
                .GetPreamble();

        if (preamble.Length == 0)
        {
            return body;
        }

        var result =
            new byte[
                preamble.Length +
                body.Length];

        Buffer.BlockCopy(
            preamble,
            0,
            result,
            0,
            preamble.Length);

        Buffer.BlockCopy(
            body,
            0,
            result,
            preamble.Length,
            body.Length);

        return result;
    }
}
