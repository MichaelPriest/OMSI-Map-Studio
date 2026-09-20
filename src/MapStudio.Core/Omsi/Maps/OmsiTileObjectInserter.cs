using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTileObjectInserter
{
    public static OmsiTileObjectInsertResult Append(
        OmsiConfigDocument document,
        OmsiNewPlacedObject placedObject)
    {
        var batch =
            AppendMany(
                document,
                [placedObject]);

        return new OmsiTileObjectInsertResult(
            batch.Bytes,
            batch.SourceSectionOrdinals[0]);
    }

    public static OmsiTileObjectBatchInsertResult AppendMany(
        OmsiConfigDocument document,
        IReadOnlyList<OmsiNewPlacedObject> placedObjects)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(placedObjects);

        if (placedObjects.Count == 0)
        {
            throw new InvalidDataException(
                "invalidNewObjectBatch");
        }

        foreach (var placedObject in placedObjects)
        {
            Validate(placedObject);
        }

        var lines =
            document.Lines.ToList();

        var sourceSectionOrdinal =
            document
                .FindSections("object")
                .Count();

        var ordinals =
            new List<int>(
                placedObjects.Count);

        foreach (var placedObject in placedObjects)
        {
            if (
                lines.Count > 0 &&
                lines[^1].Length != 0)
            {
                lines.Add(string.Empty);
            }

            ordinals.Add(
                sourceSectionOrdinal++);

            lines.Add("[object]");
            lines.Add(
                placedObject.HeaderValue);
            lines.Add(
                placedObject
                    .SceneryObjectPath);
            lines.Add(
                placedObject.ObjectId
                    .ToString(
                        CultureInfo.InvariantCulture));
            lines.Add(Format(placedObject.X));
            lines.Add(Format(placedObject.Y));
            lines.Add(Format(placedObject.Z));
            lines.Add(
                Format(
                    placedObject.Rotation));
            lines.Add(
                Format(
                    placedObject.Pitch));
            lines.Add(
                Format(
                    placedObject.Bank));

            foreach (var extra in
                placedObject.ExtraValues)
            {
                lines.Add(extra);
            }
        }

        return new OmsiTileObjectBatchInsertResult(
            Encode(document, lines),
            ordinals);
    }

    private static void Validate(
        OmsiNewPlacedObject placedObject)
    {
        ArgumentNullException.ThrowIfNull(
            placedObject);

        if (string.IsNullOrWhiteSpace(
                placedObject.HeaderValue) ||
            string.IsNullOrWhiteSpace(
                placedObject.SceneryObjectPath) ||
            placedObject.ObjectId <= 0 ||
            !IsFinite(placedObject))
        {
            throw new InvalidDataException(
                "invalidNewObject");
        }
    }

    private static bool IsFinite(
        OmsiNewPlacedObject value) =>
        double.IsFinite(value.X) &&
        double.IsFinite(value.Y) &&
        double.IsFinite(value.Z) &&
        double.IsFinite(
            value.Rotation) &&
        double.IsFinite(value.Pitch) &&
        double.IsFinite(value.Bank);

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
            text += document.NewLine;
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
