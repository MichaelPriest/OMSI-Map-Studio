using System.Text;
using MapStudio.Core.Omsi.Models;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiO3dMaterialTests
{
    [Fact]
    public void GeometryReader_PreservesTriangleMaterialAndEmbeddedMaterial()
    {
        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-o3d-material-{Guid.NewGuid():N}.o3d");

        try
        {
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x84);
                writer.Write((byte)0x19);
                writer.Write((byte)0x07);
                writer.Write((byte)0x01);
                writer.Write(uint.MaxValue);

                writer.Write((byte)0x17);
                writer.Write((uint)3);

                WriteVertex(writer, 0, 0, 0);
                WriteVertex(writer, 1, 0, 0);
                WriteVertex(writer, 0, 1, 0);

                writer.Write((byte)0x49);
                writer.Write((uint)1);
                writer.Write((uint)0);
                writer.Write((uint)1);
                writer.Write((uint)2);
                writer.Write((ushort)0);

                writer.Write((byte)0x26);
                writer.Write((ushort)1);

                writer.Write(0.2f);
                writer.Write(0.4f);
                writer.Write(0.6f);
                writer.Write(0.8f);

                writer.Write(0.1f);
                writer.Write(0.2f);
                writer.Write(0.3f);

                writer.Write(0.01f);
                writer.Write(0.02f);
                writer.Write(0.03f);

                writer.Write(32f);

                var textureBytes =
                    Encoding.GetEncoding(1252)
                        .GetBytes("building.dds");

                writer.Write(
                    checked((byte)textureBytes.Length));

                writer.Write(textureBytes);
            }

            var geometry =
                new OmsiO3dGeometryReader()
                    .Read(path);

            Assert.True(geometry.IsLoaded);
            Assert.Equal(
                new ushort[] { 0 },
                geometry.TriangleMaterialIndices);

            var material =
                Assert.Single(geometry.Materials);

            Assert.Equal(0.2f, material.DiffuseR);
            Assert.Equal(0.4f, material.DiffuseG);
            Assert.Equal(0.6f, material.DiffuseB);
            Assert.Equal(0.8f, material.DiffuseA);
            Assert.Equal(32f, material.SpecularPower);
            Assert.Equal(
                "building.dds",
                material.TextureName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GeometryReader_DecodesProtectedOfficialStyleVertices()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-o3d-protected-{Guid.NewGuid():N}.o3d");

        try
        {
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x84);
                writer.Write((byte)0x19);
                writer.Write((byte)0x07);

                // Extended option bit 1 is the alternative protection seed.
                writer.Write((byte)0x02);
                writer.Write((uint)0);

                writer.Write((byte)0x17);
                writer.Write((uint)3);

                WriteProtectedVertex(
                    writer,
                    2.5f,
                    1.25f,
                    3.75f,
                    0.2f,
                    -0.4f,
                    0.6f,
                    0.1f,
                    0.2576f);

                WriteProtectedVertex(
                    writer,
                    4.25f,
                    2.5f,
                    1.75f,
                    0f,
                    0f,
                    1f,
                    0f,
                    0f);

                WriteProtectedVertex(
                    writer,
                    1.5f,
                    4.25f,
                    2.75f,
                    0f,
                    0f,
                    1f,
                    0f,
                    0f);

                writer.Write((byte)0x49);
                writer.Write((uint)1);
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)2);
                writer.Write((ushort)0);

                writer.Write((byte)0x26);
                writer.Write((ushort)0);
            }

            var geometry =
                new OmsiO3dGeometryReader()
                    .Read(path);

            Assert.True(
                geometry.IsLoaded,
                geometry.ErrorCode);

            Assert.Equal(
                1.25f,
                geometry.Positions[0],
                4);
            Assert.Equal(
                3.75f,
                geometry.Positions[1],
                4);
            Assert.Equal(
                2.5f,
                geometry.Positions[2],
                4);

            Assert.Equal(
                -0.2f,
                geometry.Normals[0],
                4);
            Assert.Equal(
                0.6f,
                geometry.Normals[1],
                4);
            Assert.Equal(
                0.4f,
                geometry.Normals[2],
                4);

            Assert.Equal(
                0.1f,
                geometry.Uvs[0],
                4);
            Assert.Equal(
                0.8f,
                geometry.Uvs[1],
                4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GeometryReader_AppliesInverseO3dTransform()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-o3d-transform-{Guid.NewGuid():N}.o3d");

        try
        {
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x84);
                writer.Write((byte)0x19);
                writer.Write((byte)0x03);

                writer.Write((byte)0x17);
                writer.Write((ushort)3);
                WriteVertex(writer, 11, 0, 0);
                WriteVertex(writer, 12, 0, 0);
                WriteVertex(writer, 11, 1, 0);

                writer.Write((byte)0x49);
                writer.Write((ushort)1);
                writer.Write((ushort)0);
                writer.Write((ushort)1);
                writer.Write((ushort)2);
                writer.Write((ushort)0);

                writer.Write((byte)0x26);
                writer.Write((ushort)0);

                writer.Write((byte)0x79);
                writer.Write(1f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1f);
                writer.Write(0f);
                writer.Write(10f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1f);
            }

            var geometry =
                new OmsiO3dGeometryReader()
                    .Read(path);

            Assert.True(
                geometry.IsLoaded,
                geometry.ErrorCode);

            Assert.Equal(
                1f,
                geometry.Positions[0],
                4);

            Assert.Equal(
                2f,
                geometry.Positions[3],
                4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GeometryReader_LongHeaderBoneSection_UsesLongBoneCount()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-o3d-bone-{Guid.NewGuid():N}.o3d");

        try
        {
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x84);
                writer.Write((byte)0x19);
                writer.Write((byte)0x07);
                writer.Write((byte)0x01);
                writer.Write(uint.MaxValue);

                writer.Write((byte)0x17);
                writer.Write((uint)3);
                WriteVertex(writer, 0, 0, 0);
                WriteVertex(writer, 1, 0, 0);
                WriteVertex(writer, 0, 1, 0);

                writer.Write((byte)0x49);
                writer.Write((uint)1);
                writer.Write((uint)0);
                writer.Write((uint)1);
                writer.Write((uint)2);
                writer.Write((ushort)0);

                writer.Write((byte)0x26);
                writer.Write((ushort)0);

                writer.Write((byte)0x54);
                writer.Write((uint)1);

                var boneName =
                    Encoding.ASCII
                        .GetBytes("root");

                writer.Write(
                    checked((byte)boneName.Length));
                writer.Write(boneName);

                writer.Write((ushort)1);
                writer.Write((uint)0);
                writer.Write(1f);
            }

            var geometry =
                new OmsiO3dGeometryReader()
                    .Read(path);

            Assert.True(
                geometry.IsLoaded,
                geometry.ErrorCode);
            Assert.Equal(
                3,
                geometry.Positions.Length /
                3);
            Assert.Equal(
                3,
                geometry.Indices.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void StructureReader_LongHeaderBoneSection_UsesLongBoneCount()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"mapstudio-o3d-structure-bone-{Guid.NewGuid():N}.o3d");

        try
        {
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)0x84);
                writer.Write((byte)0x19);
                writer.Write((byte)0x07);
                writer.Write((byte)0x01);
                writer.Write(uint.MaxValue);

                writer.Write((byte)0x17);
                writer.Write((uint)3);
                WriteVertex(writer, 0, 0, 0);
                WriteVertex(writer, 1, 0, 0);
                WriteVertex(writer, 0, 1, 0);

                writer.Write((byte)0x49);
                writer.Write((uint)1);
                writer.Write((uint)0);
                writer.Write((uint)1);
                writer.Write((uint)2);
                writer.Write((ushort)0);

                writer.Write((byte)0x26);
                writer.Write((ushort)0);

                writer.Write((byte)0x54);
                writer.Write((uint)1);

                var boneName =
                    Encoding.ASCII
                        .GetBytes("root");

                writer.Write(
                    checked((byte)boneName.Length));
                writer.Write(boneName);

                writer.Write((ushort)1);
                writer.Write((uint)0);
                writer.Write(1f);
            }

            var summary =
                new OmsiO3dStructureReader()
                    .Read(path);

            Assert.True(
                summary.IsParsed,
                summary.ErrorCode);
            Assert.Equal(
                (uint)1,
                summary.BoneCount);
            Assert.Equal(
                (uint)3,
                summary.VertexCount);
            Assert.Equal(
                (uint)1,
                summary.TriangleCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteProtectedVertex(
        BinaryWriter writer,
        float x,
        float y,
        float z,
        float normalX,
        float normalY,
        float normalZ,
        float u,
        float v)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);

        writer.Write(normalX);
        writer.Write(normalY);
        writer.Write(normalZ);

        writer.Write(u);
        writer.Write(v);
    }

    private static void WriteVertex(
        BinaryWriter writer,
        float x,
        float y,
        float z)
    {
        writer.Write(x);
        writer.Write(y);
        writer.Write(z);

        writer.Write(0f);
        writer.Write(0f);
        writer.Write(1f);

        writer.Write(0f);
        writer.Write(0f);
    }
}
