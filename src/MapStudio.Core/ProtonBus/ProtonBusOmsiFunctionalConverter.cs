using System.Globalization;
using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiFunctionalConversionOptions(
    double WaypointSpacing = 5.0,
    int MaxWaypointsPerPath = 999,
    bool VehiclePathsSpawn = false,
    bool PedestrianPathsSpawn = false,
    bool TrainPathsSpawn = false,
    bool AllowBicycleOnPedestrianPaths = false,
    double VehicleSpawnIntervalSeconds = 5.0,
    double PedestrianSpawnIntervalSeconds = 5.0,
    double TrainSpawnIntervalSeconds = 120.0,
    double MarkerHalfSize = 0.015,
    bool ConvertStreetLights = true);

public sealed record ProtonBusOmsiFunctionalIssue(
    string Code,
    string Source,
    string? Detail = null);

public sealed record ProtonBusOmsiFunctionalConversionResult(
    ProtonBusExportScene MarkerScene,
    IReadOnlyList<ProtonBusVehiclePathDefinition> VehiclePaths,
    IReadOnlyList<ProtonBusPedestrianPathDefinition> PedestrianPaths,
    IReadOnlyList<ProtonBusTrainPathDefinition> TrainPaths,
    IReadOnlyList<ProtonBusStreetLightDefinition> StreetLights,
    IReadOnlyList<ProtonBusOmsiFunctionalIssue> Issues)
{
    public int MarkerMeshCount =>
        MarkerScene.Meshes.Count;
}

public static class ProtonBusOmsiFunctionalConverter
{
    public static ProtonBusOmsiFunctionalConversionResult Convert(
        OmsiTileReference tile,
        OmsiTileContent content,
        IReadOnlyDictionary<string, OmsiSplineDefinition> splineDefinitions,
        IReadOnlyDictionary<string, ProtonBusResolvedSceneryAsset> sceneryAssets,
        ProtonBusOmsiFunctionalConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(splineDefinitions);
        ArgumentNullException.ThrowIfNull(sceneryAssets);

        options ??= new();

        ValidateOptions(options);

        var markers =
            new List<ProtonBusExportMesh>();

        var vehiclePaths =
            new List<ProtonBusVehiclePathDefinition>();

        var pedestrianPaths =
            new List<ProtonBusPedestrianPathDefinition>();

        var trainPaths =
            new List<ProtonBusTrainPathDefinition>();

        var streetLights =
            new List<ProtonBusStreetLightDefinition>();

        var issues =
            new List<ProtonBusOmsiFunctionalIssue>();

        foreach (var spline in content.Splines)
        {
            if (!TryGetByPath(
                    splineDefinitions,
                    spline.SplinePath,
                    out var definition) ||
                definition is null ||
                !definition.Exists)
            {
                continue;
            }

            for (var pathIndex = 0;
                 pathIndex < definition.Paths.Count;
                 pathIndex++)
            {
                var path =
                    definition.Paths[pathIndex];

                ConvertSplinePath(
                    tile,
                    spline,
                    path,
                    pathIndex,
                    options,
                    markers,
                    vehiclePaths,
                    pedestrianPaths,
                    trainPaths,
                    issues);
            }
        }

        foreach (var placedObject in content.Objects)
        {
            if (!TryGetByPath(
                    sceneryAssets,
                    placedObject.SceneryObjectPath,
                    out var asset) ||
                asset is null)
            {
                continue;
            }

            var terrainOffset =
                asset.UsesAbsoluteHeight
                    ? 0.0
                    : ProtonBusOmsiTerrainSampler
                        .GetHeightAtLocalPoint(
                            content.Terrain,
                            placedObject.X,
                            placedObject.Y);

            var objectTransform =
                CreateObjectTransform(
                    tile,
                    placedObject,
                    terrainOffset);

            for (var pathIndex = 0;
                 pathIndex < asset.ResolvedPaths.Count;
                 pathIndex++)
            {
                var path =
                    asset.ResolvedPaths[pathIndex];

                ConvertSceneryPath(
                    tile,
                    placedObject,
                    path,
                    pathIndex,
                    objectTransform,
                    options,
                    markers,
                    vehiclePaths,
                    pedestrianPaths,
                    trainPaths,
                    issues);
            }

            if (options.ConvertStreetLights)
            {
                ConvertSceneryLights(
                    tile,
                    placedObject,
                    asset,
                    objectTransform,
                    options,
                    markers,
                    streetLights,
                    issues);
            }

            if (asset.ResolvedTrafficLightControllers.Count > 0)
            {
                issues.Add(
                    new(
                        "trafficLightControllerConversionPending",
                        BuildObjectSource(
                            tile,
                            placedObject),
                        $"{asset.ResolvedTrafficLightControllers.Count} OMSI traffic-light controller(s) preserved for a later synchronized conversion pass."));
            }
        }

        return new(
            new(
                markers.ToArray()),
            vehiclePaths.ToArray(),
            pedestrianPaths.ToArray(),
            trainPaths.ToArray(),
            streetLights.ToArray(),
            issues.ToArray());
    }

