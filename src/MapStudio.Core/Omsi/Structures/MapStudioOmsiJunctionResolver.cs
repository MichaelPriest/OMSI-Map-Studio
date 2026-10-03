using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioOmsiJunctionTargetArm(
    int SegmentId,
    double AngleDegrees,
    double WidthMeters,
    double SegmentLengthMeters);

public sealed record MapStudioOmsiJunctionTarget(
    int NodeId,
    IReadOnlyList<MapStudioOmsiJunctionTargetArm> Arms,
    bool PreferFunctionalTrafficControl = false);

public sealed record MapStudioOmsiJunctionMatch(
    int NodeId,
    string RelativePath,
    double RotationDegrees,
    double Score,
    IReadOnlyDictionary<int, double> TrimDistanceBySegmentId,
    int MouthCount,
    bool HasFunctionalTrafficControl = false,
    double LocalCenterX = 0,
    double LocalCenterY = 0);

public sealed class MapStudioOmsiJunctionResolver
{
    private const double MouthHeadingClusterDegrees = 20.0;
    private const double MaximumArmAngularErrorDegrees = 15.0;

    private readonly OmsiSceneryObjectReader _reader = new();

    public async Task<IReadOnlyDictionary<int, MapStudioOmsiJunctionMatch>> ResolveAsync(
        string omsiRoot,
        IReadOnlyList<OmsiAssetIndexEntry> candidateObjects,
        IReadOnlyList<MapStudioOmsiJunctionTarget> targets,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentNullException.ThrowIfNull(candidateObjects);
        ArgumentNullException.ThrowIfNull(targets);

        if (candidateObjects.Count == 0 || targets.Count == 0)
        {
            return new Dictionary<int, MapStudioOmsiJunctionMatch>();
        }

        var root = Path.GetFullPath(omsiRoot);
        var signatures = new List<CandidateSignature>();

        foreach (var entry in candidateObjects
                     .Where(entry =>
                         entry.Kind == OmsiAssetKind.SceneryObject &&
                         MapStudioOriginalOmsiAssetCatalog.Classify(entry) ==
                         MapStudioOriginalOmsiAssetRole.JunctionObject)
                     .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                     .Take(2_000))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fullPath = Path.Combine(
                root,
                entry.RelativePath
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                continue;
            }

            OmsiSceneryObjectMetadata metadata;
            try
            {
                metadata = await _reader
                    .ReadMetadataAsync(fullPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                continue;
            }

            var geometry = BuildJunctionGeometry(
                metadata.Paths);

            var mouths =
                geometry.Mouths;

            if (mouths.Count is < 3 or > 6)
            {
                continue;
            }

            var hasFunctionalTrafficControl =
                metadata.TrafficLightControllers
                    .Any(
                        controller =>
                            controller.Programs.Count >
                                0) &&
                metadata.Paths
                    .Any(
                        path =>
                            path.Type ==
                                0 &&
                            path.TrafficLightIndex is
                                >= 0);

            signatures.Add(
                new CandidateSignature(
                    entry.RelativePath,
                    mouths,
                    hasFunctionalTrafficControl,
                    geometry.CenterX,
                    geometry.CenterY));
        }

        var result = new Dictionary<int, MapStudioOmsiJunctionMatch>();

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (target.Arms is null || target.Arms.Count is < 3 or > 6)
            {
                continue;
            }

            MapStudioOmsiJunctionMatch? best = null;

            foreach (var candidate in signatures.Where(candidate => candidate.Mouths.Count == target.Arms.Count))
            {
                var match = TryFit(target, candidate);
                if (
                    match is not null &&
                    IsBetterMatch(
                        target,
                        match,
                        best))
                {
                    best = match;
                }
            }

            if (best is not null)
            {
                result[target.NodeId] = best;
            }
        }

