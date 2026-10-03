namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTerrainBorderStitchResult(
    OmsiTerrainGrid Terrain,
    int ChangedSamples);

public static class OmsiTerrainBorderStitcher
{
    private enum Edge
    {
        Left,
        Right,
        Top,
        Bottom
    }

    private enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public static OmsiTerrainBorderStitchResult
        StitchToNeighbors(
            OmsiTerrainGrid target,
            OmsiTerrainGrid? negativeX,
            OmsiTerrainGrid? positiveX,
            OmsiTerrainGrid? negativeY,
            OmsiTerrainGrid? positiveY,
            OmsiTerrainGrid? negativeXNegativeY = null,
            OmsiTerrainGrid? positiveXNegativeY = null,
            OmsiTerrainGrid? negativeXPositiveY = null,
            OmsiTerrainGrid? positiveXPositiveY = null)
    {
        Validate(
            target);

        var heights =
            target.Heights
                .ToArray();

        var changed =
            0;

        if (negativeX is not null)
        {
            changed +=
                CopyEdge(
                    heights,
                    target.CellCount,
                    Edge.Left,
                    negativeX,
                    Edge.Right);
        }

        if (positiveX is not null)
        {
            changed +=
                CopyEdge(
                    heights,
                    target.CellCount,
                    Edge.Right,
                    positiveX,
                    Edge.Left);
        }

        if (negativeY is not null)
        {
            changed +=
                CopyEdge(
                    heights,
                    target.CellCount,
                    Edge.Top,
                    negativeY,
                    Edge.Bottom);
        }

        if (positiveY is not null)
        {
            changed +=
                CopyEdge(
                    heights,
                    target.CellCount,
                    Edge.Bottom,
                    positiveY,
                    Edge.Top);
        }

        if (
            negativeX is null &&
            negativeY is null &&
            negativeXNegativeY is not null)
        {
            changed +=
                CopyCorner(
                    heights,
                    target.CellCount,
                    Corner.TopLeft,
                    negativeXNegativeY,
                    Corner.BottomRight);
        }

        if (
            positiveX is null &&
            negativeY is null &&
            positiveXNegativeY is not null)
        {
            changed +=
                CopyCorner(
                    heights,
                    target.CellCount,
                    Corner.TopRight,
                    positiveXNegativeY,
                    Corner.BottomLeft);
        }

        if (
            negativeX is null &&
            positiveY is null &&
            negativeXPositiveY is not null)
        {
            changed +=
                CopyCorner(
                    heights,
                    target.CellCount,
                    Corner.BottomLeft,
                    negativeXPositiveY,
                    Corner.TopRight);
        }

        if (
            positiveX is null &&
            positiveY is null &&
            positiveXPositiveY is not null)
        {
            changed +=
                CopyCorner(
                    heights,
                    target.CellCount,
                    Corner.BottomRight,
                    positiveXPositiveY,
                    Corner.TopLeft);
        }

        return new OmsiTerrainBorderStitchResult(
            new OmsiTerrainGrid(
                target.CellCount,
                heights),
            changed);
    }

    private static int CopyEdge(
        float[] targetHeights,
        int targetCellCount,
        Edge targetEdge,
        OmsiTerrainGrid source,
        Edge sourceEdge)
    {
        Validate(
            source);

        var targetSampleCount =
            targetCellCount +
            1;

        var sourceSampleCount =
            source.CellCount +
            1;

        var changed =
            0;

        for (
            var index = 0;
            index < targetSampleCount;
            index++)
        {
            var sourcePosition =
                (double)index /
                (
                    targetSampleCount -
                    1
                ) *
                (
                    sourceSampleCount -
                    1
                );

            var sourceIndex0 =
                Math.Clamp(
                    (int)Math.Floor(
                        sourcePosition),
                    0,
                    sourceSampleCount -
                        1);

            var sourceIndex1 =
                Math.Min(
                    sourceSampleCount -
                        1,
                    sourceIndex0 +
                        1);

            var fraction =
                sourcePosition -
                sourceIndex0;

            var sourceValue0 =
                source.Heights[
                    GetIndex(
                        source.CellCount,
                        sourceEdge,
                        sourceIndex0)];

            var sourceValue1 =
                source.Heights[
                    GetIndex(
                        source.CellCount,
                        sourceEdge,
                        sourceIndex1)];

            var sampled =
                (float)(
                    sourceValue0 +
                    (
                        sourceValue1 -
                        sourceValue0
                    ) *
                    fraction);

            var targetIndex =
                GetIndex(
                    targetCellCount,
                    targetEdge,
                    index);

            if (
                Math.Abs(
                    targetHeights[
                        targetIndex] -
                    sampled) <=
                0.000001f)
            {
                continue;
            }

            targetHeights[
                targetIndex] =
                sampled;

            changed++;
        }

        return changed;
    }

    private static int CopyCorner(
        float[] targetHeights,
        int targetCellCount,
        Corner targetCorner,
        OmsiTerrainGrid source,
        Corner sourceCorner)
    {
        Validate(
            source);

        var targetIndex =
            GetCornerIndex(
                targetCellCount,
                targetCorner);

        var sourceValue =
            source.Heights[
                GetCornerIndex(
                    source.CellCount,
                    sourceCorner)];

        if (
            Math.Abs(
                targetHeights[
                    targetIndex] -
                sourceValue) <=
            0.000001f)
        {
            return 0;
        }

        targetHeights[
            targetIndex] =
            sourceValue;

        return 1;
    }

    private static int GetIndex(
        int cellCount,
        Edge edge,
        int edgeIndex)
    {
        var sampleCount =
            cellCount +
            1;

        return edge switch
        {
            Edge.Left =>
                edgeIndex *
                sampleCount,
            Edge.Right =>
                edgeIndex *
                    sampleCount +
                cellCount,
            Edge.Top =>
                edgeIndex,
            Edge.Bottom =>
                cellCount *
                    sampleCount +
                edgeIndex,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(edge))
        };
    }

    private static int GetCornerIndex(
        int cellCount,
        Corner corner)
    {
        var sampleCount =
            cellCount +
            1;

        return corner switch
        {
            Corner.TopLeft =>
                0,
            Corner.TopRight =>
                cellCount,
            Corner.BottomLeft =>
                cellCount *
                sampleCount,
            Corner.BottomRight =>
                sampleCount *
                    sampleCount -
                1,
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(corner))
        };
    }

    private static void Validate(
        OmsiTerrainGrid terrain)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);

        if (
            terrain.CellCount <=
                0 ||
            terrain.Heights.Count !=
                checked(
                    (
                        terrain.CellCount +
                        1
                    ) *
                    (
                        terrain.CellCount +
                        1
                    )))
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        if (
            terrain.Heights.Any(
                height =>
                    !float.IsFinite(
                        height)))
        {
            throw new InvalidDataException(
                "invalidTerrainHeight");
        }
    }
}
