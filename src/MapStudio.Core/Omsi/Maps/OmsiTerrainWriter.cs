using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiTerrainWriter
{
    public static byte[] Write(
        OmsiTerrainGrid terrain)
    {
        ArgumentNullException.ThrowIfNull(
            terrain);

        if (
            terrain.CellCount <= 0 ||
            terrain.CellCount > 512)
        {
            throw new InvalidDataException(
                "invalidTerrainCellCount");
        }

        var sampleCount =
            terrain.CellCount + 1;

        var expectedHeights =
            checked(
                sampleCount *
                sampleCount);

        if (
            terrain.Heights.Count !=
                expectedHeights)
        {
            throw new InvalidDataException(
                "invalidTerrainHeightCount");
        }

        var bytes =
            new byte[
                checked(
                    4 +
                    expectedHeights *
                    sizeof(float))];

        BinaryPrimitives
            .WriteInt32LittleEndian(
                bytes.AsSpan(0, 4),
                terrain.CellCount);

        for (
            var index = 0;
            index < expectedHeights;
            index++)
        {
            var height =
                terrain.Heights[index];

            if (!float.IsFinite(height))
            {
                throw new InvalidDataException(
                    "invalidTerrainHeight");
            }

            var bits =
                BitConverter
                    .SingleToInt32Bits(
                        height);

            BinaryPrimitives
                .WriteInt32LittleEndian(
                    bytes.AsSpan(
                        4 +
                        index *
                        sizeof(float),
                        sizeof(float)),
                    bits);
        }

        return bytes;
    }
}
