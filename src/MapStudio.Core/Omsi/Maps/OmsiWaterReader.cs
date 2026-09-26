using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiWaterReader
{
    public async Task<OmsiWaterGrid> ReadAsync(
        string waterPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            waterPath);

        var bytes =
            await File.ReadAllBytesAsync(
                waterPath,
                cancellationToken);

        return Read(
            bytes);
    }

    public static OmsiWaterGrid Read(
        ReadOnlySpan<byte> bytes)
    {
        const int expectedLength =
            sizeof(int) +
            OmsiWaterGrid.HeightCount *
            sizeof(float);

        if (
            bytes.Length !=
                expectedLength)
        {
            throw new InvalidDataException(
                "Water byte length must be exactly 20 bytes.");
        }

        var cellCount =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    bytes[..4]);

        if (
            cellCount !=
                OmsiWaterGrid.CellCount)
        {
            throw new InvalidDataException(
                "Water grid cell count must be 1.");
        }

        var heights =
            new float[
                OmsiWaterGrid.HeightCount];

        for (
            var index = 0;
            index <
                heights.Length;
            index++)
        {
            var offset =
                sizeof(int) +
                index *
                sizeof(float);

            var bits =
                BinaryPrimitives
                    .ReadInt32LittleEndian(
                        bytes.Slice(
                            offset,
                            sizeof(float)));

            var height =
                BitConverter
                    .Int32BitsToSingle(
                        bits);

            if (!float.IsFinite(
                    height))
            {
                throw new InvalidDataException(
                    "Water contains a non-finite height.");
            }

            heights[index] =
                height;
        }

        return new OmsiWaterGrid(
            heights);
    }
}
