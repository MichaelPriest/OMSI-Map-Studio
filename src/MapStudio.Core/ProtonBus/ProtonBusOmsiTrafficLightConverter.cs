using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusOmsiTrafficLightConversionOptions(
    double TriggerRadius = 1.1,
    double MarkerHalfSize = 0.015,
    bool RandomTimestampAtStart = true);

public sealed record ProtonBusOmsiTrafficLightConversionIssue(
    string Code,
    string Source,
    string? Detail = null);

public sealed record ProtonBusOmsiTrafficLightConversionResult(
    IReadOnlyList<ProtonBusTrafficLightDefinition> TrafficLights,
    ProtonBusExportScene MarkerScene,
    IReadOnlyList<ProtonBusOmsiTrafficLightConversionIssue> Issues);

public static class ProtonBusOmsiTrafficLightConverter
{
    private const long MicrosecondsPerSecond =
        1_000_000;

    public static ProtonBusOmsiTrafficLightConversionResult Convert(
        OmsiTileReference tile,
        OmsiTileContent content,
        IReadOnlyDictionary<string, ProtonBusResolvedSceneryAsset> sceneryAssets,
        ProtonBusOmsiTrafficLightConversionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sceneryAssets);

        options ??=
            new();

        ValidateOptions(options);

        var definitions =
            new List<ProtonBusTrafficLightDefinition>();

        var markers =
            new List<ProtonBusExportMesh>();

        var issues =
            new List<ProtonBusOmsiTrafficLightConversionIssue>();

        foreach (var placedObject in content.Objects)
        {
            if (!TryGetByPath(
                    sceneryAssets,
                    placedObject.SceneryObjectPath,
                    out var asset) ||
                asset is null ||
                asset.ResolvedTrafficLightControllers.Count == 0)
            {
                continue;
            }

            var source =
                $"tile {tile.X},{tile.Y} object {placedObject.ObjectId}";

            if (asset.ResolvedTrafficLightControllers.Count != 1)
            {
                issues.Add(
                    new(
                        "trafficLightControllerAmbiguous",
                        source,
                        $"{asset.ResolvedTrafficLightControllers.Count} controllers exist in the same scenery object; path-to-controller association is not preserved by OMSI metadata."));
                continue;
            }

            var controller =
                asset.ResolvedTrafficLightControllers[0];

            var conversion =
                ConvertController(
                    tile,
                    content,
                    placedObject,
                    asset,
                    controller,
                    controllerIndex:
                        0,
                    options);

            definitions.AddRange(
                conversion.TrafficLights);

            markers.AddRange(
                conversion.MarkerScene.Meshes);

            issues.AddRange(
                conversion.Issues);
        }

