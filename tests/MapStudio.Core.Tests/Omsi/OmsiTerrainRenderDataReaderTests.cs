using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTerrainRenderDataReaderTests
{
    [Fact]
    public void Read_RecognizesTerrainRenderDataSections()
    {
        var path =
            CreatePath();

        try
        {
            WriteRenderData(
                path,
                key: 0,
                truncate: false);

            var summary =
                new OmsiTerrainRenderDataReader()
                    .Read(path);

            Assert.True(summary.Exists);
            Assert.True(summary.IsValid);
            Assert.Equal(
                (uint)4,
                summary.VertexCount);
            Assert.Equal(
                (uint)2,
                summary.TriangleCount);
            Assert.Equal(
                (ushort)1,
                summary.MaterialCount);
            Assert.True(
                summary.HasTransform);
            Assert.Null(
                summary.ErrorCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsUnsupportedKey()
    {
        var path =
            CreatePath();

        try
        {
            WriteRenderData(
                path,
                key: 123,
                truncate: false);

            var summary =
                new OmsiTerrainRenderDataReader()
                    .Read(path);

            Assert.True(summary.Exists);
            Assert.False(
                summary.IsValid);
            Assert.Equal(
                "unsupportedKey",
                summary.ErrorCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsTruncatedTriangleSection()
    {
        var path =
            CreatePath();

        try
        {
            WriteRenderData(
                path,
                key: 0,
                truncate: true);

            var summary =
                new OmsiTerrainRenderDataReader()
                    .Read(path);

            Assert.True(summary.Exists);
            Assert.False(
                summary.IsValid);
            Assert.Equal(
                "invalidTriangleSection",
                summary.ErrorCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ReturnsMissingForAbsentFile()
    {
        var path =
            CreatePath();

        var summary =
            new OmsiTerrainRenderDataReader()
                .Read(path);

        Assert.False(summary.Exists);
        Assert.False(summary.IsValid);
        Assert.Equal(
            0,
            summary.FileSize);
        Assert.Null(
            summary.ErrorCode);
    }

    private static string CreatePath() =>
        Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-terrain-rdy-{Guid.NewGuid():N}.rdy");

    private static void WriteRenderData(
        string path,
        uint key,
        bool truncate)
    {
        using var stream =
            File.Create(path);

        using var writer =
            new BinaryWriter(stream);

        writer.Write((byte)0x84);
        writer.Write((byte)0x19);
        writer.Write((byte)0x07);
        writer.Write((byte)0x01);
        writer.Write(key);

        writer.Write((byte)0x17);
        writer.Write((uint)4);
        writer.Write(
            new byte[4 * 32]);

        writer.Write((byte)0x49);
        writer.Write((uint)2);

        writer.Write((uint)0);
        writer.Write((uint)1);
        writer.Write((uint)2);
        writer.Write((ushort)0);

        if (truncate)
        {
            writer.Write((uint)1);
            return;
        }

        writer.Write((uint)0);
        writer.Write((uint)2);
        writer.Write((uint)3);
        writer.Write((ushort)0);

        writer.Write((byte)0x26);
        writer.Write((ushort)1);

        for (
            var index = 0;
            index < 11;
            index++)
        {
            writer.Write(0f);
        }

        writer.Write((byte)0);

        writer.Write((byte)0x79);
        for (
            var index = 0;
            index < 16;
            index++)
        {
            writer.Write(
                index is 0 or 5 or 10 or 15
                    ? 1f
                    : 0f);
        }
    }
}
