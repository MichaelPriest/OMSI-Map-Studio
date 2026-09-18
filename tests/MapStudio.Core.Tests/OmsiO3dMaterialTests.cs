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
