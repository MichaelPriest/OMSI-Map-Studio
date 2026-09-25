using MapStudio.Renderer.Viewport;

namespace MapStudio.Renderer.Viewport;

public static class NativeVegetationPresetDistributor
{
    public static IReadOnlyList<
        NativeSceneryPlacementRequest>
        Distribute(
            IReadOnlyList<
                NativeSceneryPlacementRequest>
                placements,
            IReadOnlyList<string>
                sceneryObjectPaths)
    {
        ArgumentNullException.ThrowIfNull(
            placements);

        ArgumentNullException.ThrowIfNull(
            sceneryObjectPaths);

        var paths =
            sceneryObjectPaths
                .Where(
                    path =>
                        !string.IsNullOrWhiteSpace(
                            path))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    path =>
                        path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (
            placements.Count ==
                0 ||
            paths.Length ==
                0)
        {
            return placements.ToArray();
        }

        if (paths.Length == 1)
        {
            return placements
                .Select(
                    placement =>
                        placement with
                        {
                            SceneryObjectPath =
                                paths[0]
                        })
                .ToArray();
        }

        var result =
            new NativeSceneryPlacementRequest[
                placements.Count];

        var start =
            StableIndex(
                placements[0],
                paths.Length);

        var stride =
            FindCoprimeStride(
                paths.Length);

        for (
            var index = 0;
            index <
                placements.Count;
            index++)
        {
            var pathIndex =
                (
                    start +
                    index *
                    stride
                ) %
                paths.Length;

            result[index] =
                placements[index] with
                {
                    SceneryObjectPath =
                        paths[pathIndex]
                };
        }

        return result;
    }

    private static int StableIndex(
        NativeSceneryPlacementRequest
            placement,
        int count)
    {
        unchecked
        {
            var x =
                BitConverter.DoubleToInt64Bits(
                    placement
                        .WorldPoint.X);

            var z =
                BitConverter.DoubleToInt64Bits(
                    placement
                        .WorldPoint.Z);

            var hash =
                (ulong)x *
                    11400714819323198485UL ^
                (ulong)z *
                    14029467366897019727UL;

            return
                (int)(
                    hash %
                    (ulong)count);
        }
    }

    private static int FindCoprimeStride(
        int count)
    {
        for (
            var candidate =
                Math.Max(
                    1,
                    count -
                    1);
            candidate >=
                1;
            candidate--)
        {
            if (
                GreatestCommonDivisor(
                    candidate,
                    count) ==
                1)
            {
                return candidate;
            }
        }

        return 1;
    }

    private static int GreatestCommonDivisor(
        int left,
        int right)
    {
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

        return Math.Abs(
            left);
    }
}
