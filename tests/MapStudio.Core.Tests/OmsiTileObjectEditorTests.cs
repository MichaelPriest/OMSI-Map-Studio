using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileObjectEditorTests
{
    private const string Source =
        "[object]\r\n" +
        "0\r\n" +
        "Sceneryobjects\\Pack\\tree.sco\r\n" +
        "1\r\n" +
        "10\r\n" +
        "20\r\n" +
        "30\r\n" +
        "0\r\n" +
        "0\r\n" +
        "0\r\n" +
        "[future_section]\r\n" +
        "preserve-this\r\n";

    [Fact]
    public void ApplyTransforms_RejectsNaNAndInfinity()
    {
        var document = OmsiConfigParser.Parse(Source);

        var invalidEdits = new[]
        {
            CreateEdit(x: double.NaN),
            CreateEdit(y: double.PositiveInfinity),
            CreateEdit(z: double.NegativeInfinity),
            CreateEdit(rotation: double.NaN),
            CreateEdit(pitch: double.PositiveInfinity),
            CreateEdit(bank: double.NegativeInfinity)
        };

        foreach (var edit in invalidEdits)
        {
            var error = Assert.Throws<InvalidDataException>(
                () => OmsiTileObjectEditor.ApplyTransforms(document, [edit]));
            Assert.Equal("invalidObjectTransform", error.Message);
        }

        // Rejected edits must not mutate the parsed source document.
        Assert.Contains("preserve-this", document.ToText());
        Assert.Contains("\r\n10\r\n20\r\n30\r\n", document.ToText());
    }

    [Fact]
    public void ApplyTransforms_UpdatesFiniteValuesAndPreservesUnknownSections()
    {
        var document = OmsiConfigParser.Parse(Source);

        var result = OmsiTileObjectEditor.ApplyTransforms(
            document,
            [CreateEdit(x: 11.5, y: 22.5, z: 33.5, rotation: 45)]);

        Assert.Equal(1, result.AppliedCount);
        var edited = OmsiConfigParser.ParseBytes(result.Bytes);
        var text = edited.ToText();

        Assert.Contains("\r\n11.5\r\n22.5\r\n33.5\r\n45\r\n", text);
        Assert.Contains("[future_section]\r\npreserve-this", text);
    }

    private static OmsiObjectTransformEdit CreateEdit(
        double x = 10,
        double y = 20,
        double z = 30,
        double rotation = 0,
        double pitch = 0,
        double bank = 0) =>
        new(
            SourceSectionOrdinal: 0,
            SceneryObjectPath: "Sceneryobjects\\Pack\\tree.sco",
            ObjectId: 1,
            X: x,
            Y: y,
            Z: z,
            Rotation: rotation,
            Pitch: pitch,
            Bank: bank);
}
