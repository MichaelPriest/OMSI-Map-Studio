using System.Globalization;
using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;
using MapStudio.Core.Omsi.Splines;
using MapStudio.Core.Omsi.Timetables;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiGpsRouteOptions(
    double Width = 1.0,
    double ElevationOffset = 0.05,
    double MaximumSampleLength = 3.0,
    double MaximumPartLength = 3000.0,
    string MaterialName = "gps_route")
{
    public Vector3? DiffuseColor { get; init; }
}

public sealed record ProtonBusOmsiGpsRouteIssue(
    string Code,
    string TripName,
    string? Detail = null);

public sealed record ProtonBusOmsiGpsRouteResult(
    string TripName,
    string EntrypointName,
    ProtonBusExportScene Scene,
    int ResolvedReferenceCount,
    int TotalReferenceCount,
    IReadOnlyList<ProtonBusOmsiGpsRouteIssue> Issues)
{
    public bool IsComplete =>
        TotalReferenceCount >
            0 &&
        ResolvedReferenceCount ==
            TotalReferenceCount &&
        Scene.Meshes.Count >
            0;
}

public sealed record ProtonBusOmsiGpsRoutesResult(
    ProtonBusExportScene Scene,
    IReadOnlyList<ProtonBusOmsiGpsRouteResult> Routes,
    IReadOnlyList<ProtonBusOmsiGpsRouteIssue> Issues);

public static class ProtonBusOmsiGpsRouteBuilder
{
    public static ProtonBusOmsiGpsRoutesResult Build(
        IReadOnlyList<OmsiTileReference> tileOrder,
        OmsiTimetableCatalog timetable,
        IReadOnlyDictionary<(int X, int Y), OmsiTileContent> tileContents,
        IReadOnlyDictionary<(int X, int Y), ProtonBusOmsiAssetResolutionResult> assetsByTile,
        IReadOnlyList<ProtonBusOmsiTripEntrypoint> tripEntrypoints,
        ProtonBusOmsiGpsRouteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(
            tileOrder);

        ArgumentNullException.ThrowIfNull(
            timetable);

        ArgumentNullException.ThrowIfNull(
            tileContents);

        ArgumentNullException.ThrowIfNull(
            assetsByTile);

        ArgumentNullException.ThrowIfNull(
            tripEntrypoints);

        options ??=
            new();

        ValidateOptions(
            options);

        var routes =
            new List<ProtonBusOmsiGpsRouteResult>();

        var allMeshes =
            new List<ProtonBusExportMesh>();

        var allIssues =
            new List<ProtonBusOmsiGpsRouteIssue>();

        foreach (
            var mapping
            in tripEntrypoints)
        {
            var route =
                BuildTrip(
                    tileOrder,
                    timetable,
                    tileContents,
                    assetsByTile,
                    mapping.TripName,
                    mapping.EntrypointName,
                    options);

            routes.Add(
                route);

            allMeshes.AddRange(
                route.Scene.Meshes);

            allIssues.AddRange(
                route.Issues);
        }

        return new(
            new(
                allMeshes.ToArray()),
            routes.ToArray(),
            allIssues.ToArray());
    }

