namespace MapStudio.Core.Omsi.Splines;

public sealed record MapStudioStandardRoadProfile(
    string Key,
    string Label,
    string FileName,
    int LaneCount,
    double LaneWidthMeters,
    double SidewalkWidthMeters,
    bool OneWay,
    bool IsDivided = false,
    double MedianWidthMeters = 0.0,
    bool IsPedestrian = false)
{
    public string RelativePath =>
        @"Splines\" +
        MapStudioRoadKitGenerator
            .PackFolderName +
        @"\" +
        FileName;

    public double CarriagewayWidthMeters =>
        IsPedestrian
            ? LaneWidthMeters
            : LaneCount *
              LaneWidthMeters;

    public double TotalWidthMeters =>
        CarriagewayWidthMeters +
        (
            IsDivided
                ? MedianWidthMeters
                : 0.0
        ) +
        SidewalkWidthMeters *
            2.0;

    public int LanesPerDirection =>
        IsDivided
            ? Math.Max(
                1,
                LaneCount /
                2)
            : LaneCount;
}

public static class MapStudioStandardRoadCatalog
{
    public static readonly MapStudioStandardRoadProfile
        OneWaySingle =
            new(
                "oneway-1",
                "Mão única 1 faixa · 3,5 m",
                "ms_road_oneway_3_5m.sli",
                1,
                3.5,
                0,
                true);

    public static readonly MapStudioStandardRoadProfile
        OneWayTwoLane =
            new(
                "oneway-2",
                "Mão única 2 faixas · 7 m",
                "ms_road_oneway_2lane_7m.sli",
                2,
                3.5,
                0,
                true);

    public static readonly MapStudioStandardRoadProfile
        OneWayThreeLane =
            new(
                "oneway-3",
                "Mão única 3 faixas · 10,5 m",
                "ms_road_oneway_3lane_10_5m.sli",
                3,
                3.5,
                0,
                true);

    public static readonly MapStudioStandardRoadProfile
        LocalNarrow =
            new(
                "local-5.5",
                "Rua local compacta · 5,5 m",
                "ms_road_local_5_5m.sli",
                2,
                2.75,
                0,
                false);

    public static readonly MapStudioStandardRoadProfile
        LocalWithSidewalk =
            new(
                "local-5.5-sidewalk",
                "Rua local · 5,5 m + calçadas",
                "ms_road_local_5_5m_sidewalk.sli",
                2,
                2.75,
                1.5,
                false);

    public static readonly MapStudioStandardRoadProfile
        RoadTwoLane =
            new(
                "road-7",
                "Rua 2 faixas · 7 m",
                "ms_road_2lane_7m.sli",
                2,
                3.5,
                0,
                false);

    public static readonly MapStudioStandardRoadProfile
        RoadTwoLaneWithSidewalk =
            new(
                "road-7-sidewalk",
                "Rua 2 faixas · 7 m + calçadas",
                "ms_road_2lane_7m_sidewalk.sli",
                2,
                3.5,
                2.0,
                false);

    public static readonly MapStudioStandardRoadProfile
        AvenueFourLane =
            new(
                "avenue-4",
                "Avenida 4 faixas · 14 m + calçadas",
                "ms_avenue_4lane_14m_sidewalk.sli",
                4,
                3.5,
                2.0,
                false);

    public static readonly MapStudioStandardRoadProfile
        DividedAvenueFourLane =
            new(
                "avenue-divided-4",
                "Avenida dividida 4 faixas",
                "ms_avenue_divided_4lane.sli",
                4,
                3.5,
                2.0,
                false,
                IsDivided:
                    true,
                MedianWidthMeters:
                    2.0);

    public static readonly MapStudioStandardRoadProfile
        Pedestrian =
            new(
                "pedestrian-3",
                "Via de pedestres · 3 m",
                "ms_pedestrian_3m.sli",
                0,
                3.0,
                0,
                false,
                IsPedestrian:
                    true);

    public static IReadOnlyList<
        MapStudioStandardRoadProfile>
        Profiles { get; } =
        [
            OneWaySingle,
            OneWayTwoLane,
            OneWayThreeLane,
            LocalNarrow,
            LocalWithSidewalk,
            RoadTwoLane,
            RoadTwoLaneWithSidewalk,
            AvenueFourLane,
            DividedAvenueFourLane,
            Pedestrian
        ];

    public static string GetBridgeFileName(
        MapStudioStandardRoadProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile);

        return AddVariantSuffix(
            profile.FileName,
            "bridge");
    }

    public static string GetTunnelFileName(
        MapStudioStandardRoadProfile profile)
    {
        ArgumentNullException.ThrowIfNull(
            profile);

        return AddVariantSuffix(
            profile.FileName,
            "tunnel");
    }

    public static string ResolvePlacementRelativePath(
        string relativePath,
        bool bridge,
        bool tunnel)
    {
        ArgumentException
            .ThrowIfNullOrWhiteSpace(
                relativePath);

        if (
            bridge ==
            tunnel)
        {
            return relativePath;
        }

        var profile =
            Profiles
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.RelativePath,
                            relativePath,
                            StringComparison
                                .OrdinalIgnoreCase));

        if (
            profile is null ||
            profile.IsPedestrian)
        {
            return relativePath;
        }

        var fileName =
            bridge
                ? GetBridgeFileName(
                    profile)
                : GetTunnelFileName(
                    profile);

        return @"Splines\" +
            MapStudioRoadKitGenerator
                .PackFolderName +
            @"\" +
            fileName;
    }


    public static bool
        IsTerrainConformProtectedPath(
            string? assetPath)
    {
        if (string.IsNullOrWhiteSpace(
                assetPath))
        {
            return false;
        }

        var normalized =
            assetPath
                .Replace(
                    '/',
                    '\\')
                .Trim();

        var segments =
            normalized.Split(
                '\\',
                StringSplitOptions
                    .RemoveEmptyEntries |
                StringSplitOptions
                    .TrimEntries);

        if (
            segments.Any(
                segment =>
                    string.Equals(
                        segment,
                        MapStudioBridgeSplineGenerator
                            .PackFolderName,
                        StringComparison
                            .OrdinalIgnoreCase) ||
                    string.Equals(
                        segment,
                        MapStudioTunnelSplineGenerator
                            .PackFolderName,
                        StringComparison
                            .OrdinalIgnoreCase)))
        {
            return true;
        }

        var fileName =
            segments.LastOrDefault() ??
            normalized;

        return
            fileName.EndsWith(
                "_bridge.sli",
                StringComparison
                    .OrdinalIgnoreCase) ||
            fileName.EndsWith(
                "_tunnel.sli",
                StringComparison
                    .OrdinalIgnoreCase);
    }

    private static string AddVariantSuffix(
        string fileName,
        string suffix)
    {
        var extension =
            Path.GetExtension(
                fileName);

        var stem =
            fileName[
                ..^extension.Length];

        return stem +
            "_" +
            suffix +
            extension;
    }

    public static MapStudioStandardRoadProfile
        FindByRelativePath(
            string relativePath)
    {
        ArgumentException
            .ThrowIfNullOrWhiteSpace(
                relativePath);

        return Profiles
            .First(
                profile =>
                    string.Equals(
                        profile.RelativePath,
                        relativePath,
                        StringComparison
                            .OrdinalIgnoreCase));
    }
}
