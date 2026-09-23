using System.Text;

namespace MapStudio.Core.ProtonBus;

public static class ProtonBus3dsWriter
{
    private const ushort Main3ds = 0x4D4D;
    private const ushort Version = 0x0002;
    private const ushort ObjectInfo = 0x3D3D;
    private const ushort MeshVersion = 0x3D3E;
    private const ushort MasterScale = 0x0100;

    private const ushort Material = 0xAFFF;
    private const ushort MaterialName = 0xA000;
    private const ushort MaterialDiffuse = 0xA020;
    private const ushort MaterialTransparency = 0xA050;
    private const ushort MaterialSelfIllumination = 0xA084;
    private const ushort MaterialDiffuseMap = 0xA200;
    private const ushort MaterialMapFile = 0xA300;
    private const ushort ColorRgbBytes = 0x0011;
    private const ushort PercentInteger = 0x0030;

    private const ushort Object = 0x4000;
    private const ushort TriMesh = 0x4100;
    private const ushort VertexList = 0x4110;
    private const ushort FaceList = 0x4120;
    private const ushort FaceMaterial = 0x4130;
    private const ushort MappingCoordinates = 0x4140;

    public static byte[] Write(
        ProtonBusExportScene scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        var chunks =
            scene.Meshes
                .SelectMany(
                    mesh =>
                        ProtonBus3dsMeshChunkPlanner
                            .Plan(
                                mesh))
                .ToArray();

        ValidateMaterials(
            chunks);

        return Chunk(
            Main3ds,
            writer =>
            {
                WriteChunk(
                    writer,
                    Version,
                    versionWriter =>
                        versionWriter.Write(
                            3u));

                WriteChunk(
                    writer,
                    ObjectInfo,
                    objectInfoWriter =>
                    {
                        WriteChunk(
                            objectInfoWriter,
                            MeshVersion,
                            meshVersionWriter =>
                                meshVersionWriter
                                    .Write(
                                        3u));

                        WriteChunk(
                            objectInfoWriter,
                            MasterScale,
                            scaleWriter =>
                                scaleWriter.Write(
                                    1.0f));

                        foreach (
                            var material
                            in chunks
                                .SelectMany(
                                    chunk =>
                                        chunk.Materials)
                                .GroupBy(
                                    material =>
                                        material.Name,
                                    StringComparer
                                        .Ordinal)
                                .Select(
                                    group =>
                                        group.First()))
                        {
                            WriteMaterial(
                                objectInfoWriter,
                                material);
                        }

                        foreach (
                            var chunk
                            in chunks)
                        {
                            WriteObject(
                                objectInfoWriter,
                                chunk);
                        }
                    });
            });
    }

    private static void WriteMaterial(
        BinaryWriter writer,
        ProtonBusExportMaterial material)
    {
        WriteChunk(
            writer,
            Material,
            materialWriter =>
            {
                WriteStringChunk(
                    materialWriter,
                    MaterialName,
                    material.Name);

                WriteChunk(
                    materialWriter,
                    MaterialDiffuse,
                    diffuseWriter =>
                        WriteChunk(
                            diffuseWriter,
                            ColorRgbBytes,
                            colorWriter =>
                            {
                                colorWriter.Write(
                                    (byte)255);
                                colorWriter.Write(
                                    (byte)255);
                                colorWriter.Write(
                                    (byte)255);
                            }));

                if (
                    material.Transparent)
                {
                    WriteChunk(
                        materialWriter,
                        MaterialTransparency,
                        transparentWriter =>
                            WriteChunk(
                                transparentWriter,
                                PercentInteger,
                                percentWriter =>
                                    percentWriter.Write(
                                        (ushort)50)));
                }

                if (
                    material.Emissive)
                {
                    WriteChunk(
                        materialWriter,
                        MaterialSelfIllumination,
                        selfIlluminationWriter =>
                            WriteChunk(
                                selfIlluminationWriter,
                                PercentInteger,
                                percentWriter =>
                                    percentWriter.Write(
                                        (ushort)100)));
                }

                if (
                    !string.IsNullOrWhiteSpace(
                        material.TextureFileName))
                {
                    WriteChunk(
                        materialWriter,
                        MaterialDiffuseMap,
                        textureMapWriter =>
                            WriteStringChunk(
                                textureMapWriter,
                                MaterialMapFile,
                                material
                                    .TextureFileName!));
                }
            });
    }

    private static void WriteObject(
        BinaryWriter writer,
        ProtonBus3dsMeshChunk chunk)
    {
        WriteChunk(
            writer,
            Object,
            objectWriter =>
            {
                WriteCString(
                    objectWriter,
                    chunk.Name);

                WriteChunk(
                    objectWriter,
                    TriMesh,
                    meshWriter =>
                    {
                        WriteVertices(
                            meshWriter,
                            chunk);

                        WriteFaces(
                            meshWriter,
                            chunk);

                        WriteTextureCoordinates(
                            meshWriter,
                            chunk);
                    });
            });
    }

    private static void WriteVertices(
        BinaryWriter writer,
        ProtonBus3dsMeshChunk chunk)
    {
        WriteChunk(
            writer,
            VertexList,
            vertexWriter =>
            {
                vertexWriter.Write(
                    checked(
                        (ushort)
                            chunk
                                .Vertices
                                .Count));

                foreach (
                    var vertex
                    in chunk.Vertices)
                {
                    vertexWriter.Write(
                        vertex.Position.X);
                    vertexWriter.Write(
                        vertex.Position.Y);
                    vertexWriter.Write(
                        vertex.Position.Z);
                }
            });
    }

