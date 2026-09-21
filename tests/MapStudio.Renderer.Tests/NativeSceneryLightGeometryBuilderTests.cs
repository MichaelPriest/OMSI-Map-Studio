using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Renderer.Picking;
using MapStudio.Renderer.Scene;
using Xunit;

namespace MapStudio.Renderer.Tests;

public sealed class NativeSceneryLightGeometryBuilderTests
{
    [Fact]
    public void BuildCreatesNightLightMarkerAtPlacedObject()
    {
        var tile =
            new OmsiTileReference(
                0,
                0,
                "tile_0_0.map");

        var placed =
            new OmsiPlacedObject(
                "0",
                @"Sceneryobjects\Lights\lamp.sco",
                7,
                10,
                20,
                0,
                0,
                0,
                0,
                []);

        var scene =
            new NativeSceneBuilder()
                .Build(
                    [
                        new NativeSceneTile(
                            tile,
                            new OmsiTileContent(
                                new OmsiTileSummary(
                                    true,
                                    1,
                                    0,
                                    0),
                                [placed],
                                []))
                    ],
                    new PickingRegistry<object>());

        var asset =
            new NativeSceneryAsset(
                placed.SceneryObjectPath,
                @"C:\OMSI\Sceneryobjects\Lights\lamp.sco",
                [],
                null,
                false,
                null)
            {
                LightPoints =
                    [
                        new OmsiSceneryLightPoint(
                            "light_enh_2",
                            0,
                            0,
                            5,
                            0,
                            1,
                            0,
                            255,
                            200,
                            100,
                            0.4,
                            20,
                            50,
                            "NightLightA",
                            "1",
                            "1",
                            null,
                            [])
                    ]
            };

        var result =
            new NativeSceneryLightGeometryBuilder()
                .Build(
                    scene,
                    new Dictionary<
                        string,
                        NativeSceneryAsset>(
                            StringComparer.OrdinalIgnoreCase)
                    {
                        [placed.SceneryObjectPath] =
                            asset
                    });

        Assert.Equal(
            1,
            result.LightPointCount);

        Assert.Equal(
            36,
            result.Vertices.Length);

        Assert.Contains(
            result.Vertices,
            vertex =>
                vertex.Position.Y >
                    4.7f &&
                vertex.Position.Y <
                    5.3f);
    }
}
