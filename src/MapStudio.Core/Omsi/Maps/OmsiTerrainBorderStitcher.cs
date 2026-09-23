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

    public static OmsiTerrainBorderStitchResult
        StitchToNeighbors(
            OmsiTerrainGrid target,
            OmsiTerrainGrid? negativeX,
            OmsiTerrainGrid? positiveX,
            OmsiTerrainGrid? negativeY,
            OmsiTerrainGrid? positiveY)
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
