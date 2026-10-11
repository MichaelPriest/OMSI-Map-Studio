using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Viewport;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeTransformEditValidatorTests
{
    [Fact]
    public void ObjectEdit_RejectsNonFiniteValues()
    {
        var edit = new OmsiObjectTransformEdit(
            0, "Sceneryobjects\\Pack\\tree.sco", 1,
            double.NaN, 0, 0, 0, 0, 0);

        Assert.False(NativeTransformEditValidator.IsFinite(edit));
    }

    [Fact]
    public void SplineEdit_RejectsNonFiniteValues()
    {
        var edit = new OmsiSplineTransformEdit(
            0, "Splines\\Road.sli", 1, -1, -1, false,
            0, 0, 0, 0, 10, 4, double.PositiveInfinity, 0);

        Assert.False(NativeTransformEditValidator.IsFinite(edit));
    }

    [Fact]
    public void SelectionInfo_RejectsNonFiniteValuesBeforeMutation()
    {
        var values = new NativeSelectionInfo(
            PickingKind.Object, 1, 0, 0, "tree.sco",
            0, double.NegativeInfinity, 0, 0,
            null, null, null, null, null, null);

        Assert.False(NativeTransformEditValidator.IsFinite(values));
    }

    [Fact]
    public void PendingBatch_RejectsMissingOrNonFiniteEditPayloads()
    {
        var tile = new OmsiTileReference(0, 0, "tile_0_0.map");
        var valid = new NativePendingTransformEdit(
            tile,
            new OmsiObjectTransformEdit(
                0, "Sceneryobjects\\Pack\\tree.sco", 1,
                1, 2, 3, 0, 0, 0),
            null);
        var invalid = new NativePendingTransformEdit(
            tile,
            new OmsiObjectTransformEdit(
                0, "Sceneryobjects\\Pack\\tree.sco", 1,
                double.NaN, 2, 3, 0, 0, 0),
            null);
        var empty = new NativePendingTransformEdit(tile, null, null);
        var missingTilePath = new NativePendingTransformEdit(
            tile with { RelativeMapPath = " " },
            new OmsiObjectTransformEdit(
                0, "Sceneryobjects\\Pack\\tree.sco", 1,
                1, 2, 3, 0, 0, 0),
            null);

        Assert.True(NativeTransformEditValidator.AreFinite([valid]));
        Assert.False(NativeTransformEditValidator.AreFinite([invalid]));
        Assert.False(NativeTransformEditValidator.AreFinite([empty]));
        Assert.False(NativeTransformEditValidator.AreFinite([missingTilePath]));
    }

    [Theory]
    [InlineData(-1, 1, "Sceneryobjects\\\\Pack\\\\tree.sco")]
    [InlineData(0, -1, "Sceneryobjects\\\\Pack\\\\tree.sco")]
    [InlineData(0, 1, " ")]
    public void ObjectEdit_RejectsInvalidIdentity(int sectionOrdinal, int objectId, string path)
    {
        var edit = new OmsiObjectTransformEdit(
            sectionOrdinal, path, objectId,
            1, 2, 3, 0, 0, 0);

        Assert.False(NativeTransformEditValidator.IsFinite(edit));
    }

    [Theory]
    [InlineData(-1, 1, "Splines\\\\Road.sli")]
    [InlineData(0, -1, "Splines\\\\Road.sli")]
    [InlineData(0, 1, " ")]
    public void SplineEdit_RejectsInvalidIdentity(int sectionOrdinal, int splineId, string path)
    {
        var edit = new OmsiSplineTransformEdit(
            sectionOrdinal, path, splineId, -1, -1, false,
            0, 0, 0, 0, 10, 4, 0, 0);

        Assert.False(NativeTransformEditValidator.IsFinite(edit));
    }

    [Fact]
    public void PendingBatch_RejectsEditWithBothPayloads()
    {
        var tile = new OmsiTileReference(0, 0, "tile_0_0.map");
        var objectEdit = new OmsiObjectTransformEdit(
            0, "Sceneryobjects\\\\Pack\\\\tree.sco", 1,
            1, 2, 3, 0, 0, 0);
        var splineEdit = new OmsiSplineTransformEdit(
            0, "Splines\\\\Road.sli", 1, -1, -1, false,
            0, 0, 0, 0, 10, 4, 0, 0);
        var mixed = new NativePendingTransformEdit(tile, objectEdit, splineEdit);

        Assert.False(NativeTransformEditValidator.AreFinite([mixed]));
    }


    [Fact]
    public void SelectionInfo_RejectsUnknownPickingKind()
    {
        var values = new NativeSelectionInfo(
            (PickingKind)int.MaxValue, 1, 0, 0, "tree.sco",
            0, 0, 0, 0,
            null, null, null, null, null, null);

        Assert.False(NativeTransformEditValidator.IsFinite(values));
    }

}
