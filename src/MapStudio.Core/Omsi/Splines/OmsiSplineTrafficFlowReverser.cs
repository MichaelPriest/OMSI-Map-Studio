using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Splines;

public sealed record OmsiSplineTrafficFlowReverseResult(
    byte[] Bytes,
    int ReversedPathCount);

public static class OmsiSplineTrafficFlowReverser
{
    public static OmsiSplineTrafficFlowReverseResult
        ReverseVehiclePaths(
            OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(
            document);

        var reader =
            new OmsiSplineDefinitionReader();

        var patcher =
            new OmsiSplinePathPatcher();

        var definition =
            reader.Read(
                document);

        var current =
            document;

        var bytes =
            document.ToBytes();

        var changed =
            0;

        for (
            var index = 0;
            index <
                definition.Paths.Count;
            index++)
        {
            var path =
                definition.Paths[
                    index];

            if (
                path.Type !=
                    0 ||
                path.Direction ==
                    2)
            {
                continue;
            }

            if (
                path.Direction is not
                    (0 or 1))
            {
                continue;
            }

            var reversed =
                path with
                {
                    Direction =
                        path.Direction ==
                            0
                            ? 1
                            : 0
                };

            bytes =
                patcher.Patch(
                    current,
                    index,
                    reversed);

            current =
                OmsiConfigParser
                    .ParseBytes(
                        bytes);

            changed++;
        }

        return new OmsiSplineTrafficFlowReverseResult(
            bytes,
            changed);
    }
}