    private static void ConvertSplinePath(
        OmsiTileReference tile,
        OmsiPlacedSpline spline,
        OmsiSplinePathDefinition path,
        int pathIndex,
        ProtonBusOmsiFunctionalConversionOptions options,
        ICollection<ProtonBusExportMesh> markers,
        ICollection<ProtonBusVehiclePathDefinition> vehiclePaths,
        ICollection<ProtonBusPedestrianPathDefinition> pedestrianPaths,
        ICollection<ProtonBusTrainPathDefinition> trainPaths,
        ICollection<ProtonBusOmsiFunctionalIssue> issues)
    {
        var source =
            $"tile {tile.X},{tile.Y} spline {spline.SplineId} path {pathIndex}";

        if (spline.Length <= 0.001)
        {
            issues.Add(
                new(
                    "pathLengthInvalid",
                    source,
                    "OMSI spline path has no usable length."));
            return;
        }

        foreach (var direction in GetDirections(
                     path.Direction,
                     source,
                     issues))
        {
            var prefix =
                BuildSplinePrefix(
                    path.Type,
                    tile,
                    spline.SplineId,
                    pathIndex,
                    direction.Reverse);

            var positions =
                SamplePath(
                    spline.Length,
                    direction.Reverse,
                    options,
                    distance =>
                        GetSplinePathWorldPoint(
                            tile,
                            spline,
                            path,
                            distance));

            AddFunctionalPath(
                path.Type,
                prefix,
                positions,
                source,
                options,
                markers,
                vehiclePaths,
                pedestrianPaths,
                trainPaths,
                issues);
        }
    }

    private static void ConvertSceneryPath(
        OmsiTileReference tile,
        OmsiPlacedObject placedObject,
        OmsiSceneryPathDefinition path,
        int pathIndex,
        Matrix4x4 objectTransform,
        ProtonBusOmsiFunctionalConversionOptions options,
        ICollection<ProtonBusExportMesh> markers,
        ICollection<ProtonBusVehiclePathDefinition> vehiclePaths,
        ICollection<ProtonBusPedestrianPathDefinition> pedestrianPaths,
        ICollection<ProtonBusTrainPathDefinition> trainPaths,
        ICollection<ProtonBusOmsiFunctionalIssue> issues)
    {
        var source =
            $"tile {tile.X},{tile.Y} object {placedObject.ObjectId} path {pathIndex}";

        if (path.Length <= 0.001)
        {
            issues.Add(
                new(
                    "pathLengthInvalid",
                    source,
                    "OMSI scenery path has no usable length."));
            return;
        }

        if (path.BlinkerCode != 0)
        {
            issues.Add(
                new(
                    "blinkerMappingPending",
                    source,
                    $"OMSI blinker code {path.BlinkerCode} was preserved as a diagnostic and is not guessed."));
        }

        if (path.TrafficLightIndex.HasValue)
        {
            issues.Add(
                new(
                    "trafficLightPathLinkPending",
                    source,
                    $"OMSI path references traffic-light index {path.TrafficLightIndex.Value}."));
        }

        foreach (var direction in GetDirections(
                     path.Direction,
                     source,
                     issues))
        {
            var prefix =
                BuildObjectPrefix(
                    path.Type,
                    tile,
                    placedObject.ObjectId,
                    pathIndex,
                    direction.Reverse);

            var positions =
                SamplePath(
                    path.Length,
                    direction.Reverse,
                    options,
                    distance =>
                        GetSceneryPathWorldPoint(
                            path,
                            distance,
                            objectTransform));

            AddFunctionalPath(
                path.Type,
                prefix,
                positions,
                source,
                options,
                markers,
                vehiclePaths,
                pedestrianPaths,
                trainPaths,
                issues);
        }
    }

