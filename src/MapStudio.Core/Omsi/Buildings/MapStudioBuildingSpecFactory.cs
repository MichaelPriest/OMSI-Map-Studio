using MapStudio.Core.AI;

namespace MapStudio.Core.Omsi.Buildings;

public static class MapStudioBuildingSpecFactory
{
    public static MapStudioBuildingSpec FromAnalysis(
        string name,
        MapStudioBuildingReferenceAnalysis analysis,
        string? facadeImagePath = null)
    {
        ArgumentNullException.ThrowIfNull(
            analysis);

        var normalized =
            analysis.Normalize();

        var floors =
            normalized.FloorCount ??
            InferFloorCount(
                normalized.HeightMeters);

        var wallHeight =
            normalized.HeightMeters ??
            Math.Max(
                3.0,
                floors *
                3.0);

        var width =
            normalized.WidthMeters ??
            10.0;

        var depth =
            normalized.DepthMeters ??
            Math.Max(
                6.0,
                width *
                0.75);

        var roofType =
            normalized.RoofType switch
            {
                MapStudioBuildingRoofType.Gable =>
                    MapStudioBuildingRoofType.Gable,
                MapStudioBuildingRoofType.Hip =>
                    MapStudioBuildingRoofType.Hip,
                MapStudioBuildingRoofType.Shed =>
                    MapStudioBuildingRoofType.Shed,
                _ =>
                    MapStudioBuildingRoofType.Flat
            };

        var roofHeight =
            roofType is
                MapStudioBuildingRoofType.Gable or
                MapStudioBuildingRoofType.Hip or
                MapStudioBuildingRoofType.Shed
                ? normalized.RoofHeightMeters ??
                  Math.Clamp(
                      width *
                      0.18,
                      1.0,
                      6.0)
                : 0.0;

        return new MapStudioBuildingSpec(
            name,
            width,
            depth,
            wallHeight,
            floors,
            roofType,
            roofHeight,
            facadeImagePath,
            normalized.Openings
                ?.WindowsPerFloor ??
            0,
            normalized.Openings
                ?.DoorCount ??
            0,
            normalized.Openings
                ?.TypicalWindowWidthMeters ??
            1.2,
            normalized.Openings
                ?.TypicalWindowHeightMeters ??
            1.2)
            .Normalize();
    }

    private static int InferFloorCount(
        double? heightMeters)
    {
        if (
            heightMeters is not
                { } height ||
            !double.IsFinite(
                height) ||
            height <=
                0)
        {
            return 1;
        }

        return Math.Clamp(
            (int)Math.Round(
                height /
                3.0,
                MidpointRounding
                    .AwayFromZero),
            1,
            300);
    }
}

public sealed class MapStudioAiBuildingSpecService
{
    public async Task<MapStudioBuildingSpec>
        AnalyzeAsync(
            IMapStudioAiProvider provider,
            MapStudioBuildingReferenceRequest request,
            string assetName,
            string? facadeImagePath = null,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentNullException.ThrowIfNull(
            provider);

        ArgumentNullException.ThrowIfNull(
            request);

        if (
            !provider
                .Descriptor
                .Capabilities
                .HasFlag(
                    MapStudioAiCapability
                        .BuildingReferenceAnalysis))
        {
            throw new InvalidOperationException(
                "aiProviderBuildingAnalysisUnsupported");
        }

        if (
            request.Images is null ||
            request.Images.Count ==
                0 ||
            !request.Images.Any(
                image =>
                    image.IsUsable))
        {
            throw new InvalidDataException(
                "aiBuildingReferenceImageRequired");
        }

        var analysis =
            await provider
                .AnalyzeBuildingReferenceAsync(
                    request,
                    cancellationToken)
                .ConfigureAwait(false);

        return MapStudioBuildingSpecFactory
            .FromAnalysis(
                assetName,
                analysis,
                facadeImagePath);
    }
}
