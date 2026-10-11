using System.Numerics;
using MapStudio.Core.Editing;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapObjectTransformOperationTests
{
    [Fact]
    public void ExecuteUndoRedo_RestoresExactObjectTransform()
    {
        var before = new MapObjectTransform(
            new Vector3(10, 2, -5),
            new Vector3(0, 90, 0),
            Vector3.One);
        var after = new MapObjectTransform(
            new Vector3(18, 3, -7),
            new Vector3(0, 135, 0),
            new Vector3(1.2f, 1, 1.2f));
        var target = new TestTransformTarget("tree-001", before);
        var history = new EditHistory();

        history.Execute(new MapObjectTransformOperation(target, before, after));
        Assert.Equal(after, target.Transform);

        Assert.True(history.Undo());
        Assert.Equal(before, target.Transform);

        Assert.True(history.Redo());
        Assert.Equal(after, target.Transform);
    }

    [Fact]
    public void Constructor_RejectsNonFiniteTransformValues()
    {
        var target = new TestTransformTarget("sign-001", MapObjectTransform.Identity);
        var invalid = new MapObjectTransform(
            new Vector3(float.NaN, 0, 0),
            Vector3.Zero,
            Vector3.One);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MapObjectTransformOperation(target, MapObjectTransform.Identity, invalid));
    }

    [Fact]
    public void Constructor_RequiresStableObjectIdentifier()
    {
        var target = new TestTransformTarget(" ", MapObjectTransform.Identity);

        Assert.Throws<ArgumentException>(
            () => new MapObjectTransformOperation(
                target,
                MapObjectTransform.Identity,
                MapObjectTransform.Identity));
    }

    private sealed class TestTransformTarget(string objectId, MapObjectTransform transform)
        : IMapObjectTransformTarget
    {
        public string ObjectId { get; } = objectId;

        public MapObjectTransform Transform { get; set; } = transform;
    }
}
