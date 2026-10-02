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
    IReadOnlyList<MapStudioOmsiJunctionTargetArm> Arms);

public sealed record MapStudioOmsiJunctionMatch(
    int NodeId,
    string RelativePath,
    double RotationDegrees,
    double Score,
    IReadOnlyDictionary<int, double> TrimDistanceBySegmentId,
    int MouthCount);

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

            var mouths = BuildMouths(metadata.Paths);
            if (mouths.Count is < 3 or > 6)
            {
                continue;
            }

            signatures.Add(new CandidateSignature(entry.RelativePath, mouths));
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
                if (match is not null && (best is null || match.Score < best.Score))
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
            var valid = true;
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

                if (mouth.DistanceMeters < 1.0 || mouth.DistanceMeters > maximumTrim)
                {
                    valid = false;
                    break;
                }

                angularError += angleError;
                widthError += currentWidthError;
                trims[arm.SegmentId] = mouth.DistanceMeters;
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
                minimalRotation * 0.001;

            var match = new MapStudioOmsiJunctionMatch(
                target.NodeId,
                candidate.RelativePath,
                normalizedRotation,
                score,
                trims,
                mouths.Length);

            if (best is null || match.Score < best.Score)
            {
                best = match;
            }
        }

        return best;
    }

    private static IReadOnlyList<Mouth> BuildMouths(
        IReadOnlyList<OmsiSceneryPathDefinition> paths)
    {
        var endpoints = paths
            .Where(path => path.Type == 0 && path.Length >= 1.0)
            .SelectMany(path => new[]
            {
                BuildEndpoint(path, 0),
                BuildEndpoint(path, path.Length)
            })
            .Where(endpoint =>
                double.IsFinite(endpoint.X) &&
                double.IsFinite(endpoint.Y) &&
                endpoint.Radius > 0.25)
            .ToArray();

        if (endpoints.Length == 0)
        {
            return Array.Empty<Mouth>();
        }

        var maximumRadius = endpoints.Max(endpoint => endpoint.Radius);
        var externalThreshold = Math.Max(1.5, maximumRadius * 0.62);

        var external = endpoints
            .Where(endpoint => endpoint.Radius >= externalThreshold)
            .OrderBy(endpoint => endpoint.OutwardHeadingDegrees)
            .ToArray();

        if (external.Length < 3)
        {
            return Array.Empty<Mouth>();
        }

        var clusters = new List<List<Endpoint>>();

        foreach (var endpoint in external)
        {
            if (clusters.Count == 0 ||
                ForwardAngleGap(
                    clusters[^1][^1].OutwardHeadingDegrees,
                    endpoint.OutwardHeadingDegrees) >
                MouthHeadingClusterDegrees)
            {
                clusters.Add([endpoint]);
            }
            else
            {
                clusters[^1].Add(endpoint);
            }
        }

        if (clusters.Count > 1 &&
            CircularGap(
                clusters[^1][^1].OutwardHeadingDegrees,
                clusters[0][0].OutwardHeadingDegrees) <=
            MouthHeadingClusterDegrees)
        {
            clusters[^1].AddRange(clusters[0]);
            clusters.RemoveAt(0);
        }

        return clusters
            .Select(BuildMouth)
            .OrderBy(mouth => mouth.AngleDegrees)
            .ToArray();
    }

    private static Mouth BuildMouth(IReadOnlyList<Endpoint> cluster)
    {
        var angle = CircularMeanUnsigned(
            cluster.Select(endpoint => endpoint.OutwardHeadingDegrees));
        var radians = DegreesToRadians(angle);
        var lateralX = Math.Cos(radians);
        var lateralY = -Math.Sin(radians);

        var lateral = cluster
            .Select(endpoint => endpoint.X * lateralX + endpoint.Y * lateralY)
            .ToArray();

        var pathWidths = cluster
            .Select(endpoint => Math.Clamp(endpoint.PathWidth, 0.5, 8.0))
            .Order()
            .ToArray();

        var medianWidth = pathWidths[pathWidths.Length / 2];
        var width = lateral.Max() - lateral.Min() + medianWidth;
        var distance = cluster.Average(endpoint => endpoint.Radius);

        return new Mouth(
            angle,
            distance,
            Math.Clamp(width, 2.0, 40.0));
    }

    private static Endpoint BuildEndpoint(
        OmsiSceneryPathDefinition path,
        double distance)
    {
        var clamped = Math.Clamp(distance, 0.0, path.Length);
        var heading = DegreesToRadians(path.Rotation);
        var hasCurve = Math.Abs(path.Radius) > 0.001;
        var curveAngle = hasCurve ? clamped / path.Radius : 0.0;
        var localXCurve = hasCurve
            ? path.Radius * (1.0 - Math.Cos(curveAngle))
            : 0.0;
        var localYCurve = hasCurve
            ? path.Radius * Math.Sin(curveAngle)
            : clamped;

        var cos = Math.Cos(heading);
        var sin = Math.Sin(heading);

        var x =
            path.X +
            localXCurve * cos +
            localYCurve * sin;

        var y =
            path.Y -
            localXCurve * sin +
            localYCurve * cos;

        var radius = Math.Sqrt(x * x + y * y);

        var radialAngle =
            NormalizeAngle(
                Math.Atan2(x, y) *
                180.0 /
                Math.PI);

        var tangentAngle =
            NormalizeAngle(
                path.Rotation +
                curveAngle *
                180.0 /
                Math.PI);

        var outwardHeading =
            Math.Abs(
                SignedAngleDelta(
                    tangentAngle,
                    radialAngle)) <=
                90.0
                ? tangentAngle
                : NormalizeAngle(
                    tangentAngle +
                    180.0);

        return new Endpoint(
            x,
            y,
            radius,
            radialAngle,
            outwardHeading,
            path.Width);
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
        IReadOnlyList<Mouth> Mouths);

    private readonly record struct Mouth(
        double AngleDegrees,
        double DistanceMeters,
        double WidthMeters);

    private readonly record struct Endpoint(
        double X,
        double Y,
        double Radius,
        double AngleDegrees,
        double OutwardHeadingDegrees,
        double PathWidth);
}
