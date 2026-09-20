using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSceneBuilderTests
{
    [Fact]
    public void BuildAssignsStableNativePickingIdsAndWorldPositions()
    {
        var tile =
            new OmsiTileReference(
                X: 2,
                Y: -1,
                RelativeMapPath:
                    "tile_2_-1.map");

        var placedObject =
            new OmsiPlacedObject(
                HeaderValue: "object",
                SceneryObjectPath:
                    "Sceneryobjects\\Test\\house.sco",
                ObjectId: 42,
                X: 10,
                Y: 20,
                Z: 3,
                Rotation: 0,
                Pitch: 0,
                Bank: 0,
                ExtraValues:
                    Array.Empty<string>());

        var placedSpline =
            new OmsiPlacedSpline(
                HeaderValue: "spline",
                SplinePath:
                    "Splines\\Test\\road.sli",
                SplineId: 7,
                PreviousSplineId: -1,
                NextSplineId: -1,
                X: 15,
                Z: 1,
                Y: 25,
                Rotation: 0,
                Length: 30,
                Radius: 0,
                GradientStart: 0,
                GradientEnd: 0,
                IsHeightSpline: false,
                ExtraValues:
                    Array.Empty<string>());

        var content =
            new OmsiTileContent(
                new OmsiTileSummary(
                    Exists: true,
                    ObjectCount: 1,
                    SplineCount: 1,
                    SplineAttachmentCount: 0),
                [placedObject],
                [placedSpline]);

        var picking =
            new PickingRegistry<object>();

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            content)
                    ],
                    picking);

        Assert.Single(scene.Objects);
        Assert.Single(scene.Splines);
        Assert.Equal(2, scene.SelectableCount);
        Assert.Equal(2, picking.Count);

        var nativeObject =
            scene.Objects[0];

        Assert.Equal(610f, nativeObject.WorldX);
        Assert.Equal(3f, nativeObject.WorldY);
        Assert.Equal(-280f, nativeObject.WorldZ);
        Assert.Equal(
            PickingKind.Object,
            nativeObject.PickingId.Kind);

        var nativeSpline =
            scene.Splines[0];

        Assert.Equal(615f, nativeSpline.WorldX);
        Assert.Equal(1f, nativeSpline.WorldY);
        Assert.Equal(-275f, nativeSpline.WorldZ);
        Assert.Equal(
            PickingKind.Spline,
            nativeSpline.PickingId.Kind);

        Assert.True(
            picking.TryResolve(
                nativeObject.PickingId,
                out var resolvedObject));

        Assert.Same(
            placedObject,
            resolvedObject);
    }
}
