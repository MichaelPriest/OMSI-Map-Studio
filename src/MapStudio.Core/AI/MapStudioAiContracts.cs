namespace MapStudio.Core.AI;

[Flags]
public enum MapStudioAiCapability
{
    None = 0,
    ImageUnderstanding = 1 << 0,
    BuildingReferenceAnalysis = 1 << 1,
    RoadReferenceAnalysis = 1 << 2,
    SceneReferenceAnalysis = 1 << 3,
    StructuredOutput = 1 << 4
}

public sealed record MapStudioAiProviderDescriptor(
    string Id,
    string DisplayName,
    MapStudioAiCapability Capabilities,
    bool IsLocal = false);

public sealed record MapStudioAiImageReference(
    ReadOnlyMemory<byte> Data,
    string MimeType,
    string? FileName = null)
{
    public bool IsUsable =>
        Data.Length > 0 &&
        !string.IsNullOrWhiteSpace(
            MimeType);
}

public enum MapStudioBuildingRoofType
{
    Unknown,
    Flat,
    Gable,
    Hip,
    Shed,
    Mansard,
    Dome,
    Custom
}

public sealed record MapStudioBuildingReferenceRequest(
    IReadOnlyList<MapStudioAiImageReference> Images,
    string? UserNotes = null,
    double? KnownWidthMeters = null,
    double? KnownHeightMeters = null,
    double? KnownDepthMeters = null,
    int? KnownFloorCount = null);

public sealed record MapStudioBuildingOpeningEstimate(
    int WindowsPerFloor,
    int DoorCount,
    double? TypicalWindowWidthMeters = null,
    double? TypicalWindowHeightMeters = null);

public sealed record MapStudioBuildingReferenceAnalysis(
    double? WidthMeters,
    double? HeightMeters,
    double? DepthMeters,
    int? FloorCount,
    MapStudioBuildingRoofType RoofType,
    double? RoofHeightMeters,
    MapStudioBuildingOpeningEstimate? Openings,
    string? FacadeMaterial,
    string? RoofMaterial,
    string? ArchitecturalStyle,
    string? Notes,
    double Confidence)
{
    public MapStudioBuildingReferenceAnalysis Normalize()
    {
        static double? Positive(
            double? value) =>
            value is > 0 &&
            double.IsFinite(
                value.Value)
                ? value
                : null;

        return this with
        {
            WidthMeters =
                Positive(
                    WidthMeters),
            HeightMeters =
                Positive(
                    HeightMeters),
            DepthMeters =
                Positive(
                    DepthMeters),
            FloorCount =
                FloorCount is > 0 and <= 300
                    ? FloorCount
                    : null,
            RoofHeightMeters =
                Positive(
                    RoofHeightMeters),
            Confidence =
                Math.Clamp(
                    double.IsFinite(
                        Confidence)
                        ? Confidence
                        : 0,
                    0,
                    1)
        };
    }
}

public enum MapStudioRoadReferenceCoordinateSpace
{
    NormalizedImage = 0,
    ImagePixels = 1,
    WorldMeters = 2
}

public sealed record MapStudioRoadPolylinePoint(
    double X,
    double Y);

public sealed record MapStudioRoadReferencePolyline(
    string Kind,
    IReadOnlyList<MapStudioRoadPolylinePoint> Points,
    int? LaneCount = null,
    bool? OneWay = null,
    double? WidthMeters = null);

public sealed record MapStudioRoadReferenceRequest(
    IReadOnlyList<MapStudioAiImageReference> Images,
    string? UserNotes = null);

public sealed record MapStudioRoadReferenceAnalysis(
    IReadOnlyList<MapStudioRoadReferencePolyline> Roads,
    string? Notes,
    double Confidence,
    MapStudioRoadReferenceCoordinateSpace CoordinateSpace =
        MapStudioRoadReferenceCoordinateSpace.NormalizedImage,
    int? ImageWidth = null,
    int? ImageHeight = null);

public interface IMapStudioAiProvider
{
    MapStudioAiProviderDescriptor Descriptor
    {
        get;
    }

    Task<MapStudioBuildingReferenceAnalysis>
        AnalyzeBuildingReferenceAsync(
            MapStudioBuildingReferenceRequest request,
            CancellationToken cancellationToken =
                default);

    Task<MapStudioRoadReferenceAnalysis>
        AnalyzeRoadReferenceAsync(
            MapStudioRoadReferenceRequest request,
            CancellationToken cancellationToken =
                default);
}
