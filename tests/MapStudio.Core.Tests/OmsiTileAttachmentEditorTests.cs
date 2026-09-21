using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileAttachmentEditorTests
{
    [Fact]
    public void EditorUpdatesKnownFieldsAndPreservesUnknownValues()
    {
        const string source =
            "[attachObj]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Pack\\sign.sco\r\n" +
            "101\r\n" +
            "42\r\n" +
            "keep-parent-mode\r\n" +
            "3\r\n" +
            "10\r\n" +
            "20\r\n" +
            "30\r\n" +
            "0\r\n" +
            "[splineAttachment]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Pack\\stop.sco\r\n" +
            "102\r\n" +
            "keep-spline-ref\r\n" +
            "1\r\n" +
            "2\r\n" +
            "3\r\n" +
            "4\r\n" +
            "5\r\n" +
            "6\r\n" +
            "7\r\n" +
            "8\r\n" +
            "keep-a\r\n" +
            "keep-b\r\n" +
            "[splineAttachment_repeater]\r\n" +
            "0\r\n" +
            "keep-1\r\n" +
            "keep-2\r\n" +
            "Sceneryobjects\\Pack\\lamp.sco\r\n" +
            "103\r\n" +
            "keep-3\r\n" +
            "9\r\n" +
            "10\r\n" +
            "11\r\n" +
            "12\r\n" +
            "13\r\n" +
            "14\r\n" +
            "15\r\n" +
            "16\r\n" +
            "keep-c\r\n" +
            "keep-d\r\n" +
            "[future_section]\r\n" +
            "keep-me\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var attachments =
            OmsiTileReader
                .ReadAttachments(
                    document);

        Assert.Equal(
            3,
            attachments.Count);

        var result =
            OmsiTileAttachmentEditor
                .ApplyTransforms(
                    document,
                    [
                        CreateEdit(
                            attachments[0],
                            rotation:
                                40,
                            pitch:
                                50,
                            bank:
                                60),
                        CreateEdit(
                            attachments[1],
                            x:
                                21,
                            z:
                                22,
                            y:
                                23,
                            rotation:
                                24,
                            pitch:
                                25,
                            bank:
                                26,
                            interval:
                                27,
                            distance:
                                28),
                        CreateEdit(
                            attachments[2],
                            x:
                                31,
                            z:
                                32,
                            y:
                                33,
                            rotation:
                                34,
                            pitch:
                                35,
                            bank:
                                36,
                            interval:
                                37,
                            distance:
                                38)
                    ]);

        Assert.Equal(
            3,
            result.AppliedCount);

        var edited =
            OmsiConfigParser
                .ParseBytes(
                    result.Bytes);

        var parsed =
            OmsiTileReader
                .ReadAttachments(
                    edited);

        Assert.Equal(
            40,
            parsed[0].Rotation);

        Assert.Equal(
            50,
            parsed[0].Pitch);

        Assert.Equal(
            60,
            parsed[0].Bank);

        Assert.Equal(
            21,
            parsed[1].X);

        Assert.Equal(
            28,
            parsed[1].Distance);

        Assert.Equal(
            31,
            parsed[2].X);

        Assert.Equal(
            38,
            parsed[2].Distance);

        var text =
            edited.ToText();

        Assert.Contains(
            "keep-parent-mode",
            text);

        Assert.Contains(
            "keep-spline-ref",
            text);

        Assert.Contains(
            "keep-a",
            text);

        Assert.Contains(
            "keep-b",
            text);

        Assert.Contains(
            "keep-c",
            text);

        Assert.Contains(
            "keep-d",
            text);

        Assert.Contains(
            "[future_section]\r\nkeep-me",
            text);
    }

    [Fact]
    public void EditorRejectsChangedSourceSection()
    {
        const string source =
            "[attachObj]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Pack\\sign.sco\r\n" +
            "101\r\n" +
            "42\r\n" +
            "mode\r\n" +
            "3\r\n" +
            "10\r\n" +
            "20\r\n" +
            "30\r\n" +
            "0\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var attachment =
            Assert.Single(
                OmsiTileReader
                    .ReadAttachments(
                        document));

        var changed =
            OmsiConfigParser.Parse(
                source.Replace(
                    "\r\n10\r\n20\r\n",
                    "\r\n11\r\n20\r\n",
                    StringComparison.Ordinal));

        Assert.Throws<
            InvalidDataException>(
                () =>
                    OmsiTileAttachmentEditor
                        .ApplyTransforms(
                            changed,
                            [
                                CreateEdit(
                                    attachment,
                                    rotation:
                                        40)
                            ]));
    }

    private static OmsiAttachmentTransformEdit
        CreateEdit(
            OmsiPlacedAttachment attachment,
            double? x = null,
            double? z = null,
            double? y = null,
            double? rotation = null,
            double? pitch = null,
            double? bank = null,
            double? interval = null,
            double? distance = null) =>
        new(
            attachment.SourceSectionOrdinal,
            attachment.Kind,
            attachment.AttachmentId,
            attachment.AssetPath,
            attachment.RawValues,
            x ??
                attachment.X,
            z ??
                attachment.Z,
            y ??
                attachment.Y,
            rotation ??
                attachment.Rotation,
            pitch ??
                attachment.Pitch,
            bank ??
                attachment.Bank,
            interval ??
                attachment.Interval,
            distance ??
                attachment.Distance);
}
