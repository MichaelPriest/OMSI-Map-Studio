using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using MapStudio.Core.ProtonBus;
using Xunit;

namespace MapStudio.ProtonBus.Tests;

public sealed class ProtonBus3dsWriterTests
{
    [Fact]
    public void WriterCreatesValidMainChunkLength()
    {
        var bytes =
            ProtonBus3dsWriter.Write(
                CreateScene());

        Assert.True(
            bytes.Length >
            6);

        Assert.Equal(
            0x4D4D,
            BinaryPrimitives
                .ReadUInt16LittleEndian(
                    bytes.AsSpan(
                        0,
                        2)));

        Assert.Equal(
            checked(
                (uint)
                    bytes.Length),
            BinaryPrimitives
                .ReadUInt32LittleEndian(
                    bytes.AsSpan(
                        2,
                        4)));
    }

    [Fact]
    public void WriterPreservesLongProtonObjectNames()
    {
        const string objectName =
            "road_gencol_transparent_long_name_001";

        var scene =
            new ProtonBusExportScene(
                [
                    CreateMesh(
                        objectName)
                ]);

        var bytes =
            ProtonBus3dsWriter.Write(
                scene);

        Assert.True(
            ContainsAsciiCString(
                bytes,
                objectName));
    }

    [Fact]
    public void WriterStoresMaterialAndPngTextureNames()
    {
        var bytes =
            ProtonBus3dsWriter.Write(
                CreateScene());

        Assert.True(
            ContainsAsciiCString(
                bytes,
                "asphalt"));

        Assert.True(
            ContainsAsciiCString(
                bytes,
                "asphalt.png"));
    }

    [Fact]
    public void WriterContainsStaticMeshChunkIds()
    {
        var bytes =
            ProtonBus3dsWriter.Write(
                CreateScene());

        Assert.True(
            ContainsChunkId(
                bytes,
                0x3D3D));

        Assert.True(
            ContainsChunkId(
                bytes,
                0x4000));

        Assert.True(
            ContainsChunkId(
                bytes,
                0x4100));

        Assert.True(
            ContainsChunkId(
                bytes,
                0x4110));

        Assert.True(
            ContainsChunkId(
                bytes,
                0x4120));

        Assert.True(
            ContainsChunkId(
                bytes,
                0x4130));

        Assert.True(
            ContainsChunkId(
                bytes,
                0x4140));
    }

    [Fact]
    public void WriterRejectsUnknownTriangleMaterial()
    {
        var mesh =
            CreateMesh(
                "broken") with
            {
                Triangles =
                [
                    new(
                        0,
                        1,
                        2,
                        "missing")
                ]
            };

        var scene =
            new ProtonBusExportScene(
                [
                    mesh
                ]);

        var error =
            Assert.Throws<
                ArgumentException>(
                    () =>
                        ProtonBus3dsWriter
                            .Write(
                                scene));

        Assert.Contains(
            "unknown material",
            error.Message,
            StringComparison
                .OrdinalIgnoreCase);
    }

    private static ProtonBusExportScene
        CreateScene() =>
        new(
            [
                CreateMesh(
                    "road_gencol_001")
            ]);

    private static ProtonBusExportMesh
        CreateMesh(
            string name) =>
        new(
            name,
            [
                new(
                    new Vector3(
                        0,
                        0,
                        0),
                    new Vector2(
                        0,
                        0)),
                new(
                    new Vector3(
                        1,
                        0,
                        0),
                    new Vector2(
                        1,
                        0)),
                new(
                    new Vector3(
                        0,
                        0,
                        1),
                    new Vector2(
                        0,
                        1))
            ],
            [
                new(
                    0,
                    1,
                    2,
                    "asphalt")
            ],
            [
                new(
                    "asphalt",
                    "asphalt.png")
            ]);

    private static bool ContainsAsciiCString(
        byte[] data,
        string value)
    {
        var needle =
            Encoding.ASCII.GetBytes(
                value +
                "\0");

        return data
            .AsSpan()
            .IndexOf(
                needle) >=
            0;
    }

    private static bool ContainsChunkId(
        byte[] data,
        ushort id)
    {
        Span<byte> needle =
            stackalloc byte[2];

        BinaryPrimitives
            .WriteUInt16LittleEndian(
                needle,
                id);

        return data
            .AsSpan()
            .IndexOf(
                needle) >=
            0;
    }
}
