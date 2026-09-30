using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;

namespace MapStudio.Native.Services;

public sealed class NativeCompositeBuildingVisualEvidenceProvider :
    IMapStudioBuildingVisualEvidenceProvider
{
    private readonly IReadOnlyList<
        IMapStudioBuildingVisualEvidenceProvider>
        _providers;

    public NativeCompositeBuildingVisualEvidenceProvider(
        IEnumerable<
            IMapStudioBuildingVisualEvidenceProvider>
            providers)
    {
        ArgumentNullException.ThrowIfNull(
            providers);

        _providers =
            providers
                .Where(
                    provider =>
                        provider is not null)
                .ToArray();
    }

    public async Task<IReadOnlyDictionary<
        string,
        MapStudioBuildingVisualEvidence>>
        AnalyzeAsync(
            IReadOnlyList<
                MapStudioProjectedBuildingFootprint>
                buildings,
            MapStudioGeographicAnchor anchor,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            buildings);

        var remaining =
            buildings
                .ToDictionary(
                    building =>
                        building.Id,
                    StringComparer.Ordinal);

        var results =
            new Dictionary<
                string,
                MapStudioBuildingVisualEvidence>(
                    StringComparer.Ordinal);

        foreach (var provider in _providers)
        {
            cancellationToken
                .ThrowIfCancellationRequested();

            if (remaining.Count == 0)
            {
                break;
            }

            var partial =
                await provider
                    .AnalyzeAsync(
                        remaining.Values
                            .ToArray(),
                        anchor,
                        cancellationToken)
                    .ConfigureAwait(false);

            foreach (var pair in partial)
            {
                if (
                    !remaining.ContainsKey(
                        pair.Key))
                {
                    continue;
                }

                results[
                    pair.Key] =
                    pair.Value;

                remaining.Remove(
                    pair.Key);
            }
        }

        return results;
    }
}
