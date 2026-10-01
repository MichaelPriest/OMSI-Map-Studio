namespace MapStudio.Core.Omsi.Indexing;

public enum MapStudioOriginalOmsiAssetRole
{
    None,
    RoadSpline,
    InvisibleRoadSpline,
    SidewalkSpline,
    RailSpline,
    JunctionObject,
    CrosswalkObject,
    TrafficSignalObject,
    TrafficSignObject,
    BusStopObject,
    BusShelterObject,
    StreetLightObject,
    BenchObject,
    WasteBasketObject,
    BollardObject,
    UtilityPoleObject,
    UtilityObject,
    StreetFurnitureObject,
    BuildingObject,
    VegetationObject
}

/// <summary>
/// Curated family rules for assets shipped with a clean OMSI 2 installation.
/// The rules are intentionally family based instead of copying an external
/// filename list. Runtime use still requires the asset to exist locally.
/// </summary>
public static class MapStudioOriginalOmsiAssetCatalog
{
    public static MapStudioOriginalOmsiAssetRole Classify(
        OmsiAssetIndexEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return Classify(
            entry.RelativePath,
            entry.Kind);
    }

    public static MapStudioOriginalOmsiAssetRole Classify(
        string relativePath,
        OmsiAssetKind kind)
    {
        if (string.IsNullOrWhiteSpace(
                relativePath))
        {
            return MapStudioOriginalOmsiAssetRole.None;
        }

        var path =
            NormalizePath(
                relativePath);

        var fileName =
            Path.GetFileName(
                    path)
                .ToLowerInvariant();

        if (kind == OmsiAssetKind.Spline)
        {
            if (
                path is
                    @"splines\invis_street.sli" or
                    @"splines\marcel\invis_street.sli")
            {
                return MapStudioOriginalOmsiAssetRole
                    .InvisibleRoadSpline;
            }

            if (
                path is
                    @"splines\invis_sidewalk.sli" or
                    @"splines\marcel\invis_sidewalk.sli")
            {
                return MapStudioOriginalOmsiAssetRole
                    .SidewalkSpline;
            }

            var originalSplineFamily =
                path.StartsWith(
                    @"splines\marcel\",
                    StringComparison.Ordinal) ||
                path.StartsWith(
                    @"splines\marcel ch\",
                    StringComparison.Ordinal) ||
                path.StartsWith(
                    @"splines\ruede\",
                    StringComparison.Ordinal);

            if (!originalSplineFamily)
            {
                return MapStudioOriginalOmsiAssetRole.None;
            }

            if (
                fileName.StartsWith(
                    "rail_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .RailSpline;
            }

            if (
                fileName.StartsWith(
                    "sdwk_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .SidewalkSpline;
            }

            if (
                fileName.StartsWith(
                    "str_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "hstr_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "demo_lht_str_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "str_ch_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .RoadSpline;
            }

            return MapStudioOriginalOmsiAssetRole.None;
        }

        if (kind != OmsiAssetKind.SceneryObject)
        {
            return MapStudioOriginalOmsiAssetRole.None;
        }

        if (
            path.StartsWith(
                @"sceneryobjects\kreuz_mc\",
                StringComparison.Ordinal) ||
            path.StartsWith(
                @"sceneryobjects\kreuz_rue\",
                StringComparison.Ordinal))
        {
            if (
                fileName.Contains(
                    "crosswalk",
                    StringComparison.Ordinal) ||
                fileName.Contains(
                    "zebra",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .CrosswalkObject;
            }

            return MapStudioOriginalOmsiAssetRole
                .JunctionObject;
        }

        if (
            path.StartsWith(
                @"sceneryobjects\verkehrszeichen_mc\",
                StringComparison.Ordinal))
        {
            if (
                fileName.StartsWith(
                    "ampel_",
                    StringComparison.Ordinal) ||
                fileName.Contains(
                    "mast_ampel",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .TrafficSignalObject;
            }

            return MapStudioOriginalOmsiAssetRole
                .TrafficSignObject;
        }

        if (
            path.StartsWith(
                @"sceneryobjects\verkehrszeichen_rue\",
                StringComparison.Ordinal))
        {
            return MapStudioOriginalOmsiAssetRole
                .TrafficSignObject;
        }

        if (
            path ==
                @"sceneryobjects\generic\bus_stop.sco")
        {
            return MapStudioOriginalOmsiAssetRole
                .BusStopObject;
        }

        if (
            path.StartsWith(
                @"sceneryobjects\streetobjects_mc\",
                StringComparison.Ordinal))
        {
            if (
                fileName.StartsWith(
                    "busstop",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .BusStopObject;
            }

            if (
                fileName.StartsWith(
                    "strlt_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .StreetLightObject;
            }

            if (
                fileName.StartsWith(
                    "parkbench_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .BenchObject;
            }

            if (
                fileName.StartsWith(
                    "muelleinmer_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .WasteBasketObject;
            }

            if (
                fileName.StartsWith(
                    "bollard_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .BollardObject;
            }

            return MapStudioOriginalOmsiAssetRole
                .StreetFurnitureObject;
        }

        if (
            path.StartsWith(
                @"sceneryobjects\streetobjects_rue\",
                StringComparison.Ordinal))
        {
            if (
                fileName.StartsWith(
                    "busstop",
                    StringComparison.Ordinal) ||
                fileName.Contains(
                    "busstop",
                    StringComparison.Ordinal) ||
                fileName.Contains(
                    "busbahnsteig",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "timetable",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .BusStopObject;
            }

            if (
                fileName.Contains(
                    "wartehaus",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .BusShelterObject;
            }

            if (
                fileName.StartsWith(
                    "gaslight_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "neonlight_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "sodiumlight_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "lbl-",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .StreetLightObject;
            }

            if (
                fileName.Contains(
                    "_pole_",
                    StringComparison.Ordinal) &&
                fileName.Contains(
                    "cable",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .UtilityPoleObject;
            }

            return MapStudioOriginalOmsiAssetRole
                .StreetFurnitureObject;
        }

        if (
            path.StartsWith(
                @"sceneryobjects\ruede\",
                StringComparison.Ordinal))
        {
            if (
                fileName.StartsWith(
                    "hst_",
                    StringComparison.Ordinal))
            {
                return fileName.Contains(
                        "wartehaus",
                        StringComparison.Ordinal)
                    ? MapStudioOriginalOmsiAssetRole
                        .BusShelterObject
                    : MapStudioOriginalOmsiAssetRole
                        .BusStopObject;
            }

            if (
                fileName.StartsWith(
                    "peitschenleuchte",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .StreetLightObject;
            }

            if (
                fileName.StartsWith(
                    "trafohaus",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "wasserpumpe",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .UtilityObject;
            }

            if (
                fileName.StartsWith(
                    "tree_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "trees_",
                    StringComparison.Ordinal) ||
                fileName.StartsWith(
                    "forest_",
                    StringComparison.Ordinal))
            {
                return MapStudioOriginalOmsiAssetRole
                    .VegetationObject;
            }
        }

        if (
            path.StartsWith(
                @"sceneryobjects\trees_mc\",
                StringComparison.Ordinal))
        {
            return MapStudioOriginalOmsiAssetRole
                .VegetationObject;
        }

        if (
            path.StartsWith(
                @"sceneryobjects\buildings_mc\",
                StringComparison.Ordinal) ||
            path.StartsWith(
                @"sceneryobjects\buildings_rue\",
                StringComparison.Ordinal) ||
            path.StartsWith(
                @"sceneryobjects\buildings_ddr_rue\",
                StringComparison.Ordinal) ||
            path.StartsWith(
                @"sceneryobjects\buildings_jhy\",
                StringComparison.Ordinal))
        {
            return MapStudioOriginalOmsiAssetRole
                .BuildingObject;
        }

        return MapStudioOriginalOmsiAssetRole.None;
    }

    public static bool IsKnownOriginalAsset(
        OmsiAssetIndexEntry entry) =>
        Classify(entry) !=
        MapStudioOriginalOmsiAssetRole.None;

    public static bool IsOriginalRoadSpline(
        OmsiAssetIndexEntry entry) =>
        Classify(entry) ==
        MapStudioOriginalOmsiAssetRole.RoadSpline;

    private static string NormalizePath(
        string path) =>
        path
            .Trim()
            .Replace(
                '/',
                '\\')
            .TrimStart(
                '\\')
            .ToLowerInvariant();
}