    private static void AddFunctionalPath(
        int type,
        string prefix,
        IReadOnlyList<Vector3> positions,
        string source,
        ProtonBusOmsiFunctionalConversionOptions options,
        ICollection<ProtonBusExportMesh> markers,
        ICollection<ProtonBusVehiclePathDefinition> vehiclePaths,
        ICollection<ProtonBusPedestrianPathDefinition> pedestrianPaths,
        ICollection<ProtonBusTrainPathDefinition> trainPaths,
        ICollection<ProtonBusOmsiFunctionalIssue> issues)
    {
        if (positions.Count < 2)
        {
            issues.Add(
                new(
                    "pathWaypointCountInvalid",
                    source,
                    "A Proton Bus path requires at least two waypoint positions."));
            return;
        }

        switch (type)
        {
            case 0:
                vehiclePaths.Add(
                    new(
                        Prefix: prefix,
                        Reverse: false,
                        Loop: false,
                        MaxPathsToCheck: positions.Count,
                        IsSpawner: options.VehiclePathsSpawn,
                        IsBusSpawner: false,
                        RightBlinker: false,
                        LeftBlinker: false,
                        SpawnIntervalSeconds:
                            options.VehicleSpawnIntervalSeconds));

                AddMarkers(
                    prefix,
                    positions,
                    options.MarkerHalfSize,
                    markers);
                break;

            case 1:
                pedestrianPaths.Add(
                    new(
                        Prefix: prefix,
                        Reverse: false,
                        Loop: false,
                        MaxPathsToCheck: positions.Count,
                        IsSpawner: options.PedestrianPathsSpawn,
                        SpawnIntervalSeconds:
                            options.PedestrianSpawnIntervalSeconds,
                        AllowBicycle:
                            options.AllowBicycleOnPedestrianPaths));

                AddMarkers(
                    prefix,
                    positions,
                    options.MarkerHalfSize,
                    markers);
                break;

            case 2:
                trainPaths.Add(
                    new(
                        Prefix: prefix,
                        Reverse: false,
                        Loop: false,
                        MaxPathsToCheck: positions.Count,
                        IsSpawner: options.TrainPathsSpawn,
                        SpawnTimeIntervalSeconds:
                            options.TrainSpawnIntervalSeconds));

                AddMarkers(
                    prefix,
                    positions,
                    options.MarkerHalfSize,
                    markers);
                break;

            case 3:
                issues.Add(
                    new(
                        "airPathUnsupported",
                        source,
                        "Proton Bus export currently has no configured air-path writer."));
                break;

            default:
                issues.Add(
                    new(
                        "pathTypeUnsupported",
                        source,
                        $"OMSI path type {type} is not supported."));
                break;
        }
    }

