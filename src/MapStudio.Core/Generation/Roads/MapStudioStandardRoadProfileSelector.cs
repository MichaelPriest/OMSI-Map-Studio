using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.Generation.Roads;

public static class MapStudioStandardRoadProfileSelector
{
    public static MapStudioStandardRoadProfile Select(
        string? kind,
        int? laneCount,
        bool? oneWay,
        double? widthMeters)
    {
        var normalized =
            kind?
                .Trim()
                .ToLowerInvariant();

        var lanes =
            laneCount is > 0
                ? laneCount
                : null;

        var width =
            widthMeters is > 0 &&
            double.IsFinite(
                widthMeters.Value)
                ? widthMeters
                : null;

        if (
            normalized is
                "footway" or
                "pedestrian" or
                "path" or
                "steps" or
                "cycleway")
        {
            return MapStudioStandardRoadCatalog
                .Pedestrian;
        }

        if (oneWay == true)
        {
            if (
                lanes is >= 3 ||
                width is >= 8.75)
            {
                return MapStudioStandardRoadCatalog
                    .OneWayThreeLane;
            }

            if (
                lanes is >= 2 ||
                width is >= 5.25)
            {
                return MapStudioStandardRoadCatalog
                    .OneWayTwoLane;
            }

            return MapStudioStandardRoadCatalog
                .OneWaySingle;
        }

        if (
            normalized is
                "motorway" or
                "trunk" or
                "motorway_link" or
                "trunk_link")
        {
            return MapStudioStandardRoadCatalog
                .DividedAvenueFourLane;
        }

        if (
            normalized is
                "primary" or
                "primary_link")
        {
            if (
                lanes is >= 4 ||
                width is >= 11.5)
            {
                return MapStudioStandardRoadCatalog
                    .AvenueFourLane;
            }

            return MapStudioStandardRoadCatalog
                .RoadTwoLaneWithSidewalk;
        }

        if (
            normalized is
                "secondary" or
                "secondary_link")
        {
            if (
                lanes is >= 4 ||
                width is >= 11.5)
            {
                return MapStudioStandardRoadCatalog
                    .AvenueFourLane;
            }

            return MapStudioStandardRoadCatalog
                .RoadTwoLaneWithSidewalk;
        }

        if (
            normalized is
                "tertiary" or
                "tertiary_link")
        {
            if (
                lanes is >= 4 ||
                width is >= 11.5)
            {
                return MapStudioStandardRoadCatalog
                    .AvenueFourLane;
            }

            if (width is <= 6.2)
            {
                return MapStudioStandardRoadCatalog
                    .LocalWithSidewalk;
            }

            return MapStudioStandardRoadCatalog
                .RoadTwoLaneWithSidewalk;
        }

        if (
            normalized is
                "service" or
                "track")
        {
            if (
                lanes is >= 4 ||
                width is >= 11.5)
            {
                return MapStudioStandardRoadCatalog
                    .AvenueFourLane;
            }

            if (width is >= 6.3)
            {
                return MapStudioStandardRoadCatalog
                    .RoadTwoLane;
            }

            return MapStudioStandardRoadCatalog
                .LocalNarrow;
        }

        if (
            normalized is
                "residential" or
                "living_street" or
                "unclassified" or
                "road")
        {
            if (
                lanes is >= 4 ||
                width is >= 11.5)
            {
                return MapStudioStandardRoadCatalog
                    .AvenueFourLane;
            }

            if (width is >= 6.3)
            {
                return MapStudioStandardRoadCatalog
                    .RoadTwoLaneWithSidewalk;
            }

            return MapStudioStandardRoadCatalog
                .LocalWithSidewalk;
        }

        if (
            lanes is >= 4 ||
            width is >= 11.5)
        {
            return MapStudioStandardRoadCatalog
                .AvenueFourLane;
        }

        if (width is <= 6.2)
        {
            return MapStudioStandardRoadCatalog
                .LocalWithSidewalk;
        }

        return MapStudioStandardRoadCatalog
            .RoadTwoLaneWithSidewalk;
    }
}
