using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Workspace;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioWorkspaceOmsiPackageExporterTests
{
    [Fact]
    public async Task ExportBuildsSafeOmsiFolderTreeWithoutSkyOverrides()
    {
        var workspace =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Export-Workspace-" +
                Guid.NewGuid()
                    .ToString("N"));

        var destination =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Export-Output-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var bootstrapper =
                new MapStudioWorkspaceBootstrapper();

            await bootstrapper
                .EnsureAsync(
                    workspace);

            var mapPath =
                await bootstrapper
                    .CreateBlankMapAsync(
                        workspace,
                        "Test_Map",
                        "Test Map");

            var globalPath =
                Path.Combine(
                    mapPath,
                    "global.cfg");

            var descriptor =
                new OmsiMapDescriptor(
                    "Test_Map",
                    "Test Map",
                    mapPath,
                    globalPath,
                    false,
                    [],
                    []);

            var result =
                await new MapStudioWorkspaceOmsiPackageExporter()
                    .ExportAsync(
                        workspace,
                        descriptor,
                        destination);

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        "maps",
                        "Test_Map",
                        "global.cfg")));

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        "Sceneryobjects")));

            Assert.True(
                Directory.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        "Splines")));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        "Texture",
                        "mapstudio_grass.bmp")));

            Assert.False(
                File.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        "Texture",
                        "himmel01.bmp")));

            Assert.False(
                Directory.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        "Texture",
                        "skybox")));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        ".mapstudio-export",
                        "package.json")));

            Assert.True(
                File.Exists(
                    Path.Combine(
                        result.PackageRoot,
                        "README-MAP-STUDIO.txt")));

            Assert.True(
                result.CopiedFiles >
                0);

            var second =
                await new MapStudioWorkspaceOmsiPackageExporter()
                    .ExportAsync(
                        workspace,
                        descriptor,
                        destination);

            Assert.NotEqual(
                result.PackageRoot,
                second.PackageRoot);
        }
        finally
        {
            if (Directory.Exists(
                    workspace))
            {
                Directory.Delete(
                    workspace,
                    recursive:
                        true);
            }

            if (Directory.Exists(
                    destination))
            {
                Directory.Delete(
                    destination,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task ExportRejectsDestinationInsideWorkspace()
    {
        var workspace =
            Path.Combine(
                Path.GetTempPath(),
                "MapStudio-Export-Reject-" +
                Guid.NewGuid()
                    .ToString("N"));

        try
        {
            var bootstrapper =
                new MapStudioWorkspaceBootstrapper();

            await bootstrapper
                .EnsureAsync(
                    workspace);

            var mapPath =
                await bootstrapper
                    .CreateBlankMapAsync(
                        workspace,
                        "Map",
                        "Map");

            var descriptor =
                new OmsiMapDescriptor(
                    "Map",
                    "Map",
                    mapPath,
                    Path.Combine(
                        mapPath,
                        "global.cfg"),
                    false,
                    [],
                    []);

            await Assert.ThrowsAsync<
                InvalidDataException>(
                    () =>
                        new MapStudioWorkspaceOmsiPackageExporter()
                            .ExportAsync(
                                workspace,
                                descriptor,
                                Path.Combine(
                                    workspace,
                                    "exports")));
        }
        finally
        {
            if (Directory.Exists(
                    workspace))
            {
                Directory.Delete(
                    workspace,
                    recursive:
                        true);
            }
        }
    }
}