    private static void ConvertSceneryLights(
        OmsiTileReference tile,
        OmsiPlacedObject placedObject,
        ProtonBusResolvedSceneryAsset asset,
        Matrix4x4 objectTransform,
        ProtonBusOmsiFunctionalConversionOptions options,
        ICollection<ProtonBusExportMesh> markers,
        ICollection<ProtonBusStreetLightDefinition> streetLights,
        ICollection<ProtonBusOmsiFunctionalIssue> issues)
    {
        for (var lightIndex = 0;
             lightIndex < asset.ResolvedLightPoints.Count;
             lightIndex++)
        {
            var light =
                asset.ResolvedLightPoints[lightIndex];

            var source =
                $"tile {tile.X},{tile.Y} object {placedObject.ObjectId} light {lightIndex}";

            if (!light.HasRenderableEnhancedData)
            {
                issues.Add(
                    new(
                        "lightPointDataIncomplete",
                        source,
                        $"OMSI light '{light.Keyword}' does not expose a complete enhanced-light position/color set."));
                continue;
            }

            var local =
                new Vector3(
                    (float)light.PositionX!.Value,
                    (float)light.PositionZ!.Value,
                    (float)light.PositionY!.Value);

            var world =
                Vector3.Transform(
                    local,
                    objectTransform);

            var prefix =
                $"pl_t{tile.X}_{tile.Y}_o{placedObject.ObjectId}_l{lightIndex}";

            var color =
                new Vector3(
                    NormalizeColor(
                        light.Red!.Value),
                    NormalizeColor(
                        light.Green!.Value),
                    NormalizeColor(
                        light.Blue!.Value));

            var range =
                light.Range.HasValue &&
                double.IsFinite(light.Range.Value) &&
                light.Range.Value > 0
                    ? light.Range.Value
                    : 15.0;

            var intensity =
                ParseIntensity(
                    light.MultiplicationFactor);

            var definition =
                new ProtonBusStreetLightDefinition(
                    Prefix: prefix,
                    AlwaysOn: false,
                    Real:
                        new(
                            Color: color,
                            Range: range,
                            Intensity: intensity));

            streetLights.Add(
                definition);

            markers.Add(
                ProtonBusMarkerMeshBuilder
                    .Create(
                        definition.RealObjectName,
                        world,
                        options.MarkerHalfSize));
        }
    }

    private static IReadOnlyList<Vector3> SamplePath(
        double length,
        bool reverse,
        ProtonBusOmsiFunctionalConversionOptions options,
        Func<double, Vector3> sample)
    {
        var maximumSegments =
            Math.Max(
                1,
                options.MaxWaypointsPerPath - 1);

        var segmentCount =
            Math.Clamp(
                (int)Math.Ceiling(
                    length /
                    options.WaypointSpacing),
                1,
                maximumSegments);

        var output =
            new Vector3[
                segmentCount + 1];

        for (var index = 0;
             index <= segmentCount;
             index++)
        {
            var fraction =
                (double)index /
                segmentCount;

            var distance =
                reverse
                    ? length *
                      (1.0 - fraction)
                    : length *
                      fraction;

            output[index] =
                sample(distance);
        }

        return output;
    }