    private static void WriteFaces(
        BinaryWriter writer,
        ProtonBus3dsMeshChunk chunk)
    {
        WriteChunk(
            writer,
            FaceList,
            faceWriter =>
            {
                faceWriter.Write(
                    checked(
                        (ushort)
                            chunk
                                .Triangles
                                .Count));

                foreach (
                    var triangle
                    in chunk.Triangles)
                {
                    faceWriter.Write(
                        checked(
                            (ushort)
                                triangle.A));
                    faceWriter.Write(
                        checked(
                            (ushort)
                                triangle.B));
                    faceWriter.Write(
                        checked(
                            (ushort)
                                triangle.C));

                    faceWriter.Write(
                        (ushort)0);
                }

                foreach (
                    var materialGroup
                    in chunk
                        .Triangles
                        .Select(
                            (triangle, index) =>
                                new
                                {
                                    Triangle =
                                        triangle,
                                    Index =
                                        index
                                })
                        .Where(
                            entry =>
                                !string
                                    .IsNullOrWhiteSpace(
                                        entry
                                            .Triangle
                                            .MaterialName))
                        .GroupBy(
                            entry =>
                                entry
                                    .Triangle
                                    .MaterialName!,
                            StringComparer
                                .Ordinal))
                {
                    WriteChunk(
                        faceWriter,
                        FaceMaterial,
                        materialWriter =>
                        {
                            WriteCString(
                                materialWriter,
                                materialGroup
                                    .Key);

                            materialWriter.Write(
                                checked(
                                    (ushort)
                                        materialGroup
                                            .Count()));

                            foreach (
                                var entry
                                in materialGroup)
                            {
                                materialWriter.Write(
                                    checked(
                                        (ushort)
                                            entry
                                                .Index));
                            }
                        });
                }
            });
    }

    private static void
        WriteTextureCoordinates(
            BinaryWriter writer,
            ProtonBus3dsMeshChunk chunk)
    {
        WriteChunk(
            writer,
            MappingCoordinates,
            textureWriter =>
            {
                textureWriter.Write(
                    checked(
                        (ushort)
                            chunk
                                .Vertices
                                .Count));

                foreach (
                    var vertex
                    in chunk.Vertices)
                {
                    textureWriter.Write(
                        vertex
                            .TextureCoordinate
                            .X);

                    textureWriter.Write(
                        vertex
                            .TextureCoordinate
                            .Y);
                }
            });
    }

    private static void ValidateMaterials(
        IReadOnlyList<
            ProtonBus3dsMeshChunk>
            chunks)
    {
        foreach (
            var chunk
            in chunks)
        {
            var materialNames =
                chunk
                    .Materials
                    .Select(
                        material =>
                            material.Name)
                    .ToHashSet(
                        StringComparer.Ordinal);

            foreach (
                var material
                in chunk.Materials)
            {
                ValidateCString(
                    material.Name,
                    "Material name");

                if (
                    !string.IsNullOrWhiteSpace(
                        material.TextureFileName))
                {
                    ValidateCString(
                        material.TextureFileName!,
                        "Texture file name");
                }
            }

            ValidateCString(
                chunk.Name,
                "Object name");

            foreach (
                var triangle
                in chunk.Triangles)
            {
                if (
                    string.IsNullOrWhiteSpace(
                        triangle.MaterialName))
                {
                    continue;
                }

                if (
                    !materialNames.Contains(
                        triangle.MaterialName))
                {
                    throw new ArgumentException(
                        $"Mesh '{chunk.Name}' references unknown material '{triangle.MaterialName}'.");
                }
            }
        }
    }

    private static void WriteStringChunk(
        BinaryWriter writer,
        ushort id,
        string value) =>
        WriteChunk(
            writer,
            id,
            stringWriter =>
                WriteCString(
                    stringWriter,
                    value));

    private static void WriteCString(
        BinaryWriter writer,
        string value)
    {
        ValidateCString(
            value,
            "3DS string");

        writer.Write(
            Encoding.ASCII.GetBytes(
                value));

        writer.Write(
            (byte)0);
    }

    private static void ValidateCString(
        string value,
        string fieldName)
    {
        if (
            string.IsNullOrWhiteSpace(
                value))
        {
            throw new ArgumentException(
                $"{fieldName} cannot be empty.");
        }

        if (
            value.Contains(
                '\0'))
        {
            throw new ArgumentException(
                $"{fieldName} cannot contain a NUL character.");
        }

        if (
            value.Any(
                character =>
                    character >
                        127))
        {
            throw new ArgumentException(
                $"{fieldName} must be ASCII for Proton Bus 3DS compatibility.");
        }
    }

    private static byte[] Chunk(
        ushort id,
        Action<BinaryWriter> writePayload)
    {
        using var stream =
            new MemoryStream();

        using (
            var writer =
                new BinaryWriter(
                    stream,
                    Encoding.ASCII,
                    leaveOpen: true))
        {
            WriteChunk(
                writer,
                id,
                writePayload);
        }

        return stream.ToArray();
    }

    private static void WriteChunk(
        BinaryWriter writer,
        ushort id,
        Action<BinaryWriter> writePayload)
    {
        using var payloadStream =
            new MemoryStream();

        using (
            var payloadWriter =
                new BinaryWriter(
                    payloadStream,
                    Encoding.ASCII,
                    leaveOpen: true))
        {
            writePayload(
                payloadWriter);
        }

        var payload =
            payloadStream.ToArray();

        writer.Write(
            id);

        writer.Write(
            checked(
                (uint)
                    (payload.Length +
                     6)));

        writer.Write(
            payload);
    }
}
