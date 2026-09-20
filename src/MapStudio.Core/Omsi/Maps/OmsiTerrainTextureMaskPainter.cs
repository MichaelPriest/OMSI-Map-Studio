namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTerrainTextureMaskPainter
{
    private const double TileSizeMeters =
        300.0;

    public static OmsiTerrainTexturePaintResult
        PaintCircularBrush(
            OmsiTerrainTextureMaskData source,
            double localX,
            double localY,
            double radiusMeters,
            byte targetAlpha,
            double feather)
    {
        ArgumentNullException.ThrowIfNull(
            source);

        if (
            source.Width <= 0 ||
            source.Height <= 0 ||
            source.AlphaPixels.Length !=
                checked(
                    source.Width *
                    source.Height))
        {
            throw new InvalidDataException(
                "terrainMaskInvalidData");
        }

        if (
            !double.IsFinite(localX) ||
            !double.IsFinite(localY) ||
            !double.IsFinite(radiusMeters) ||
            !double.IsFinite(feather) ||
            radiusMeters <= 0 ||
            feather < 0 ||
            feather > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radiusMeters));
        }

        var output =
            source.AlphaPixels
                .ToArray();

        var innerRadius =
            radiusMeters *
            (1.0 -
                feather);

        var changed =
            0;

        for (
            var row = 0;
            row < source.Height;
            row++)
        {
            var worldY =
                source.Height == 1
                    ? 0
                    : row /
                        (double)(
                            source.Height -
                            1) *
                        TileSizeMeters;

            for (
                var column = 0;
                column < source.Width;
                column++)
            {
                var worldX =
                    source.Width == 1
                        ? 0
                        : column /
                            (double)(
                                source.Width -
                                1) *
                            TileSizeMeters;

                var dx =
                    worldX -
                    localX;

                var dy =
                    worldY -
                    localY;

                var distance =
                    Math.Sqrt(
                        dx * dx +
                        dy * dy);

                if (
                    distance >
                    radiusMeters)
                {
                    continue;
                }

                double weight;

                if (
                    feather <= 0 ||
                    distance <=
                        innerRadius)
                {
                    weight = 1.0;
                }
                else
                {
                    var denominator =
                        Math.Max(
                            0.000001,
                            radiusMeters -
                            innerRadius);

                    weight =
                        Math.Clamp(
                            (
                                radiusMeters -
                                distance
                            ) /
                            denominator,
                            0,
                            1);
                }

                var index =
                    row *
                    source.Width +
                    column;

                var original =
                    output[index];

                var painted =
                    (byte)Math.Clamp(
                        Math.Round(
                            original +
                            (
                                targetAlpha -
                                original
                            ) *
                            weight),
                        0,
                        255);

                if (
                    painted ==
                    original)
                {
                    continue;
                }

                output[index] =
                    painted;

                changed++;
            }
        }

        return new OmsiTerrainTexturePaintResult(
            new OmsiTerrainTextureMaskData(
                source.Width,
                source.Height,
                output),
            changed);
    }
}
