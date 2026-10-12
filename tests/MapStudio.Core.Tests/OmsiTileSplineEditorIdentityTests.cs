using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileSplineEditorIdentityTests
{
    private const string Source =
        "[version]\r\n" +
        "14\r\n" +
        "[spline]\r\n" +
        "0\r\n" +
        "Splines\\Pack\\road.sli\r\n" +
        "1\r\n" +
        "-1\r\n" +
        "-1\r\n" +
        "10\r\n" +
        "30\r\n" +
        "20\r\n" +
        "0\r\n" +
        "100\r\n" +
        "0\r\n" +
        "0\r\n" +
        "0\r\n" +
        "[future_section]\r\n" +
        "preserve-this\r\n";

    [Fact]
    public void ApplyTransforms_RejectsInvalidSplineIdentity()
    {
        var document = OmsiConfigParser.Parse(Source);
        var invalidEdits = new[]
        {
            CreateEdit() with { SplineId = -1 },
            CreateEdit() with { SplinePath = "" },
            CreateEdit() with { SplinePath = "   " }
        };

        foreach (var edit in invalidEdits)
        {
            var error = Assert.Throws<InvalidDataException>(
                () => OmsiTileSplineEditor.ApplyTransforms(document, [edit]));
            Assert.Equal("invalidSplineTransform", error.Message);
        }

        Assert.Contains("preserve-this", document.ToText());
        Assert.Contains("\r\n10\r\n30\r\n20\r\n", document.ToText());
    }

    [Fact]
    public void ApplyTransforms_UpdatesSplineAndPreservesUnknownSections()
    {
        var document = OmsiConfigParser.Parse(Source);

        var result = OmsiTileSplineEditor.ApplyTransforms(
            document,
            [CreateEdit(x: 15)]);

        Assert.Equal(1, result.AppliedCount);
        var edited = OmsiConfigParser.ParseBytes(result.Bytes);
        Assert.Contains("\\r\\n15\\r\\n30\\r\\n20\\r\\n", edited.ToText());
        Assert.Contains("[future_section]\\r\\npreserve-this", edited.ToText());
    }

    [Fact]
    public void ApplyTransforms_StillRejectsNonFiniteValuesWithExistingError()
    {
        var document = OmsiConfigParser.Parse(Source);
        var error = Assert.Throws<InvalidDataException>(
            () => OmsiTileSplineEditor.ApplyTransforms(
                document,
                [CreateEdit() with { X = double.NaN }]));

        Assert.Equal("invalidSplineSection", error.Message);
    }

    private static OmsiSplineTransformEdit CreateEdit(
        double x = 10) =>
        new(
            SourceSectionOrdinal: 0,
            SplinePath: "Splines\\Pack\\road.sli",
            SplineId: 1,
            PreviousSplineId: -1,
            NextSplineId: -1,
            IsHeightSpline: false,
            X: x,
            Z: 30,
            Y: 20,
            Rotation: 0,
            Length: 100,
            Radius: 0,
            GradientStart: 0,
            GradientEnd: 0);
}
