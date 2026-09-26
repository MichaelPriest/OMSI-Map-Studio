using MapStudio.Core.Omsi.Models;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiO3dGeometryWriterTests
{
    [Fact]
    public async Task WriterRoundTripsEditableVersion3Geometry()
    {
        var path =
            Path.Combine(
                Path.GetTempPath(),
                "mapstudio-o3d-" +
                Guid.NewGuid()
                    .ToString("N") +
                ".o3d");

        try
        {
            var geometry =
                new OmsiO3dGeometry(
                    IsLoaded:
                        true,
                    ErrorCode:
                        null,
                    Positions:
                    [
                        -1, 0, 0,
                         1, 0, 0,
                         0, 2, 0
                    ],
                    Normals:
                    [
                        0, 0, 1,
                        0, 0, 1,
                        0, 0, 1
                    ],
                    Uvs:
                    [
                        0, 1,
                        1, 1,
                        0.5f, 0
                    ],
                    Indices:
                    [
                        0, 1, 2
                    ],
                    TriangleMaterialIndices:
                    [
                        0
                    ],
                    Materials:
                    [
                        new OmsiO3dMaterial(
                            1,
                            1,
                            1,
                            1,
                            0.1f,
                            0.1f,
                            0.1f,
                            0,
                            0,
                            0,
                            8,
                            "facade.png")
                    ]);

            await new OmsiO3dGeometryWriter()
                .WriteAsync(
                    path,
                    geometry);

            var parsed =
                new OmsiO3dGeometryReader()
                    .Read(
                        path);

            Assert.True(
                parsed.IsLoaded);

            Assert.Equal(
                geometry.Positions,
                parsed.Positions);

            Assert.Equal(
                geometry.Normals,
                parsed.Normals);

            Assert.Equal(
                geometry.Uvs,
                parsed.Uvs);

            Assert.Equal(
                geometry.Indices,
                parsed.Indices);

            Assert.Equal(
                geometry
                    .TriangleMaterialIndices,
                parsed
                    .TriangleMaterialIndices);

            var material =
                Assert.Single(
                    parsed.Materials);

            Assert.Equal(
                "facade.png",
                material.TextureName);
        }
        finally
        {
            File.Delete(
                path);
        }
    }

    [Fact]
    public void WriterRejectsInvalidMaterialIndices()
    {
        var geometry =
            new OmsiO3dGeometry(
                true,
                null,
                [
                    0, 0, 0,
                    1, 0, 0,
                    0, 1, 0
                ],
                [
                    0, 0, 1,
                    0, 0, 1,
                    0, 0, 1
                ],
                [
                    0, 0,
                    1, 0,
                    0, 1
                ],
                [
                    0, 1, 2
                ],
                [
                    1
                ],
                [
                    new OmsiO3dMaterial(
                        1,
                        1,
                        1,
                        1,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0,
                        null)
                ]);

        using var stream =
            new MemoryStream();

        Assert.Throws<
            InvalidDataException>(
                () =>
                    new OmsiO3dGeometryWriter()
                        .Write(
                            stream,
                            geometry));
    }
}