        return new(
            definitions.ToArray(),
            new(
                markers.ToArray()),
            issues.ToArray());
    }

    private static ProtonBusOmsiTrafficLightConversionResult
        ConvertController(
            OmsiTileReference tile,
            OmsiTileContent content,
            OmsiPlacedObject placedObject,
            ProtonBusResolvedSceneryAsset asset,
            OmsiTrafficLightController controller,
            int controllerIndex,
            ProtonBusOmsiTrafficLightConversionOptions options)
    {
        var source =
            $"tile {tile.X},{tile.Y} object {placedObject.ObjectId} controller {controllerIndex}";

        var issues =
            new List<ProtonBusOmsiTrafficLightConversionIssue>();

        if (controller.Programs.Count == 0)
        {
            issues.Add(
                new(
                    "trafficLightProgramsMissing",
                    source));

            return Empty(
                issues);
        }

        var programDurations =
            controller
                .Programs
                .Select(
                    program =>
                        program.Phases.Sum(
                            phase =>
                                phase.Duration))
                .ToArray();

        if (programDurations.Any(
                duration =>
                    !double.IsFinite(duration) ||
                    duration <= 0))
        {
            issues.Add(
                new(
                    "trafficLightProgramDurationInvalid",
                    source,
                    "Every OMSI traffic-light program must contain a positive finite phase duration."));

            return Empty(
                issues);
        }

        var cycle =
            controller.CycleDuration is
                { } configuredCycle &&
            double.IsFinite(configuredCycle) &&
            configuredCycle > 0
                ? configuredCycle
                : programDurations.Max();

        if (programDurations.Any(
                duration =>
                    duration >
                    cycle +
                    0.000001))
        {
            issues.Add(
                new(
                    "trafficLightProgramExceedsCycle",
                    source,
                    $"At least one program exceeds the controller cycle of {cycle:0.######} seconds."));

            return Empty(
                issues);
        }

        if (!TryToMicroseconds(
                cycle,
                out var cycleMicroseconds))
        {
            issues.Add(
                new(
                    "trafficLightTimingPrecisionUnsupported",
                    source,
                    $"Cycle duration {cycle:R} cannot be represented exactly at microsecond precision."));

            return Empty(
                issues);
        }

        var boundaries =
            new SortedSet<long>
            {
                0,
                cycleMicroseconds
            };

        foreach (var program in controller.Programs)
        {
            long elapsed =
                0;

            foreach (var phase in program.Phases)
            {
                if (!TryToMicroseconds(
                        phase.Duration,
                        out var phaseMicroseconds))
                {
                    issues.Add(
                        new(
                            "trafficLightTimingPrecisionUnsupported",
                            source,
                            $"Program '{program.Name}' phase duration {phase.Duration:R} cannot be represented exactly at microsecond precision."));

                    return Empty(
                        issues);
                }

                elapsed +=
                    phaseMicroseconds;

                if (elapsed < cycleMicroseconds)
                {
                    boundaries.Add(
                        elapsed);
                }
            }
        }

        var orderedBoundaries =
            boundaries.ToArray();

        var intervals =
            new List<long>(
                Math.Max(
                    1,
                    orderedBoundaries.Length -
                    1));

        for (var index = 0;
             index < orderedBoundaries.Length - 1;
             index++)
        {
            var duration =
                orderedBoundaries[index + 1] -
                orderedBoundaries[index];

            if (duration > 0)
            {
                intervals.Add(
                    duration);
            }
        }

        if (intervals.Count == 0)
        {
            issues.Add(
                new(
                    "trafficLightTimelineEmpty",
                    source));

            return Empty(
                issues);
        }

        var quantum =
            intervals.Aggregate(
                GreatestCommonDivisor);

        if (quantum <= 0)
        {
            issues.Add(
                new(
                    "trafficLightTickIntervalInvalid",
                    source));

            return Empty(
                issues);
        }

        var ticks =
            new List<ProtonBusTrafficLightTick>(
                intervals.Count);

        for (var intervalIndex = 0;
             intervalIndex < intervals.Count;
             intervalIndex++)
        {
            var start =
                orderedBoundaries[
                    intervalIndex];

            var duration =
                intervals[
                    intervalIndex];

            var repeatLong =
                duration /
                quantum;

            if (repeatLong <= 0 ||
                repeatLong > int.MaxValue)
            {
                issues.Add(
                    new(
                        "trafficLightRepeatOverflow",
                        source,
                        $"Interval repeat {repeatLong} cannot be represented by Proton Bus."));

                return Empty(
                    issues);
            }

            var states =
                new Dictionary<
                    int,
                    ProtonBusTrafficLightPathState>();

            for (var programIndex = 0;
                 programIndex < controller.Programs.Count;
                 programIndex++)
            {
                var program =
                    controller.Programs[
                        programIndex];

                var signalCode =
                    GetSignalCodeAt(
                        program,
                        start,
                        cycleMicroseconds);

                var state =
                    MapSignalCode(
                        signalCode);

                states[
                    programIndex +
                    1] =
                    state with
                    {
                        Trigger =
                            state.Red ||
                            state.Yellow
                    };
            }

            ticks.Add(
                new(
                    checked(
                        (int)repeatLong),
                    states));
        }

        var prefix =
            $"tl_t{tile.X}_{tile.Y}_o{placedObject.ObjectId}_c{controllerIndex}";

        var definition =
            new ProtonBusTrafficLightDefinition(
                Prefix:
                    prefix,
                PathCount:
                    controller.Programs.Count,
                TickInterval:
                    quantum /
                    (double)
                        MicrosecondsPerSecond,
                TriggerRadius:
                    options.TriggerRadius,
                UseRealLights:
                    false,
                Ticks:
                    ticks,
                RandomTimestampAtStart:
                    options.RandomTimestampAtStart,
                FirstTickToRun:
                    1);

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

        var markers =
            new List<ProtonBusExportMesh>();

        for (var programIndex = 0;
             programIndex < controller.Programs.Count;
             programIndex++)
        {
            var matchingPaths =
                asset.ResolvedPaths
                    .Where(
                        path =>
                            path.TrafficLightIndex ==
                            programIndex)
                    .ToArray();

            if (matchingPaths.Length == 0)
            {
                continue;
            }

            if (matchingPaths.Length > 1)
            {
                issues.Add(
                    new(
                        "trafficLightMultipleTriggerPaths",
                        source,
                        $"OMSI signal index {programIndex} controls {matchingPaths.Length} paths; Proton Bus currently has one trigger position per machine path."));
                continue;
            }

            var position =
                GetSceneryPathStart(
                    matchingPaths[0],
                    objectTransform);

            markers.Add(
                ProtonBusMarkerMeshBuilder
                    .Create(
                        definition
                            .GetTriggerObjectName(
                                programIndex +
                                1),
                        position,
                        options.MarkerHalfSize));
        }

        return new(
            [
                definition
            ],
            new(
                markers.ToArray()),
            issues);
    }

    private static int GetSignalCodeAt(
        OmsiTrafficLightProgram program,
        long timeMicroseconds,
        long cycleMicroseconds)
    {
        long elapsed =
            0;

        foreach (var phase in program.Phases)
        {
            if (!TryToMicroseconds(
                    phase.Duration,
                    out var duration))
            {
                continue;
            }

            var end =
                elapsed +
                duration;

            if (timeMicroseconds < end)
            {
                return phase.SignalCode;
            }

            elapsed =
                end;
        }

        if (program.Phases.Count == 0)
        {
            return -1;
        }

        // OMSI programs commonly define fewer explicit seconds than the
        // traffic_lights_group cycle. The final signal state remains active
        // through the remaining group cycle.
        if (timeMicroseconds < cycleMicroseconds)
        {
            return program
                .Phases[^1]
                .SignalCode;
        }

        return program
            .Phases[^1]
            .SignalCode;
    }

    private static ProtonBusTrafficLightPathState
        MapSignalCode(
            int signalCode) =>
        signalCode switch
        {
            >= 0 and <= 2 =>
                new(
                    Red:
                        true),
            >= 3 and <= 5 =>
                new(
                    Red:
                        true,
                    Yellow:
                        true),
            >= 6 and <= 8 =>
                new(
                    Green:
                        true),
            >= 9 and <= 11 =>
                new(
                    Yellow:
                        true),
            _ =>
                new()
        };

    private static Vector3 GetSceneryPathStart(
        OmsiSceneryPathDefinition path,
        Matrix4x4 objectTransform)
    {
        var local =
            new Vector3(
                (float)path.X,
                (float)path.Z,
                (float)path.Y);

        return Vector3.Transform(
            local,
            objectTransform);
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
                    OmsiTileGrid.GetOriginX(
                        tile.X) +
                    placedObject.X),
                (float)(
                    placedObject.Z +
                    terrainOffset),
                (float)(
                    OmsiTileGrid.GetOriginZ(
                        tile.Y) +
                    placedObject.Y));

    private static bool TryToMicroseconds(
        double seconds,
        out long microseconds)
    {
        microseconds =
            0;

        if (!double.IsFinite(
                seconds) ||
            seconds < 0)
        {
            return false;
        }

        var scaled =
            seconds *
            MicrosecondsPerSecond;

        if (scaled > long.MaxValue)
        {
            return false;
        }

        var rounded =
            Math.Round(
                scaled,
                MidpointRounding.AwayFromZero);

        if (Math.Abs(
                scaled -
                rounded) >
            0.000001)
        {
            return false;
        }

        microseconds =
            checked(
                (long)rounded);

        return true;
    }

    private static long GreatestCommonDivisor(
        long left,
        long right)
    {
        left =
            Math.Abs(
                left);

        right =
            Math.Abs(
                right);

        while (right != 0)
        {
            var remainder =
                left %
                right;

            left =
                right;

            right =
                remainder;
        }

        return left;
    }

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
            path
                .Trim()
                .Replace(
                    '\\',
                    '/');

        foreach (var pair in values)
        {
            if (string.Equals(
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

    private static void ValidateOptions(
        ProtonBusOmsiTrafficLightConversionOptions options)
    {
        if (!double.IsFinite(
                options.TriggerRadius) ||
            options.TriggerRadius <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Trigger radius must be finite and greater than zero.");
        }

        if (!double.IsFinite(
                options.MarkerHalfSize) ||
            options.MarkerHalfSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Marker size must be finite and greater than zero.");
        }
    }

    private static ProtonBusOmsiTrafficLightConversionResult Empty(
        IReadOnlyList<ProtonBusOmsiTrafficLightConversionIssue> issues) =>
        new(
            Array.Empty<ProtonBusTrafficLightDefinition>(),
            new(
                Array.Empty<ProtonBusExportMesh>()),
            issues);

    private static float DegreesToRadians(
        double value) =>
        (float)(
            value *
            Math.PI /
            180.0);
}
