using System.Text;

namespace MapStudio.Core.Omsi.Models;

public sealed class OmsiO3dGeometryWriter
{
    private const byte VertexSection =
        0x17;

    private const byte TriangleSection =
        0x49;

    private const byte MaterialSection =
        0x26;

    private static readonly Encoding
        Windows1252 =
            CreateWindows1252();

    public async Task WriteAsync(
        string path,
        OmsiO3dGeometry geometry,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        ArgumentNullException.ThrowIfNull(
            geometry);

        Validate(
            geometry);

        var directory =
            Path.GetDirectoryName(
                Path.GetFullPath(
                    path));

        if (
            !string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        var temporaryPath =
            path +
            ".tmp-" +
            Guid.NewGuid()
                .ToString("N");

        try
        {
            await using (
                var stream =
                    new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize:
                            64 *
                            1024,
                        useAsync:
                            true))
            using (
                var writer =
                    new BinaryWriter(
                        stream,
                        Windows1252,
                        leaveOpen:
                            true))
            {
                WriteGeometry(
                    writer,
                    geometry);

                writer.Flush();

                await stream
                    .FlushAsync(
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(
                temporaryPath,
                path,
                overwrite:
                    true);
        }
        finally
        {
            if (
                File.Exists(
                    temporaryPath))
            {
                File.Delete(
                    temporaryPath);
            }
        }
    }

    public void Write(
        Stream stream,
        OmsiO3dGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        ArgumentNullException.ThrowIfNull(
            geometry);

        if (!stream.CanWrite)
        {
            throw new ArgumentException(
                "Stream is not writable.",
                nameof(stream));
        }

        Validate(
            geometry);

        using var writer =
            new BinaryWriter(
                stream,
                Windows1252,
                leaveOpen:
                    true);

        WriteGeometry(
            writer,
            geometry);

        writer.Flush();
    }

    private static void WriteGeometry(
        BinaryWriter writer,
        OmsiO3dGeometry geometry)
    {
        writer.Write(
            (byte)0x84);

        writer.Write(
            (byte)0x19);

        // Version 3 keeps the file deliberately simple:
        // no encryption/protection and 16-bit indices/counts.
        writer.Write(
            (byte)3);

        writer.Write(
            VertexSection);

        writer.Write(
            checked(
                (ushort)(
                    geometry.Positions.Length /
                    3)));

        var vertexCount =
            geometry.Positions.Length /
            3;

        for (
            var index = 0;
            index < vertexCount;
            index++)
        {
            var p =
                index *
                3;

            var t =
                index *
                2;

            writer.Write(
                geometry.Positions[p]);

            writer.Write(
                geometry.Positions[
                    p +
                    1]);

            writer.Write(
                geometry.Positions[
                    p +
                    2]);

            writer.Write(
                geometry.Normals[p]);

            writer.Write(
                geometry.Normals[
                    p +
                    1]);

            writer.Write(
                geometry.Normals[
                    p +
                    2]);

            writer.Write(
                geometry.Uvs[t]);

            // Reader converts OMSI V into the renderer's
            // top-left texture convention by applying 1 - v.
            writer.Write(
                1.0f -
                geometry.Uvs[
                    t +
                    1]);
        }

        writer.Write(
            TriangleSection);

        var triangleCount =
            geometry.Indices.Length /
            3;

        writer.Write(
            checked(
                (ushort)
                    triangleCount));

        for (
            var triangle = 0;
            triangle < triangleCount;
            triangle++)
        {
            var index =
                triangle *
                3;

            writer.Write(
                checked(
                    (ushort)
                        geometry.Indices[
                            index]));

            writer.Write(
                checked(
                    (ushort)
                        geometry.Indices[
                            index +
                            1]));

            writer.Write(
                checked(
                    (ushort)
                        geometry.Indices[
                            index +
                            2]));

            writer.Write(
                geometry
                    .TriangleMaterialIndices[
                        triangle]);
        }

        writer.Write(
            MaterialSection);

        writer.Write(
            checked(
                (ushort)
                    geometry.Materials.Count));

        foreach (
            var material in
                geometry.Materials)
        {
            writer.Write(
                material.DiffuseR);
            writer.Write(
                material.DiffuseG);
            writer.Write(
                material.DiffuseB);
            writer.Write(
                material.DiffuseA);

            writer.Write(
                material.SpecularR);
            writer.Write(
                material.SpecularG);
            writer.Write(
                material.SpecularB);

            writer.Write(
                material.EmissionR);
            writer.Write(
                material.EmissionG);
            writer.Write(
                material.EmissionB);

            writer.Write(
                material.SpecularPower);

            var textureBytes =
                string.IsNullOrWhiteSpace(
                    material.TextureName)
                    ? Array.Empty<byte>()
                    : Windows1252
                        .GetBytes(
                            material.TextureName);

            if (
                textureBytes.Length >
                byte.MaxValue)
            {
                throw new InvalidDataException(
                    "o3dTextureNameTooLong");
            }

            writer.Write(
                checked(
                    (byte)
                        textureBytes.Length));

            if (
                textureBytes.Length >
                0)
            {
                writer.Write(
                    textureBytes);
            }
        }
    }

    private static void Validate(
        OmsiO3dGeometry geometry)
    {
        if (!geometry.IsLoaded)
        {
            throw new InvalidDataException(
                "o3dGeometryNotLoaded");
        }

        if (
            geometry.Positions.Length %
                3 !=
            0 ||
            geometry.Normals.Length !=
                geometry.Positions.Length)
        {
            throw new InvalidDataException(
                "o3dInvalidVertexArrays");
        }

        var vertexCount =
            geometry.Positions.Length /
            3;

        if (
            vertexCount <=
                0 ||
            vertexCount >
                ushort.MaxValue)
        {
            throw new InvalidDataException(
                "o3dVertexCountUnsupported");
        }

        if (
            geometry.Uvs.Length !=
            vertexCount *
            2)
        {
            throw new InvalidDataException(
                "o3dInvalidUvArray");
        }

        if (
            geometry.Indices.Length %
                3 !=
            0)
        {
            throw new InvalidDataException(
                "o3dInvalidIndexArray");
        }

        var triangleCount =
            geometry.Indices.Length /
            3;

        if (
            triangleCount <=
                0 ||
            triangleCount >
                ushort.MaxValue ||
            geometry
                .TriangleMaterialIndices
                .Length !=
                triangleCount)
        {
            throw new InvalidDataException(
                "o3dTriangleCountUnsupported");
        }

        if (
            geometry.Materials.Count ==
                0 ||
            geometry.Materials.Count >
                ushort.MaxValue)
        {
            throw new InvalidDataException(
                "o3dMaterialCountUnsupported");
        }

        for (
            var index = 0;
            index < geometry.Indices.Length;
            index++)
        {
            if (
                geometry.Indices[index] >=
                vertexCount)
            {
                throw new InvalidDataException(
                    "o3dTriangleIndexOutOfRange");
            }
        }

        foreach (
            var materialIndex in
                geometry
                    .TriangleMaterialIndices)
        {
            if (
                materialIndex >=
                geometry.Materials.Count)
            {
                throw new InvalidDataException(
                    "o3dMaterialIndexOutOfRange");
            }
        }
    }

    private static Encoding
        CreateWindows1252()
    {
        Encoding.RegisterProvider(
            CodePagesEncodingProvider
                .Instance);

        return Encoding.GetEncoding(
            1252);
    }
}
