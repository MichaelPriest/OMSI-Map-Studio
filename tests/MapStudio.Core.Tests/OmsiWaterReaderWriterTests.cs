using System.Buffers.Binary;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiWaterReaderWriterTests
{
    [Fact]
    public void WriterProducesCanonicalTwentyByteWaterGrid()
    {
        var water =
            new OmsiWaterGrid(
                [
                    -1.0f,
                    0.5f,
                    2.25f,
                    3.75f
                ]);

        var bytes =
            OmsiWaterWriter
                .Write(
                    water);

        Assert.Equal(
            20,
            bytes.Length);

        Assert.Equal(
            1,
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    bytes.AsSpan(
                        0,
                        4)));
    }

    [Fact]
    public void ReaderWriterRoundTripPreservesFourCornerHeights()
    {
        var source =
            new OmsiWaterGrid(
                [
                    -2.5f,
                    -1.0f,
                    0.25f,
                    4.5f
                ]);

        var bytes =
            OmsiWaterWriter
                .Write(
                    source);

        var result =
            OmsiWaterReader
                .Read(
                    bytes);

        Assert.Equal(
            source.Heights,
            result.Heights);

        Assert.Equal(
            -2.5f,
            result.GetHeight(
                0,
                0));

        Assert.Equal(
            -1.0f,
            result.GetHeight(
                1,
                0));

        Assert.Equal(
            0.25f,
            result.GetHeight(
                0,
                1));

        Assert.Equal(
            4.5f,
            result.GetHeight(
                1,
                1));
    }

    [Fact]
    public void ReaderRejectsWrongCellCount()
    {
        var bytes =
            new byte[20];

        BinaryPrimitives
            .WriteInt32LittleEndian(
                bytes.AsSpan(
                    0,
                    4),
                2);

        Assert.Throws<
            InvalidDataException>(
                () =>
                    OmsiWaterReader
                        .Read(
                            bytes));
    }

    [Fact]
    public void ReaderRejectsWrongByteLength()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    OmsiWaterReader
                        .Read(
                            new byte[16]));
    }

    [Fact]
    public void WriterRejectsNonFiniteHeight()
    {
        var water =
            new OmsiWaterGrid(
                [
                    0,
                    0,
                    float.NaN,
                    0
                ]);

        Assert.Throws<
            InvalidDataException>(
                () =>
                    OmsiWaterWriter
                        .Write(
                            water));
    }
}
