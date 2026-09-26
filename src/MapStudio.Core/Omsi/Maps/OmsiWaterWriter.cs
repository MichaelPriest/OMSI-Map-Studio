using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Maps;

public static class OmsiWaterWriter
{
    public static byte[] Write(
        OmsiWaterGrid water)
    {
        ArgumentNullException.ThrowIfNull(
            water);

        if (
            water.Heights.Count !=
                OmsiWaterGrid.HeightCount)
        {
            throw new InvalidDataException(
                "invalidWaterHeightCount");
        }

        var bytes =
            new byte[
                sizeof(int) +
                OmsiWaterGrid.HeightCount *
                sizeof(float)];

        BinaryPrimitives
            .WriteInt32LittleEndian(
                bytes.AsSpan(
                    0,
                    sizeof(int)),
                OmsiWaterGrid.CellCount);

        for (
            var index = 0;
            index <
                OmsiWaterGrid.HeightCount;
            index++)
        {
            var height =
                water.Heights[index];

            if (!float.IsFinite(
                    height))
            {
                throw new InvalidDataException(
                    "invalidWaterHeight");
            }

            BinaryPrimitives
                .WriteInt32LittleEndian(
                    bytes.AsSpan(
                        sizeof(int) +
                        index *
                        sizeof(float),
                        sizeof(float)),
                    BitConverter
                        .SingleToInt32Bits(
                            height));
        }

        return bytes;
    }
}