    public static ProtonBusOmsiGpsRouteResult BuildTrip(
        IReadOnlyList<OmsiTileReference> tileOrder,
        OmsiTimetableCatalog timetable,
        IReadOnlyDictionary<(int X, int Y), OmsiTileContent> tileContents,
        IReadOnlyDictionary<(int X, int Y), ProtonBusOmsiAssetResolutionResult> assetsByTile,
        string tripName,
        string entrypointName,
        ProtonBusOmsiGpsRouteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(
            tileOrder);

        ArgumentNullException.ThrowIfNull(
            timetable);

        ArgumentNullException.ThrowIfNull(
            tileContents);

        ArgumentNullException.ThrowIfNull(
            assetsByTile);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            tripName);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            entrypointName);

        options ??=
            new();

        ValidateOptions(
            options);

        var issues =
            new List<ProtonBusOmsiGpsRouteIssue>();

        var trip =
            timetable
                .Trips
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Name,
                            tripName,
                            StringComparison.OrdinalIgnoreCase));

        if (
            trip is null)
        {
            issues.Add(
                new(
                    "gpsTripMissing",
                    tripName));

            return Empty(
                tripName,
                entrypointName,
                issues);
        }

        var references =
            ResolveTripReferences(
                timetable,
                trip,
                issues);

        if (
            references.Count ==
            0)
        {
            issues.Add(
                new(
                    "gpsRouteReferencesMissing",
                    tripName));

            return new(
                tripName,
                entrypointName,
                new(
                    Array.Empty<ProtonBusExportMesh>()),
                0,
                0,
                issues);
        }

        var polyline =
            new List<Vector3>();

        var resolved =
            0;

        foreach (
            var reference
            in references)
        {
            if (
                !TryResolveReference(
                    tileOrder,
                    tileContents,
                    assetsByTile,
                    reference,
                    options,
                    out var points,
                    out var issue))
            {
                if (
                    issue is
                        not null)
                {
                    issues.Add(
                        issue with
                        {
                            TripName =
                                tripName
                        });
                }

                continue;
            }

            AppendPolyline(
                polyline,
                points);

            resolved++;
        }

        if (
            resolved !=
                references.Count)
        {
            issues.Add(
                new(
                    "gpsRouteIncomplete",
                    tripName,
                    $"Resolved {resolved} of {references.Count} timetable path references. GPS mesh was not emitted to avoid drawing a misleading route."));

            return new(
                tripName,
                entrypointName,
                new(
                    Array.Empty<ProtonBusExportMesh>()),
                resolved,
                references.Count,
                issues);
        }

        if (
            polyline.Count <
            2)
        {
            issues.Add(
                new(
                    "gpsRouteGeometryEmpty",
                    tripName));

            return new(
                tripName,
                entrypointName,
                new(
                    Array.Empty<ProtonBusExportMesh>()),
                resolved,
                references.Count,
                issues);
        }

        var parts =
            SplitPolyline(
                polyline,
                options.MaximumPartLength);

        var meshes =
            new List<ProtonBusExportMesh>(
                parts.Count);

        for (
            var partIndex = 0;
            partIndex <
                parts.Count;
            partIndex++)
        {
            var suffix =
                partIndex ==
                    0
                    ? null
                    : $"part.{partIndex:000}";

            meshes.Add(
                BuildRibbon(
                    ProtonBusGpsRouteNamePlanner
                        .BuildObjectName(
                            entrypointName,
                            suffix),
                    parts[
                        partIndex],
                    options,
                    partIndex));
        }

        return new(
            tripName,
            entrypointName,
            new(
                meshes.ToArray()),
            resolved,
            references.Count,
            issues);
    }

    private static IReadOnlyList<RouteReference>
        ResolveTripReferences(
            OmsiTimetableCatalog timetable,
            OmsiTimetableTrip trip,
            ICollection<ProtonBusOmsiGpsRouteIssue>
                issues)
    {
        var output =
            new List<RouteReference>();

        if (
            trip.UsesStationLinks)
        {
            var stations =
                trip
                    .Stations
                    .OfType<
                        OmsiTimetableTripStationType2>()
                    .ToArray();

            for (
                var index = 0;
                index <
                    stations.Length -
                    1;
                index++)
            {
                var start =
                    stations[index]
                        .Id;

                var end =
                    stations[
                        index +
                        1]
                        .Id;

                var link =
                    timetable
                        .StationLinks
                        .FirstOrDefault(
                            candidate =>
                                candidate
                                    .StartBusStopId ==
                                    start &&
                                candidate
                                    .EndBusStopId ==
                                    end);

                if (
                    link is null)
                {
                    issues.Add(
                        new(
                            "gpsStationLinkMissing",
                            trip.Name,
                            $"StationLink {start}->{end} was not found."));
                    continue;
                }

                foreach (
                    var entry
                    in link.Entries)
                {
                    output.Add(
                        new(
                            entry.TileIndex,
                            entry.Id,
                            entry.Line2,
                            entry.Length));
                }
            }

            return output;
        }

        var track =
            timetable
                .Tracks
                .FirstOrDefault(
                    candidate =>
                        TrackNamesMatch(
                            candidate.Name,
                            trip.EffectiveTrackName));

        if (
            track is null)
        {
            issues.Add(
                new(
                    "gpsTrackMissing",
                    trip.Name,
                    $"Track '{trip.EffectiveTrackName}' was not found."));

            return output;
        }

        foreach (
            var entry
            in track.Entries)
        {
            output.Add(
                new(
                    entry.TileIndex,
                    entry.Id,
                    entry.Line2,
                    entry.Length));
        }

        return output;
    }

    private static bool TryResolveReference(
        IReadOnlyList<OmsiTileReference> tileOrder,
        IReadOnlyDictionary<(int X, int Y), OmsiTileContent> tileContents,
        IReadOnlyDictionary<(int X, int Y), ProtonBusOmsiAssetResolutionResult> assetsByTile,
        RouteReference reference,
        ProtonBusOmsiGpsRouteOptions options,
        out IReadOnlyList<Vector3> points,
        out ProtonBusOmsiGpsRouteIssue? issue)
    {
        points =
            Array.Empty<Vector3>();

        issue =
            null;

        if (
            reference.TileIndex <
                0 ||
            reference.TileIndex >=
                tileOrder.Count)
        {
            issue =
                new(
                    "gpsTileIndexOutOfRange",
                    string.Empty,
                    $"TileIndex {reference.TileIndex} is outside 0..{Math.Max(0, tileOrder.Count - 1)}.");

            return false;
        }

        if (
            !int.TryParse(
                reference.PathIndexText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var pathIndex) ||
            pathIndex <
            0)
        {
            issue =
                new(
                    "gpsPathIndexInvalid",
                    string.Empty,
                    $"Path index '{reference.PathIndexText}' is not a non-negative integer.");

            return false;
        }

        var tile =
            tileOrder[
                reference.TileIndex];

        if (
            !tileContents.TryGetValue(
                (
                    tile.X,
                    tile.Y
                ),
                out var content) ||
            !assetsByTile.TryGetValue(
                (
                    tile.X,
                    tile.Y
                ),
                out var assets))
        {
            issue =
                new(
                    "gpsTileNotLoaded",
                    string.Empty,
                    $"Tile {tile.X},{tile.Y} is not loaded.");

            return false;
        }

        var spline =
            content
                .Splines
                .FirstOrDefault(
                    candidate =>
                        candidate.SplineId ==
                        reference.EntityId);

        if (
            spline is
                not null)
        {
            if (
                !TryGetByPath(
                    assets.SplineDefinitions,
                    spline.SplinePath,
                    out var definition) ||
                definition is null ||
                pathIndex >=
                    definition.Paths.Count)
            {
                issue =
                    new(
                        "gpsSplinePathMissing",
                        string.Empty,
                        $"Spline {reference.EntityId} path {pathIndex} cannot be resolved.");

                return false;
            }

            var length =
                Math.Min(
                    spline.Length,
                    reference.Length is
                        > 0
                        ? reference.Length.Value
                        : spline.Length);

            points =
                Sample(
                    length,
                    options.MaximumSampleLength,
                    distance =>
                        GetSplinePoint(
                            tile,
                            spline,
                            definition
                                .Paths[
                                    pathIndex],
                            distance,
                            options.ElevationOffset));

            return
                points.Count >=
                2;
        }

        var placedObject =
            content
                .Objects
                .FirstOrDefault(
                    candidate =>
                        candidate.ObjectId ==
                        reference.EntityId);

        if (
            placedObject is
                null ||
            !TryGetByPath(
                assets.SceneryAssets,
                placedObject.SceneryObjectPath,
                out var sceneryAsset) ||
            sceneryAsset is null ||
            pathIndex >=
                sceneryAsset
                    .ResolvedPaths
                    .Count)
        {
            issue =
                new(
                    "gpsEntityPathMissing",
                    string.Empty,
                    $"Entity {reference.EntityId} path {pathIndex} cannot be resolved on tile {tile.X},{tile.Y}.");

            return false;
        }

        var sceneryPath =
            sceneryAsset
                .ResolvedPaths[
                    pathIndex];

        var objectLength =
            Math.Min(
                sceneryPath.Length,
                reference.Length is
                    > 0
                    ? reference.Length.Value
                    : sceneryPath.Length);

        var terrainOffset =
            sceneryAsset
                .UsesAbsoluteHeight
                ? 0.0
                : ProtonBusOmsiTerrainSampler
                    .GetHeightAtLocalPoint(
                        content.Terrain,
                        placedObject.X,
                        placedObject.Y);

        var transform =
            CreateObjectTransform(
                tile,
                placedObject,
                terrainOffset);

        points =
            Sample(
                objectLength,
                options.MaximumSampleLength,
                distance =>
                    GetObjectPoint(
                        sceneryPath,
                        distance,
                        transform,
                        options.ElevationOffset));

        return
            points.Count >=
            2;
    }

    private static IReadOnlyList<Vector3> Sample(
        double length,
        double maximumSampleLength,
        Func<double, Vector3> sampler)
    {
        if (
            length <=
            0.01)
        {
            return Array.Empty<Vector3>();
        }

        var segments =
            Math.Max(
                2,
                (int)Math.Ceiling(
                    length /
                    maximumSampleLength));

        var points =
            new Vector3[
                segments +
                1];

        for (
            var index = 0;
            index <=
                segments;
            index++)
        {
            points[
                index] =
                sampler(
                    length *
                    index /
                    segments);
        }

        return points;
    }

    private static Vector3 GetSplinePoint(
        OmsiTileReference tile,
        OmsiPlacedSpline spline,
        OmsiSplinePathDefinition path,
        double distance,
        double elevationOffset)
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

        var curved =
            Math.Abs(
                spline.Radius) >
            0.001;

        var angle =
            curved
                ? clamped /
                  spline.Radius
                : 0.0;

        var curveX =
            curved
                ? spline.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          angle)
                  )
                : 0.0;

        var curveZ =
            curved
                ? spline.Radius *
                  Math.Sin(
                      angle)
                : clamped;

        var cos =
            Math.Cos(
                yaw);

        var sin =
            Math.Sin(
                yaw);

        var center =
            new Vector3(
                (float)(
                    OmsiTileGrid
                        .GetOriginX(
                            tile.X) +
                    spline.X +
                    curveX *
                        cos +
                    curveZ *
                        sin),
                (float)(
                    spline.Z +
                    ProtonBusOmsiSplineTessellator
                        .GetGradientRise(
                            spline.GradientStart,
                            spline.GradientEnd,
                            length,
                            clamped)),
                (float)(
                    OmsiTileGrid
                        .GetOriginZ(
                            tile.Y) +
                    spline.Y -
                    curveX *
                        sin +
                    curveZ *
                        cos));

        var heading =
            yaw +
            angle;

        var lateral =
            new Vector3(
                (float)Math.Cos(
                    heading),
                0,
                (float)-Math.Sin(
                    heading));

        return
            center +
            lateral *
                (float)path.X +
            Vector3.UnitY *
                (float)(
                    path.Z +
                    elevationOffset);
    }

    private static Vector3 GetObjectPoint(
        OmsiSceneryPathDefinition path,
        double distance,
        Matrix4x4 transform,
        double elevationOffset)
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

        var curved =
            Math.Abs(
                path.Radius) >
            0.001;

        var angle =
            curved
                ? clamped /
                  path.Radius
                : 0.0;

        var curveX =
            curved
                ? path.Radius *
                  (
                      1.0 -
                      Math.Cos(
                          angle)
                  )
                : 0.0;

        var curveY =
            curved
                ? path.Radius *
                  Math.Sin(
                      angle)
                : clamped;

        var cos =
            Math.Cos(
                heading);

        var sin =
            Math.Sin(
                heading);

        var localX =
            path.X +
            curveX *
                cos +
            curveY *
                sin;

        var localY =
            path.Y -
            curveX *
                sin +
            curveY *
                cos;

        var rise =
            ProtonBusOmsiSplineTessellator
                .GetGradientRise(
                    path.GradientStart,
                    path.GradientEnd,
                    path.Length,
                    clamped);

        return Vector3.Transform(
            new(
                (float)localX,
                (float)(
                    path.Z +
                    rise +
                    elevationOffset),
                (float)localY),
            transform);
    }

    private static ProtonBusExportMesh BuildRibbon(
        string objectName,
        IReadOnlyList<Vector3> points,
        ProtonBusOmsiGpsRouteOptions options,
        int partIndex)
    {
        var width =
            (float)options.Width;

        var halfWidth =
            width /
            2.0f;

        var color =
            options.DiffuseColor ??
            Vector3.One;

        var materialName =
            $"{options.MaterialName}_{partIndex}";

        var vertices =
            new List<ProtonBusExportVertex>(
                points.Count *
                2);

        var cumulative =
            0.0f;

        for (
            var index = 0;
            index <
                points.Count;
            index++)
        {
            if (
                index >
                0)
            {
                cumulative +=
                    Vector3.Distance(
                        points[
                            index -
                            1],
                        points[
                            index]);
            }

            var previous =
                points[
                    Math.Max(
                        0,
                        index -
                        1)];

            var next =
                points[
                    Math.Min(
                        points.Count -
                            1,
                        index +
                        1)];

            var direction =
                next -
                previous;

            direction.Y =
                0;

            if (
                direction.LengthSquared() <
                0.000001f)
            {
                direction =
                    Vector3.UnitZ;
            }
            else
            {
                direction =
                    Vector3.Normalize(
                        direction);
            }

            var lateral =
                new Vector3(
                    direction.Z,
                    0,
                    -direction.X);

            vertices.Add(
                new(
                    points[index] -
                    lateral *
                        halfWidth,
                    new(
                        0,
                        cumulative /
                            10.0f)));

            vertices.Add(
                new(
                    points[index] +
                    lateral *
                        halfWidth,
                    new(
                        1,
                        cumulative /
                            10.0f)));
        }

        var triangles =
            new List<ProtonBusExportTriangle>(
                Math.Max(
                    0,
                    points.Count -
                        1) *
                2);

        for (
            var index = 0;
            index <
                points.Count -
                    1;
            index++)
        {
            var left0 =
                index *
                2;

            var right0 =
                left0 +
                1;

            var left1 =
                left0 +
                2;

            var right1 =
                left0 +
                3;

            triangles.Add(
                new(
                    left0,
                    left1,
                    right1,
                    materialName));

            triangles.Add(
                new(
                    left0,
                    right1,
                    right0,
                    materialName));
        }

        return new(
            objectName,
            vertices.ToArray(),
            triangles.ToArray(),
            [
                new(
                    materialName,
                    DiffuseColor:
                        color)
            ]);
    }

    private static IReadOnlyList<IReadOnlyList<Vector3>>
        SplitPolyline(
            IReadOnlyList<Vector3> points,
            double maximumPartLength)
    {
        var output =
            new List<IReadOnlyList<Vector3>>();

        var current =
            new List<Vector3>
            {
                points[0]
            };

        var currentLength =
            0.0;

        for (
            var index = 1;
            index <
                points.Count;
            index++)
        {
            var previous =
                points[
                    index -
                    1];

            var point =
                points[index];

            var segment =
                Vector3.Distance(
                    previous,
                    point);

            if (
                current.Count >
                    1 &&
                currentLength +
                    segment >
                maximumPartLength)
            {
                output.Add(
                    current.ToArray());

                current =
                [
                    previous
                ];

                currentLength =
                    0.0;
            }

            current.Add(
                point);

            currentLength +=
                segment;
        }

        if (
            current.Count >=
            2)
        {
            output.Add(
                current.ToArray());
        }

        return output;
    }

    private static void AppendPolyline(
        ICollection<Vector3> output,
        IReadOnlyList<Vector3> points)
    {
        foreach (
            var point
            in points)
        {
            if (
                output.LastOrDefault() is
                    { } last &&
                output.Count >
                    0 &&
                Vector3.DistanceSquared(
                    last,
                    point) <
                0.0001f)
            {
                continue;
            }

            output.Add(
                point);
        }
    }

    private static Matrix4x4 CreateObjectTransform(
        OmsiTileReference tile,
        OmsiPlacedObject placedObject,
        double terrainOffset) =>
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
                (float)(
                    OmsiTileGrid
                        .GetOriginX(
                            tile.X) +
                    placedObject.X),
                (float)(
                    placedObject.Z +
                    terrainOffset),
                (float)(
                    OmsiTileGrid
                        .GetOriginZ(
                            tile.Y) +
                    placedObject.Y));

    private static bool TryGetByPath<T>(
        IReadOnlyDictionary<string, T> values,
        string path,
        out T? value)
    {
        if (
            values.TryGetValue(
                path,
                out value))
        {
            return true;
        }

        var normalized =
            path
                .Trim()
                .Replace(
                    '\\',
                    '/');

        foreach (
            var pair
            in values)
        {
            if (
                string.Equals(
                    pair.Key
                        .Trim()
                        .Replace(
                            '\\',
                            '/'),
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

    private static bool TrackNamesMatch(
        string left,
        string right) =>
        string.Equals(
            Path.GetFileNameWithoutExtension(
                left.Trim()),
            Path.GetFileNameWithoutExtension(
                right.Trim()),
            StringComparison.OrdinalIgnoreCase);

    private static void ValidateOptions(
        ProtonBusOmsiGpsRouteOptions options)
    {
        if (
            !double.IsFinite(
                options.Width) ||
            options.Width <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "GPS route width must be finite and greater than zero.");
        }

        if (
            !double.IsFinite(
                options.ElevationOffset))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "GPS elevation offset must be finite.");
        }

        if (
            !double.IsFinite(
                options.MaximumSampleLength) ||
            options.MaximumSampleLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "GPS maximum sample length must be finite and greater than zero.");
        }

        if (
            !double.IsFinite(
                options.MaximumPartLength) ||
            options.MaximumPartLength <=
            0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "GPS maximum part length must be finite and greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(
            options.MaterialName);
    }

    private static ProtonBusOmsiGpsRouteResult Empty(
        string tripName,
        string entrypointName,
        IReadOnlyList<ProtonBusOmsiGpsRouteIssue> issues) =>
        new(
            tripName,
            entrypointName,
            new(
                Array.Empty<ProtonBusExportMesh>()),
            0,
            0,
            issues);

    private static float DegreesToRadians(
        double value) =>
        (float)(
            value *
            Math.PI /
            180.0);

    private sealed record RouteReference(
        int TileIndex,
        int EntityId,
        string PathIndexText,
        double? Length);
}