        return result;
    }

    private static bool IsBetterMatch(
        MapStudioOmsiJunctionTarget target,
        MapStudioOmsiJunctionMatch candidate,
        MapStudioOmsiJunctionMatch? current)
    {
        if (current is null)
        {
            return true;
        }

        if (
            target.PreferFunctionalTrafficControl &&
            candidate.HasFunctionalTrafficControl !=
                current.HasFunctionalTrafficControl)
        {
            return candidate.HasFunctionalTrafficControl;
        }

        return candidate.Score <
            current.Score;
    }

    private static MapStudioOmsiJunctionMatch? TryFit(
        MapStudioOmsiJunctionTarget target,
        CandidateSignature candidate)
    {
        var targetArms = target.Arms
            .OrderBy(arm => NormalizeAngle(arm.AngleDegrees))
            .ToArray();

        var mouths = candidate.Mouths
            .OrderBy(mouth => mouth.AngleDegrees)
            .ToArray();

        MapStudioOmsiJunctionMatch? best = null;

        for (var shift = 0; shift < mouths.Length; shift++)
        {
            var differences = new double[targetArms.Length];

            for (var index = 0; index < targetArms.Length; index++)
            {
                differences[index] = SignedAngleDelta(
                    mouths[(index + shift) % mouths.Length].AngleDegrees,
                    targetArms[index].AngleDegrees);
            }

            var rotation = CircularMeanSigned(differences);
            var angularError = 0.0;
            var widthError = 0.0;
            var mouthDistanceError = 0.0;
            var valid = true;

            var targetWidthScale =
                targetArms
                    .Max(
                        arm =>
                            Math.Max(
                                2.0,
                                arm.WidthMeters));

            var expectedMouthDistance =
                Math.Clamp(
                    targetWidthScale *
                        0.80,
                    4.0,
                    16.0);

            var maximumReasonableMouthDistance =
                Math.Clamp(
                    targetWidthScale *
                        1.45 +
                    2.0,
                    10.0,
                    24.0);
            var trims = new Dictionary<int, double>();

            for (var index = 0; index < targetArms.Length; index++)
            {
                var arm = targetArms[index];
                var mouth = mouths[(index + shift) % mouths.Length];

                var rotatedAngle = NormalizeAngle(mouth.AngleDegrees + rotation);
                var angleError = Math.Abs(SignedAngleDelta(rotatedAngle, arm.AngleDegrees));

                if (angleError > MaximumArmAngularErrorDegrees)
                {
                    valid = false;
                    break;
                }

                var allowedWidthError = Math.Max(3.0, arm.WidthMeters * 0.45);
                var currentWidthError = Math.Abs(mouth.WidthMeters - arm.WidthMeters);

                if (currentWidthError > allowedWidthError)
                {
                    valid = false;
                    break;
                }

                var maximumTrim = Math.Min(35.0, arm.SegmentLengthMeters * 0.42);

                if (
                    mouth.DistanceMeters <
                        1.0 ||
                    mouth.DistanceMeters >
                        maximumTrim ||
                    mouth.DistanceMeters >
                        maximumReasonableMouthDistance)
                {
                    valid = false;
                    break;
                }

                angularError += angleError;
                widthError += currentWidthError;
                mouthDistanceError +=
                    Math.Abs(
                        mouth.DistanceMeters -
                        expectedMouthDistance);

                trims[arm.SegmentId] =
                    mouth.DistanceMeters;
            }

            if (!valid)
            {
                continue;
            }

            var normalizedRotation =
                NormalizeAngle(
                    rotation);

            var minimalRotation =
                Math.Min(
                    normalizedRotation,
                    360.0 -
                    normalizedRotation);

            var score =
                angularError / targetArms.Length +
                widthError / targetArms.Length * 1.5 +
                mouthDistanceError /
                    targetArms.Length *
                    0.35 +
                minimalRotation * 0.001;

            var match = new MapStudioOmsiJunctionMatch(
                target.NodeId,
                candidate.RelativePath,
                normalizedRotation,
                score,
                trims,
                mouths.Length,
                candidate.HasFunctionalTrafficControl,
                candidate.CenterX,
                candidate.CenterY);

            if (best is null || match.Score < best.Score)
            {
                best = match;
            }
        }

        return best;
    }

    private static JunctionGeometry
        BuildJunctionGeometry(
            IReadOnlyList<OmsiSceneryPathDefinition> paths)
    {
        var rawEndpoints =
            paths
                .Where(
                    path =>
                        path.Type ==
                            0 &&
                        path.Length >=
                            1.0)
                .SelectMany(
                    path =>
                        new[]
                        {
                            BuildRawEndpoint(
                                path,
                                0),
                            BuildRawEndpoint(
                                path,
                                path.Length)
                        })
                .Where(
                    endpoint =>
                        double.IsFinite(
                            endpoint.X) &&
                        double.IsFinite(
                            endpoint.Y))
                .ToArray();

        if (rawEndpoints.Length == 0)
        {
            return JunctionGeometry.Empty;
        }

        var preliminaryCenterX =
            rawEndpoints.Average(
                endpoint =>
                    endpoint.X);

        var preliminaryCenterY =
            rawEndpoints.Average(
                endpoint =>
                    endpoint.Y);

        var endpoints =
            rawEndpoints
                .Select(
                    endpoint =>
                        BuildEndpoint(
                            endpoint,
                            preliminaryCenterX,
                            preliminaryCenterY))
                .Where(
                    endpoint =>
                        endpoint.Radius >
                            0.25)
                .ToArray();

        if (endpoints.Length == 0)
        {
            return JunctionGeometry.Empty;
        }

        var maximumRadius =
            endpoints.Max(
                endpoint =>
                    endpoint.Radius);

        var externalThreshold =
            Math.Max(
                1.5,
                maximumRadius *
                    0.62);

        var external =
            endpoints
                .Where(
                    endpoint =>
                        endpoint.Radius >=
                            externalThreshold)
                .OrderBy(
                    endpoint =>
                        endpoint
                            .OutwardHeadingDegrees)
                .ToArray();

        if (external.Length < 3)
        {
            return JunctionGeometry.Empty;
        }

        var clusters =
            new List<List<Endpoint>>();

        foreach (var endpoint in external)
        {
            if (
                clusters.Count ==
                    0 ||
                ForwardAngleGap(
                    clusters[^1][^1]
                        .OutwardHeadingDegrees,
                    endpoint
                        .OutwardHeadingDegrees) >
                    MouthHeadingClusterDegrees)
            {
                clusters.Add(
                    [endpoint]);
            }
            else
            {
                clusters[^1]
                    .Add(
                        endpoint);
            }
        }

        if (
            clusters.Count >
                1 &&
            CircularGap(
                clusters[^1][^1]
                    .OutwardHeadingDegrees,
                clusters[0][0]
                    .OutwardHeadingDegrees) <=
                MouthHeadingClusterDegrees)
        {
            clusters[^1]
                .AddRange(
                    clusters[0]);

            clusters.RemoveAt(
                0);
        }

        var roughMouths =
            clusters
                .Select(
                    BuildMouth)
                .OrderBy(
                    mouth =>
                        mouth.AngleDegrees)
                .ToArray();

        if (roughMouths.Length is < 3 or > 6)
        {
            return JunctionGeometry.Empty;
        }

        var center =
            ResolveJunctionCenter(
                roughMouths,
                preliminaryCenterX,
                preliminaryCenterY);

        var mouths =
            roughMouths
                .Select(
                    mouth =>
                        mouth with
                        {
                            DistanceMeters =
                                Math.Sqrt(
                                    Math.Pow(
                                        mouth.X -
                                        center.X,
                                        2) +
                                    Math.Pow(
                                        mouth.Y -
                                        center.Y,
                                        2))
                        })
                .OrderBy(
                    mouth =>
                        mouth.AngleDegrees)
                .ToArray();

        return new JunctionGeometry(
            center.X,
            center.Y,
            mouths);
    }

    private static Mouth BuildMouth(
        IReadOnlyList<Endpoint> cluster)
    {
        var angle =
            CircularMeanUnsigned(
                cluster.Select(
                    endpoint =>
                        endpoint
                            .OutwardHeadingDegrees));

        var radians =
            DegreesToRadians(
                angle);

        var lateralX =
            Math.Cos(
                radians);

        var lateralY =
            -Math.Sin(
                radians);

        var lateral =
            cluster
                .Select(
                    endpoint =>
                        endpoint.X *
                            lateralX +
                        endpoint.Y *
                            lateralY)
                .ToArray();

        var pathWidths =
            cluster
                .Select(
                    endpoint =>
                        Math.Clamp(
                            endpoint.PathWidth,
                            0.5,
                            8.0))
                .Order()
                .ToArray();

        var medianWidth =
            pathWidths[
                pathWidths.Length /
                2];

        var width =
            lateral.Max() -
            lateral.Min() +
            medianWidth;

        return new Mouth(
            angle,
            DistanceMeters:
                0,
            Math.Clamp(
                width,
                2.0,
                40.0),
            cluster.Average(
                endpoint =>
                    endpoint.X),
            cluster.Average(
                endpoint =>
                    endpoint.Y));
    }

    private static (double X, double Y)
        ResolveJunctionCenter(
            IReadOnlyList<Mouth> mouths,
            double fallbackX,
            double fallbackY)
    {
        double a11 = 0;
        double a12 = 0;
        double a22 = 0;
        double b1 = 0;
        double b2 = 0;

        foreach (var mouth in mouths)
        {
            var radians =
                DegreesToRadians(
                    mouth.AngleDegrees);

            var normalX =
                Math.Cos(
                    radians);

            var normalY =
                -Math.Sin(
                    radians);

            var rhs =
                normalX *
                    mouth.X +
                normalY *
                    mouth.Y;

            a11 +=
                normalX *
                normalX;

            a12 +=
                normalX *
                normalY;

            a22 +=
                normalY *
                normalY;

            b1 +=
                normalX *
                rhs;

            b2 +=
                normalY *
                rhs;
        }

        var determinant =
            a11 *
                a22 -
            a12 *
                a12;

        if (
            Math.Abs(
                determinant) <
                1e-6)
        {
            return (
                fallbackX,
                fallbackY
            );
        }

        var x =
            (
                b1 *
                    a22 -
                b2 *
                    a12
            ) /
            determinant;

        var y =
            (
                a11 *
                    b2 -
                a12 *
                    b1
            ) /
            determinant;

        return
            double.IsFinite(
                x) &&
            double.IsFinite(
                y)
                ? (
                    x,
                    y
                )
                : (
                    fallbackX,
                    fallbackY
                );
    }

    private static RawEndpoint
        BuildRawEndpoint(
            OmsiSceneryPathDefinition path,
            double distance)
    {
        var clamped =
            Math.Clamp(
                distance,
                0.0,
                path.Length);

        var heading =
            DegreesToRadians(
                path.Rotation);

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

        var x =
            path.X +
            localXCurve *
                cos +
            localYCurve *
                sin;

        var y =
            path.Y -
            localXCurve *
                sin +
            localYCurve *
                cos;

        var tangentAngle =
            NormalizeAngle(
                path.Rotation +
                curveAngle *
                180.0 /
                Math.PI);

        return new RawEndpoint(
            x,
            y,
            tangentAngle,
            path.Width);
    }

    private static Endpoint
        BuildEndpoint(
            RawEndpoint endpoint,
            double centerX,
            double centerY)
    {
        var dx =
            endpoint.X -
            centerX;

        var dy =
            endpoint.Y -
            centerY;

        var radius =
            Math.Sqrt(
                dx *
                    dx +
                dy *
                    dy);

        var radialAngle =
            NormalizeAngle(
                Math.Atan2(
                    dx,
                    dy) *
                180.0 /
                Math.PI);

        var outwardHeading =
            Math.Abs(
                SignedAngleDelta(
                    endpoint
                        .TangentAngleDegrees,
                    radialAngle)) <=
                90.0
                ? endpoint
                    .TangentAngleDegrees
                : NormalizeAngle(
                    endpoint
                        .TangentAngleDegrees +
                    180.0);

        return new Endpoint(
            endpoint.X,
            endpoint.Y,
            radius,
            outwardHeading,
            endpoint.PathWidth);
    }

    private static double CircularMeanUnsigned(IEnumerable<double> angles)
    {
        var values = angles.Select(DegreesToRadians).ToArray();
        var x = values.Sum(Math.Cos);
        var y = values.Sum(Math.Sin);
        return NormalizeAngle(Math.Atan2(y, x) * 180.0 / Math.PI);
    }

    private static double CircularMeanSigned(IEnumerable<double> angles)
    {
        var values = angles.Select(DegreesToRadians).ToArray();
        var x = values.Sum(Math.Cos);
        var y = values.Sum(Math.Sin);
        return Math.Atan2(y, x) * 180.0 / Math.PI;
    }

    private static double SignedAngleDelta(double from, double to)
    {
        var delta = NormalizeAngle(to) - NormalizeAngle(from);

        while (delta > 180.0)
        {
            delta -= 360.0;
        }

        while (delta <= -180.0)
        {
            delta += 360.0;
        }

        return delta;
    }

    private static double ForwardAngleGap(double from, double to) =>
        NormalizeAngle(to - from);

    private static double CircularGap(double last, double first) =>
        NormalizeAngle(first + 360.0 - last);

    private static double NormalizeAngle(double value)
    {
        value %= 360.0;
        if (value < 0)
        {
            value += 360.0;
        }

        return value;
    }

    private static double DegreesToRadians(double value) =>
        value * Math.PI / 180.0;

    private sealed record CandidateSignature(
        string RelativePath,
        IReadOnlyList<Mouth> Mouths,
        bool HasFunctionalTrafficControl,
        double CenterX,
        double CenterY);

    private sealed record JunctionGeometry(
        double CenterX,
        double CenterY,
        IReadOnlyList<Mouth> Mouths)
    {
        public static JunctionGeometry Empty { get; } =
            new(
                0,
                0,
                Array.Empty<Mouth>());
    }

    private readonly record struct Mouth(
        double AngleDegrees,
        double DistanceMeters,
        double WidthMeters,
        double X,
        double Y);

    private readonly record struct RawEndpoint(
        double X,
        double Y,
        double TangentAngleDegrees,
        double PathWidth);

    private readonly record struct Endpoint(
        double X,
        double Y,
        double Radius,
        double OutwardHeadingDegrees,
        double PathWidth);
}
