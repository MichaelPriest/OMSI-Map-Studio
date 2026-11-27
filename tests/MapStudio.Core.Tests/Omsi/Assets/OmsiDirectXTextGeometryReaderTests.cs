using MapStudio.Core.Omsi.Models;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class
    OmsiDirectXTextGeometryReaderTests
{
    [Fact]
    public void Read_TextMesh_ParsesGeometryUvAndMaterial()
    {
        var path =
            CreateTempFile(
                """
                xof 0303txt 0032
                Mesh LegacyMesh {
                  3;
                  0;0;0;,
                  1;0;0;,
                  0;1;0;;
                  1;
                  3;0,1,2;;

                  MeshTextureCoords {
                    3;
                    0;0;,
                    1;0;,
                    0;1;;
                  }

                  MeshMaterialList {
                    1;
                    1;
                    0;;
                    Material LegacyMaterial {
                      1;0.5;0.25;1;;
                      8;
                      0.1;0.2;0.3;;
                      0;0;0;;
                      TextureFilename {
                        "legacy.bmp";
                      }
                    }
                  }
                }
                """);

        try
        {
            var geometry =
                new OmsiDirectXTextGeometryReader()
                    .Read(path);

            Assert.True(
                geometry.IsLoaded,
                geometry.ErrorCode);

            Assert.Equal(
                9,
                geometry.Positions.Length);

            Assert.Equal(
                new uint[]
                {
                    0,
                    1,
                    2
                },
                geometry.Indices);

            Assert.Equal(
                6,
                geometry.Uvs.Length);

            Assert.Equal(
                1f,
                geometry.Positions[7],
                4);

            Assert.Equal(
                0f,
                geometry.Positions[8],
                4);

            var material =
                Assert.Single(
                    geometry.Materials);

            Assert.Equal(
                "legacy.bmp",
                material.TextureName);

            Assert.Equal(
                new ushort[] { 0 },
                geometry
                    .TriangleMaterialIndices);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_TextMesh_PreservesDirectXYUpAxes()
    {
        var path =
            CreateTempFile(
                """
                xof 0303txt 0032
                Mesh {
                  3;
                  0;0;0;,
                  1;0;0;,
                  0;0;2;;
                  1;
                  3;0,1,2;;
                }
                """);

        try
        {
            var geometry =
                new OmsiDirectXTextGeometryReader()
                    .Read(path);

            Assert.True(
                geometry.IsLoaded,
                geometry.ErrorCode);

            Assert.Equal(
                2f,
                geometry.Positions[8],
                4);

            Assert.Equal(
                0f,
                geometry.Positions[7],
                4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_Quad_FanTriangulates()
    {
        var path =
            CreateTempFile(
                """
                xof 0303txt 0032
                Mesh {
                  4;
                  0;0;0;,
                  1;0;0;,
                  1;1;0;,
                  0;1;0;;
                  1;
                  4;0,1,2,3;;
                }
                """);

        try
        {
            var geometry =
                new OmsiDirectXTextGeometryReader()
                    .Read(path);

            Assert.True(
                geometry.IsLoaded,
                geometry.ErrorCode);

            Assert.Equal(
                6,
                geometry.Indices.Length);

            Assert.Equal(
                2,
                geometry
                    .TriangleMaterialIndices
                    .Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_FrameTransform_AppliesTransform()
    {
        var path =
            CreateTempFile(
                """
                xof 0303txt 0032
                Frame Root {
                  FrameTransformMatrix {
                    1,0,0,0,
                    0,1,0,0,
                    0,0,1,0,
                    10,0,0,1;;
                  }
                  Mesh {
                    3;
                    0;0;0;,
                    1;0;0;,
                    0;1;0;;
                    1;
                    3;0,1,2;;
                  }
                }
                """);

        try
        {
            var geometry =
                new OmsiDirectXTextGeometryReader()
                    .Read(path);

            Assert.True(
                geometry.IsLoaded,
                geometry.ErrorCode);

            Assert.Equal(
                10f,
                geometry.Positions[0],
                4);

            Assert.Equal(
                11f,
                geometry.Positions[3],
                4);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("bin ")]
    [InlineData("tzip")]
    [InlineData("bzip")]
    public void Read_NonTextEncoding_IsRejected(
        string encoding)
    {
        var path =
            CreateTempFile(
                $"xof 0303{encoding}0032");

        try
        {
            var geometry =
                new OmsiDirectXTextGeometryReader()
                    .Read(path);

            Assert.False(
                geometry.IsLoaded);

            Assert.Equal(
                "legacyDirectXUnsupportedEncoding",
                geometry.ErrorCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTempFile(
        string content)
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                $"mapstudio-directx-{Guid.NewGuid():N}.x");

        File.WriteAllText(
            path,
            content);

        return path;
    }
}
