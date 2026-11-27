using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiAttachmentReaderTests
{
    [Fact]
    public void ReadsObjectAndSplineAttachmentsWithVarParent()
    {
        const string source =
            "[version]\r\n14\r\n\r\n" +
            "[attachObj]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Pack\\Sign.sco\r\n" +
            "101\r\n" +
            "10\r\n" +
            "0\r\n" +
            "2\r\n" +
            "45\r\n" +
            "5\r\n" +
            "-2\r\n" +
            "3\r\n\r\n" +
            "[varparent]\r\n" +
            "vehicle_var\r\n\r\n" +
            "[splineAttachement]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Pack\\Lamp.sco\r\n" +
            "102\r\n" +
            "0\r\n" +
            "1.5\r\n" +
            "2.5\r\n" +
            "3.5\r\n" +
            "90\r\n" +
            "1\r\n" +
            "2\r\n" +
            "12\r\n" +
            "25\r\n" +
            "0\r\n" +
            "0\r\n\r\n" +
            "[splineAttachement_repeater]\r\n" +
            "0\r\n" +
            "a\r\n" +
            "b\r\n" +
            "Sceneryobjects\\Pack\\Fence.sco\r\n" +
            "103\r\n" +
            "0\r\n" +
            "4\r\n" +
            "5\r\n" +
            "6\r\n" +
            "180\r\n" +
            "0\r\n" +
            "0\r\n" +
            "8\r\n" +
            "16\r\n" +
            "0\r\n" +
            "0\r\n";

        var content =
            OmsiTileReader.ReadContent(
                OmsiConfigParser.Parse(
                    source));

        Assert.NotNull(
            content.Attachments);

        Assert.Equal(
            3,
            content.Attachments!
                .Count);

        Assert.Equal(
            2,
            content.Summary
                .SplineAttachmentCount);

        var attachedObject =
            content.Attachments[0];

        Assert.Equal(
            OmsiAttachmentKind
                .ObjectAttachment,
            attachedObject.Kind);

        Assert.Equal(
            101,
            attachedObject.AttachmentId);

        Assert.Equal(
            10,
            attachedObject
                .AttachedToObjectId);

        Assert.Equal(
            2,
            attachedObject
                .AttachPointIndex);

        Assert.Equal(
            3,
            attachedObject
                .LabelsCount);

        Assert.Equal(
            "vehicle_var",
            attachedObject
                .VariableParentValue);

        var spline =
            content.Attachments[1];

        Assert.Equal(
            OmsiAttachmentKind
                .SplineAttachment,
            spline.Kind);

        Assert.Equal(
            1.5,
            spline.X);

        Assert.Equal(
            2.5,
            spline.Z);

        Assert.Equal(
            3.5,
            spline.Y);

        Assert.Equal(
            12,
            spline.Interval);

        Assert.Equal(
            25,
            spline.Distance);

        var repeater =
            content.Attachments[2];

        Assert.Equal(
            OmsiAttachmentKind
                .SplineAttachmentRepeater,
            repeater.Kind);

        Assert.Equal(
            @"Sceneryobjects\Pack\Fence.sco",
            repeater.AssetPath);

        Assert.Equal(
            103,
            repeater.AttachmentId);

        Assert.Equal(
            8,
            repeater.Interval);

        Assert.Equal(
            16,
            repeater.Distance);
    }

    [Fact]
    public void MalformedAttachmentIsIgnoredWithoutBreakingNormalPlacements()
    {
        const string source =
            "[object]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Pack\\House.sco\r\n" +
            "10\r\n" +
            "1\r\n" +
            "2\r\n" +
            "3\r\n" +
            "0\r\n" +
            "0\r\n" +
            "0\r\n\r\n" +
            "[attachObj]\r\n" +
            "broken\r\n";

        var content =
            OmsiTileReader.ReadContent(
                OmsiConfigParser.Parse(
                    source));

        Assert.Single(
            content.Objects);

        Assert.NotNull(
            content.Attachments);

        Assert.Empty(
            content.Attachments!);
    }
}
