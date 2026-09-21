using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
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
    public async Task EnsureSeedsOwnStarterAssetsWithoutOmsiInstallation()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Workspace-Starter-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var bootstrapper =
                new MapStudioWorkspaceBootstrapper();

            var first =
                await bootstrapper
                    .EnsureAsync(
                        root,
                        seedStarterAssets:
                            true);

            Assert.True(
                first.StarterAssetsCreated);

            Assert.True(
                Directory.Exists(
                    first.TexturePath));

            Assert.NotEmpty(
                Directory.EnumerateFiles(
                    first.SplinesPath,
                    "*.sli",
                    SearchOption
                        .AllDirectories));

            Assert.NotEmpty(
                Directory.EnumerateFiles(
                    first.SceneryObjectsPath,
                    "*.sco",
                    SearchOption
                        .AllDirectories));

            var vegetationRoot =
                Path.Combine(
                    first.SceneryObjectsPath,
                    MapStudioStarterVegetationGenerator
                        .RootFolderName);

            var starterTree =
                Path.Combine(
                    vegetationRoot,
                    "Starter_Tree",
                    "starter_tree.sco");

            var starterShrub =
                Path.Combine(
                    vegetationRoot,
                    "Starter_Shrub",
                    "starter_shrub.sco");

            Assert.True(
                File.Exists(
                    starterTree));

            Assert.True(
                File.Exists(
                    starterShrub));

            var treeMetadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        starterTree);

            var shrubMetadata =
                await new OmsiSceneryObjectReader()
                    .ReadMetadataAsync(
                        starterShrub);

            Assert.NotNull(
                treeMetadata.Tree);

            Assert.NotNull(
                shrubMetadata.Tree);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        vegetationRoot,
                        "Starter_Tree",
                        treeMetadata.Tree!
                            .TextureName)));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        vegetationRoot,
                        "Starter_Shrub",
                        shrubMetadata.Tree!
                            .TextureName)));

            var manifestPath =
                Path.Combine(
                    first.RootPath,
                    ".mapstudio",
                    "workspace.json");

            Assert.True(
                File.Exists(
                    manifestPath));

            var manifest =
                await File.ReadAllTextAsync(
                    manifestPath);

            Assert.Contains(
                "standalone-workspace",
                manifest,
                StringComparison.Ordinal);

            Assert.Contains(
                "omsi-compatible-content-root",
                manifest,
                StringComparison.Ordinal);

            var second =
                await bootstrapper
                    .EnsureAsync(
                        root,
                        seedStarterAssets:
                            true);

            Assert.False(
                second.StarterAssetsCreated);
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
    public async Task ImportMapFolderCopiesAndValidatesExternalMap()
    {
        var targetRoot =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Workspace-Target-" +
                Guid.NewGuid()
                    .ToString("N"));

        var sourceRoot =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Workspace-Source-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var bootstrapper =
                new MapStudioWorkspaceBootstrapper();

            var externalMap =
                await bootstrapper
                    .CreateBlankMapAsync(
                        sourceRoot,
                        "External_City",
                        "External City");

            var imported =
                await bootstrapper
                    .ImportMapFolderAsync(
                        targetRoot,
                        externalMap);

            Assert.NotEqual(
                Path.GetFullPath(
                    externalMap),
                Path.GetFullPath(
                    imported.MapDirectory));

            Assert.True(
                imported.CopiedFiles >
                    0);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        imported.MapDirectory,
                        "global.cfg")));

            var map =
                await OmsiMapCatalog
                    .OpenMapAsync(
                        imported.MapDirectory);

            Assert.Equal(
                "External City",
                map.DisplayName);

            Assert.Single(
                map.Tiles);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        imported.MapDirectory,
                        ".mapstudio",
                        "import.json")));
        }
        finally
        {
            if (Directory.Exists(
                    targetRoot))
            {
                Directory.Delete(
                    targetRoot,
                    recursive:
                        true);
            }

            if (Directory.Exists(
                    sourceRoot))
            {
                Directory.Delete(
                    sourceRoot,
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
