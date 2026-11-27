using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Terrain;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Graphics;

namespace MapStudio.Native.Services;

internal static class NativeTerrariumElevation
{
    private const int TileSize = MapStudioTerrariumElevationClient.TileSize;

    internal static MapStudioTerrariumElevationClient Client { get; } = new(
        new HttpClient { Timeout = TimeSpan.FromSeconds(30) },
        bytes => NativePngDecoder.TryDecode(bytes, (TileSize, TileSize))?.Rgba);

    internal static async Task<MapStudioTerrainElevationBatchResult> ApplyToTerrainAsync(
        string omsiRoot, OmsiMapDescriptor map, MapStudioGeographicAnchor anchor,
        IProgress<MapStudioElevationDownloadProgress> progress, CancellationToken cancellationToken)
    {
        var bounds = MapStudioElevationCoverageResolver.Resolve(map.Tiles, anchor);
        var surface = await Client.DownloadForCoverageAsync(
            bounds, progress: progress, cancellationToken: cancellationToken).ConfigureAwait(false);

        return await new MapStudioTerrainElevationBatchApplier().ApplyAsync(
            omsiRoot, map.DirectoryPath, anchor, surface, cancellationToken).ConfigureAwait(false);
    }
}
