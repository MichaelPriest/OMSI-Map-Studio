namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTerrainLevelResult(
    OmsiTerrainGrid Terrain,
    int ChangedSamples);

public static class OmsiTerrainLeveler
{
    private const double TileSize = 300.0;

    public static OmsiTerrainLevelResult
        LevelCircularBrush(
            OmsiTerrainGrid terrain,
            double localX,
            double localY,
            double targetHeight,
            double radius,
            double feather)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);

        if (
            !double.IsFinite(localX) ||
            !double.IsFinite(localY) ||
            !double.IsFinite(targetHeight) ||
            !double.IsFinite(radius) ||
            !double.IsFinite(feather) ||
            radius <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            throw new InvalidDataException(
                "invalidTerrainBrush");
        }

        var cellCount =
            terrain.CellCount;

        if (cellCount <= 0)
        {
            throw new InvalidDataException(
                "invalidTerrainCellCount");
        }

        var sampleCount =
            cellCount + 1;

        if (
            terrain.Heights.Count !=
                sampleCount *
                sampleCount)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var spacing =
            TileSize /
            cellCount;

        var innerRadius =
            radius *
            (1 - feather);

        var next =
            terrain.Heights
                .ToArray();

        var changed = 0;

        for (
            var row = 0;
            row < sampleCount;
            row++)
        {
            var sampleY =
                row * spacing;

            for (
                var column = 0;
                column < sampleCount;
                column++)
            {
                var sampleX =
                    column * spacing;

                var distance =
                    Math.Sqrt(
                        Math.Pow(
                            sampleX - localX,
                            2) +
                        Math.Pow(
                            sampleY - localY,
                            2));

                if (distance > radius)
                {
                    continue;
                }

                double weight;

                if (
                    feather <= 0 ||
                    distance <=
                        innerRadius)
                {
                    weight = 1;
                }
                else
                {
                    var featherWidth =
                        Math.Max(
                            0.000001,
                            radius -
                            innerRadius);

                    weight =
                        1 -
                        (
                            distance -
                            innerRadius
                        ) /
                        featherWidth;

                    weight =
                        Math.Clamp(
                            weight,
                            0,
                            1);
                }

                var index =
                    row *
                    sampleCount +
                    column;

                var current =
                    next[index];

                var updated =
                    current +
                    (
                        targetHeight -
                        current
                    ) *
                    weight;

                var asFloat =
                    (float)updated;

                if (
                    Math.Abs(
                        asFloat -
                        current) >
                    0.00001f)
                {
                    next[index] =
                        asFloat;
                    changed++;
                }
            }
        }

        return new OmsiTerrainLevelResult(
            new OmsiTerrainGrid(
                terrain.CellCount,
                next),
            changed);
    }

    public static OmsiTerrainLevelResult
        ApplyElevationGrid(
            OmsiTerrainGrid terrain,
            int rows,
            int columns,
            IReadOnlyList<double> elevations,
            double verticalOffset)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);
        ArgumentNullException.ThrowIfNull(
            elevations);

        if (
            rows < 2 ||
            columns < 2 ||
            rows > 257 ||
            columns > 257 ||
            elevations.Count !=
                rows * columns ||
            !double.IsFinite(
                verticalOffset) ||
            elevations.Any(
                elevation =>
                    !double.IsFinite(
                        elevation)))
        {
            throw new InvalidDataException(
                "invalidElevationGrid");
        }

        var terrainSampleCount =
            terrain.CellCount + 1;

        if (
            terrain.CellCount <= 0 ||
            terrain.Heights.Count !=
                terrainSampleCount *
                terrainSampleCount)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var next =
            new float[
                terrain.Heights.Count];

        var changed = 0;

        for (
            var row = 0;
            row < terrainSampleCount;
            row++)
        {
            var sourceY =
                terrainSampleCount == 1
                    ? 0
                    : (double)row /
                      (terrainSampleCount - 1) *
                      (rows - 1);

            var y0 =
                Math.Clamp(
                    (int)Math.Floor(
                        sourceY),
                    0,
                    rows - 1);

            var y1 =
                Math.Min(
                    rows - 1,
                    y0 + 1);

            var fy =
                sourceY - y0;

            for (
                var column = 0;
                column < terrainSampleCount;
                column++)
            {
                var sourceX =
                    terrainSampleCount == 1
                        ? 0
                        : (double)column /
                          (terrainSampleCount - 1) *
                          (columns - 1);

                var x0 =
                    Math.Clamp(
                        (int)Math.Floor(
                            sourceX),
                        0,
                        columns - 1);

                var x1 =
                    Math.Min(
                        columns - 1,
                        x0 + 1);

                var fx =
                    sourceX - x0;

                var topLeft =
                    elevations[
                        y0 *
                        columns +
                        x0];

                var topRight =
                    elevations[
                        y0 *
                        columns +
                        x1];

                var bottomLeft =
                    elevations[
                        y1 *
                        columns +
                        x0];

                var bottomRight =
                    elevations[
                        y1 *
                        columns +
                        x1];

                var top =
                    topLeft +
                    (
                        topRight -
                        topLeft
                    ) *
                    fx;

                var bottom =
                    bottomLeft +
                    (
                        bottomRight -
                        bottomLeft
                    ) *
                    fx;

                var sampled =
                    top +
                    (
                        bottom -
                        top
                    ) *
                    fy +
                    verticalOffset;

                var asFloat =
                    (float)sampled;

                var index =
                    row *
                    terrainSampleCount +
                    column;

                next[index] =
                    asFloat;

                if (
                    Math.Abs(
                        asFloat -
                        terrain.Heights[index]) >
                    0.00001f)
                {
                    changed++;
                }
            }
        }

        return new OmsiTerrainLevelResult(
            new OmsiTerrainGrid(
                terrain.CellCount,
                next),
            changed);
    }
}
