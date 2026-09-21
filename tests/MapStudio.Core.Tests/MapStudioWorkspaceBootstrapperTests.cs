using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Workspace;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioWorkspaceBootstrapperTests
{
    [Fact]
    public async Task EnsureCreatesStandaloneContentRootAndOwnTemplate()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Workspace-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var info =
                await new MapStudioWorkspaceBootstrapper()
                    .EnsureAsync(
                        root,
                        seedStarterAssets:
                            false);

            Assert.True(
                Directory.Exists(
                    info.MapsPath));

            Assert.True(
                Directory.Exists(
                    info.SceneryObjectsPath));

            Assert.True(
                Directory.Exists(
                    info.SplinesPath));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        info.TemplatePath,
                        "global.cfg")));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        info.TemplatePath,
                        "tile_0_0.map.terrain")));

            var terrain =
                await new OmsiTerrainReader()
                    .ReadAsync(
                        Path.Combine(
                            info.TemplatePath,
                            "tile_0_0.map.terrain"));

            Assert.Equal(
                60,
                terrain.CellCount);

            Assert.All(
                terrain.Heights,
                value =>
                    Assert.Equal(
                        0,
                        value));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task CreateBlankMapProducesMapReadableByCore()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Workspace-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var creator =
                new MapStudioWorkspaceBootstrapper();

            var path =
                await creator
                    .CreateBlankMapAsync(
                        root,
                        "Cidade_Teste",
                        "Cidade Teste");

            var map =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        path);

            Assert.Equal(
                "Cidade Teste",
                map.DisplayName);

            Assert.Single(
                map.Tiles);

            var tile =
                map.Tiles[0];

            Assert.Equal(
                0,
                tile.X);

            Assert.Equal(
                0,
                tile.Y);

            var content =
                await new OmsiTileReader()
                    .ReadContentAsync(
                        Path.Combine(
                            path,
                            tile.RelativeMapPath));

            Assert.NotNull(
                content.Terrain);

            Assert.Equal(
                60,
                content.Terrain!
                    .CellCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task ImportItemFolderCopiesPackIntoWorkspace()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Workspace-" +
                Guid.NewGuid()
                    .ToString("N"));

        var source =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-AssetPack-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            Directory.CreateDirectory(
                source);

            await File.WriteAllTextAsync(
                Path.Combine(
                    source,
                    "test.sli"),
                "[texture]\r\ntest.bmp\r\n");

            await File.WriteAllTextAsync(
                Path.Combine(
                    source,
                    "test.bmp"),
                "placeholder");

            var result =
                await new MapStudioWorkspaceBootstrapper()
                    .ImportAssetFolderAsync(
                        root,
                        source);

            Assert.True(
                result.CopiedFiles >=
                2);

            Assert.Single(
                result.DestinationDirectories);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.DestinationDirectories[0],
                        "test.sli")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }

            if (Directory.Exists(source))
            {
                Directory.Delete(
                    source,
                    recursive:
                        true);
            }
        }
    }
}
