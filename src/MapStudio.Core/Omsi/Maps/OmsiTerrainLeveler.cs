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
}
