using System.Buffers.Binary;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTerrainReaderTests
{
    [Fact]
    public void Read_DecodesLittleEndianHeightGrid()
    {
        const int cellCount = 2;

        var heights = new float[]
        {
            0, 1, 2,
            3, 4, 5,
            6, 7, 8
        };

        var bytes =
            CreateTerrainBytes(
                cellCount,
                heights);

        var terrain =
            OmsiTerrainReader.Read(
                bytes);

        Assert.Equal(
            cellCount,
            terrain.CellCount);

        Assert.Equal(
            3,
            terrain.SampleCount);

        Assert.Equal(
            heights,
            terrain.Heights);
    }

    [Fact]
    public void Read_RejectsUnexpectedLength()
    {
        var bytes =
            CreateTerrainBytes(
                2,
                [
                    0, 1, 2,
                    3, 4, 5,
                    6, 7, 8
                ]);

        Array.Resize(
            ref bytes,
            bytes.Length - 4);

        Assert.Throws<
            InvalidDataException>(
            () =>
                OmsiTerrainReader.Read(
                    bytes));
    }

    [Fact]
    public void Read_RejectsNonFiniteHeight()
    {
        var bytes =
            CreateTerrainBytes(
                1,
                [
                    0,
                    float.NaN,
                    1,
                    2
                ]);

        Assert.Throws<
            InvalidDataException>(
            () =>
                OmsiTerrainReader.Read(
                    bytes));
    }

    [Fact]
    public void Read_RecognizesStandardOmsiGrid()
    {
        const int cellCount = 60;

        var heights =
            Enumerable.Repeat(
                    148f,
                    61 * 61)
                .ToArray();

        var bytes =
            CreateTerrainBytes(
                cellCount,
                heights);

        Assert.Equal(
            14_888,
            bytes.Length);

        var terrain =
            OmsiTerrainReader.Read(
                bytes);

        Assert.Equal(
            60,
            terrain.CellCount);

        Assert.Equal(
            3_721,
            terrain.Heights.Count);

        Assert.All(
            terrain.Heights,
            height =>
                Assert.Equal(
                    148f,
                    height));
    }

    private static byte[] CreateTerrainBytes(
        int cellCount,
        IReadOnlyList<float> heights)
    {
        var bytes =
            new byte[
                4 +
                heights.Count *
                sizeof(float)];

        BinaryPrimitives
            .WriteInt32LittleEndian(
                bytes.AsSpan(0, 4),
                cellCount);

        for (
            var index = 0;
            index < heights.Count;
            index++)
        {
            BinaryPrimitives
                .WriteInt32LittleEndian(
                    bytes.AsSpan(
                        4 +
                        index *
                        sizeof(float),
                        sizeof(float)),
                    BitConverter
                        .SingleToInt32Bits(
                            heights[index]));
        }

        return bytes;
    }
}