    private static Vector3 GetSplinePathWorldPoint(
        OmsiTileReference tile,
        OmsiPlacedSpline spline,
        OmsiSplinePathDefinition path,
        double distance)
    {
        var length =
            Math.Max(
                0.0,
                spline.Length);

        var clamped =
            Math.Clamp(
                distance,
                0.0,
                length);

        var yaw =
            spline.Rotation *
            Math.PI /
            180.0;

        var hasCurve =
            Math.Abs(
                spline.Radius) >
            0.001;

        var curveAngle =
            hasCurve
                ? clamped /
                  spline.Radius
                : 0.0;

        var localX =
            hasCurve
                ? spline.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          curveAngle)
                  )
                : 0.0;

        var localZ =
            hasCurve
                ? spline.Radius *
                  Math.Sin(
                      curveAngle)
                : clamped;

        var cosYaw =
            Math.Cos(
                yaw);

        var sinYaw =
            Math.Sin(
                yaw);

        var worldX =
            OmsiTileGrid.GetOriginX(
                tile.X) +
            spline.X +
            localX *
                cosYaw +
            localZ *
                sinYaw;

        var worldZ =
            OmsiTileGrid.GetOriginZ(
                tile.Y) +
            spline.Y -
            localX *
                sinYaw +
            localZ *
                cosYaw;

        var heading =
            yaw +
            curveAngle;

        var lateral =
            new Vector3(
                (float)Math.Cos(
                    heading),
                0,
                (float)-Math.Sin(
                    heading));

        var worldY =
            spline.Z +
            ProtonBusOmsiSplineTessellator
                .GetGradientRise(
                    spline.GradientStart,
                    spline.GradientEnd,
                    length,
                    clamped);

        return
            new Vector3(
                (float)worldX,
                (float)worldY,
                (float)worldZ) +
            lateral *
                (float)path.X +
            Vector3.UnitY *
                (float)path.Z;
    }

    private static Vector3 GetSceneryPathWorldPoint(
        OmsiSceneryPathDefinition path,
        double distance,
        Matrix4x4 objectTransform)
    {
        var clamped =
            Math.Clamp(
                distance,
                0.0,
                path.Length);

        var heading =
            path.Rotation *
            Math.PI /
            180.0;

        var hasCurve =
            Math.Abs(
                path.Radius) >
            0.001;

        var curveAngle =
            hasCurve
                ? clamped /
                  path.Radius
                : 0.0;

        var localXCurve =
            hasCurve
                ? path.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          curveAngle)
                  )
                : 0.0;

        var localYCurve =
            hasCurve
                ? path.Radius *
                  Math.Sin(
                      curveAngle)
                : clamped;

        var cos =
            Math.Cos(
                heading);

        var sin =
            Math.Sin(
                heading);

        var forwardX =
            localXCurve *
                cos +
            localYCurve *
                sin;

        var forwardY =
            -localXCurve *
                sin +
            localYCurve *
                cos;

        var rise =
            ProtonBusOmsiSplineTessellator
                .GetGradientRise(
                    path.GradientStart,
                    path.GradientEnd,
                    path.Length,
                    clamped);

        var local =
            new Vector3(
                (float)(
                    path.X +
                    forwardX),
                (float)(
                    path.Z +
                    rise),
                (float)(
                    path.Y +
                    forwardY));

        return Vector3.Transform(
            local,
            objectTransform);
    }

    private static Matrix4x4 CreateObjectTransform(
        OmsiTileReference tile,
        OmsiPlacedObject placedObject,
        double terrainOffset)
    {
        var worldX =
            OmsiTileGrid.GetOriginX(
                tile.X) +
            placedObject.X;

        var worldY =
            placedObject.Z +
            terrainOffset;

        var worldZ =
            OmsiTileGrid.GetOriginZ(
                tile.Y) +
            placedObject.Y;

        return
            Matrix4x4
                .CreateFromYawPitchRoll(
                    DegreesToRadians(
                        placedObject.Rotation),
                    DegreesToRadians(
                        placedObject.Pitch),
                    DegreesToRadians(
                        placedObject.Bank)) *
            Matrix4x4
                .CreateTranslation(
                    (float)worldX,
                    (float)worldY,
                    (float)worldZ);
    }

    private static IEnumerable<(bool Reverse, string Suffix)> GetDirections(
        int direction,
        string source,
        ICollection<ProtonBusOmsiFunctionalIssue> issues)
    {
        switch (direction)
        {
            case 0:
                yield return (
                    Reverse: false,
                    Suffix: "f");
                break;

            case 1:
                yield return (
                    Reverse: true,
                    Suffix: "r");
                break;

            case 2:
                yield return (
                    Reverse: false,
                    Suffix: "f");

                yield return (
                    Reverse: true,
                    Suffix: "r");
                break;

            default:
                issues.Add(
                    new(
                        "pathDirectionUnsupported",
                        source,
                        $"OMSI path direction {direction} is outside 0..2."));
                break;
        }
    }

    private static string BuildSplinePrefix(
        int type,
        OmsiTileReference tile,
        int splineId,
        int pathIndex,
        bool reverse) =>
        $"{GetTypePrefix(type)}_t{tile.X}_{tile.Y}_s{splineId}_p{pathIndex}_{(reverse ? "r" : "f")}";

    private static string BuildObjectPrefix(
        int type,
        OmsiTileReference tile,
        int objectId,
        int pathIndex,
        bool reverse) =>
        $"{GetTypePrefix(type)}_t{tile.X}_{tile.Y}_o{objectId}_p{pathIndex}_{(reverse ? "r" : "f")}";

    private static string GetTypePrefix(
        int type) =>
        type switch
        {
            0 => "pv",
            1 => "pp",
            2 => "pt",
            3 => "pa",
            _ => "px"
        };

    private static void AddMarkers(
        string prefix,
        IReadOnlyList<Vector3> positions,
        double markerHalfSize,
        ICollection<ProtonBusExportMesh> output)
    {
        for (var index = 0;
             index < positions.Count;
             index++)
        {
            output.Add(
                ProtonBusMarkerMeshBuilder
                    .Create(
                        prefix +
                        "." +
                        index.ToString(
                            "000",
                            CultureInfo.InvariantCulture),
                        positions[index],
                        markerHalfSize));
        }
    }

    private static float NormalizeColor(
        double value)
    {
        var normalized =
            value > 1.0
                ? value /
                  255.0
                : value;

        return (float)Math.Clamp(
            normalized,
            0.0,
            1.0);
    }

    private static double ParseIntensity(
        string? value)
    {
        if (
            !string.IsNullOrWhiteSpace(
                value) &&
            double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) &&
            double.IsFinite(parsed))
        {
            return Math.Clamp(
                parsed,
                0.0,
                1.0);
        }

        return 1.0;
    }

    private static float DegreesToRadians(
        double value) =>
        (float)(
            value *
            Math.PI /
            180.0);

    private static string BuildObjectSource(
        OmsiTileReference tile,
        OmsiPlacedObject placedObject) =>
        $"tile {tile.X},{tile.Y} object {placedObject.ObjectId}";

    private static bool TryGetByPath<T>(
        IReadOnlyDictionary<string, T> values,
        string path,
        out T? value)
    {
        if (values.TryGetValue(
                path,
                out value))
        {
            return true;
        }

        var normalized =
            NormalizePath(
                path);

        foreach (var pair in values)
        {
            if (string.Equals(
                    NormalizePath(
                        pair.Key),
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
            {
                value =
                    pair.Value;
                return true;
            }
        }

        value =
            default;
        return false;
    }

    private static string NormalizePath(
        string value) =>
        value
            .Trim()
            .Replace(
                '\',
                '/');

    private static void ValidateOptions(
        ProtonBusOmsiFunctionalConversionOptions options)
    {
        if (
            !double.IsFinite(
                options.WaypointSpacing) ||
            options.WaypointSpacing <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Waypoint spacing must be finite and greater than zero.");
        }

        if (
            options.MaxWaypointsPerPath <
            2 ||
            options.MaxWaypointsPerPath >
            999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Max waypoints per path must be between 2 and 999.");
        }

        if (
            !double.IsFinite(
                options.MarkerHalfSize) ||
            options.MarkerHalfSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Marker size must be finite and greater than zero.");
        }
    }
}

public static class ProtonBusMarkerMeshBuilder
{
    private static readonly ProtonBusExportMaterial
        InvisibleMaterial =
            new(
                Name:
                    "proton_marker_invisible",
                Transparent:
                    true,
                DiffuseColor:
                    Vector3.Zero,
                Opacity:
                    0.0f);

    public static ProtonBusExportMesh Create(
        string objectName,
        Vector3 position,
        double halfSize = 0.015)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            objectName);

        if (
            !double.IsFinite(
                halfSize) ||
            halfSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(halfSize));
        }

        var size =
            (float)halfSize;

        return new(
            objectName,
            [
                new(
                    position +
                    new Vector3(
                        -size,
                        0,
                        -size),
                    Vector2.Zero),
                new(
                    position +
                    new Vector3(
                        size,
                        0,
                        -size),
                    Vector2.UnitX),
                new(
                    position +
                    new Vector3(
                        0,
                        0,
                        size *
                        2.0f),
                    Vector2.UnitY)
            ],
            [
                new(
                    0,
                    1,
                    2,
                    InvisibleMaterial.Name)
            ],
            [
                InvisibleMaterial
            ]);
    }
}
