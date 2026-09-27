namespace MapStudio.Core.Generation.Scene;

public enum MapStudioSceneEvidenceSource
{
    Osm = 0,
    AerialImagery = 1,
    StreetLevelImagery = 2,
    UserImagery = 3,
    ElevationModel = 4
}

public enum MapStudioSceneFeatureKind
{
    Building = 0,
    Wall = 1,
    Fence = 2,
    UtilityPole = 3,
    StreetLight = 4,
    Tree = 5,
    TrafficSign = 6,
    BusShelter = 7,
    GuardRail = 8,
    Sidewalk = 9,
    Driveway = 10,
    Parking = 11,
    Unknown = 99
}

public sealed record MapStudioSceneEvidence(
    MapStudioSceneEvidenceSource Source,
    double Confidence,
    string? ReferenceId = null,
    string? Notes = null)
{
    public MapStudioSceneEvidence Normalize() =>
        this with
        {
            Confidence =
                Math.Clamp(
                    double.IsFinite(
                        Confidence)
                        ? Confidence
                        : 0.0,
                    0.0,
                    1.0),
            ReferenceId =
                string.IsNullOrWhiteSpace(
                    ReferenceId)
                    ? null
                    : ReferenceId.Trim(),
            Notes =
                string.IsNullOrWhiteSpace(
                    Notes)
                    ? null
                    : Notes.Trim()
        };
}

public sealed record MapStudioSceneFeatureCandidate(
    string Id,
    MapStudioSceneFeatureKind Kind,
    IReadOnlyList<MapStudioSceneEvidence> Evidence,
    string? SourceBuildingId = null,
    double? WidthMeters = null,
    double? HeightMeters = null,
    double? DepthMeters = null);

public sealed record MapStudioSceneFeatureDecision(
    string Id,
    MapStudioSceneFeatureKind Kind,
    double Confidence,
    bool AutoGenerate,
    bool NeedsReview,
    IReadOnlyList<MapStudioSceneEvidence> Evidence,
    string? SourceBuildingId,
    double? WidthMeters,
    double? HeightMeters,
    double? DepthMeters);

public sealed record MapStudioSceneReconstructionPlan(
    IReadOnlyList<MapStudioSceneFeatureDecision> Features)
{
    public int AutoGenerateCount =>
        Features.Count(
            feature =>
                feature.AutoGenerate);

    public int ReviewCount =>
        Features.Count(
            feature =>
                feature.NeedsReview);
}

public sealed class MapStudioSceneReconstructionPlanBuilder
{
    public const double AutoGenerateThreshold =
        0.82;

    public const double ReviewThreshold =
        0.55;

    private static readonly IReadOnlyDictionary<
        MapStudioSceneEvidenceSource,
        double> SourceWeights =
        new Dictionary<
            MapStudioSceneEvidenceSource,
            double>
        {
            [
                MapStudioSceneEvidenceSource
                    .UserImagery
            ] = 1.00,
            [
                MapStudioSceneEvidenceSource
                    .StreetLevelImagery
            ] = 0.95,
            [
                MapStudioSceneEvidenceSource
                    .Osm
            ] = 0.90,
            [
                MapStudioSceneEvidenceSource
                    .ElevationModel
            ] = 0.85,
            [
                MapStudioSceneEvidenceSource
                    .AerialImagery
            ] = 0.80
        };

    public MapStudioSceneReconstructionPlan Build(
        IEnumerable<MapStudioSceneFeatureCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(
            candidates);

        var features =
            candidates
                .Where(
                    candidate =>
                        !string.IsNullOrWhiteSpace(
                            candidate.Id))
                .Select(
                    BuildDecision)
                .ToArray();

        return new MapStudioSceneReconstructionPlan(
            features);
    }

    private static MapStudioSceneFeatureDecision
        BuildDecision(
            MapStudioSceneFeatureCandidate candidate)
    {
        var evidence =
            (candidate.Evidence ??
                Array.Empty<
                    MapStudioSceneEvidence>())
            .Select(
                item =>
                    item.Normalize())
            .Where(
                item =>
                    item.Confidence >
                    0)
            .ToArray();

        var confidence =
            FuseEvidence(
                evidence);

        return new MapStudioSceneFeatureDecision(
            candidate.Id.Trim(),
            candidate.Kind,
            confidence,
            confidence >=
                AutoGenerateThreshold,
            confidence >=
                ReviewThreshold &&
            confidence <
                AutoGenerateThreshold,
            evidence,
            string.IsNullOrWhiteSpace(
                    candidate.SourceBuildingId)
                ? null
                : candidate
                    .SourceBuildingId
                    .Trim(),
            Positive(
                candidate.WidthMeters),
            Positive(
                candidate.HeightMeters),
            Positive(
                candidate.DepthMeters));
    }

    private static double FuseEvidence(
        IReadOnlyList<MapStudioSceneEvidence> evidence)
    {
        if (evidence.Count == 0)
        {
            return 0.0;
        }

        var combinedMiss =
            1.0;

        foreach (
            var item in
                evidence)
        {
            var weight =
                SourceWeights
                    .GetValueOrDefault(
                        item.Source,
                        0.75);

            var weighted =
                Math.Clamp(
                    item.Confidence *
                    weight,
                    0.0,
                    0.995);

            combinedMiss *=
                1.0 -
                weighted;
        }

        return Math.Clamp(
            1.0 -
            combinedMiss,
            0.0,
            1.0);
    }

    private static double? Positive(
        double? value) =>
        value is
            > 0 &&
        double.IsFinite(
            value.Value)
            ? value
            : null;
}
