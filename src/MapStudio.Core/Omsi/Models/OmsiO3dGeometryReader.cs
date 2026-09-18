namespace MapStudio.Core.Omsi.Models;

public sealed class OmsiO3dGeometryReader
{
    private const byte VertexSection = 0x17;
    private const byte TriangleSection = 0x49;
    private const byte MaterialSection = 0x26;
    private const byte BoneSection = 0x54;
    private const byte TransformSection = 0x79;

    private const uint MaxVertices = 150_000;
    private const uint MaxTriangles = 300_000;

    public OmsiO3dGeometry Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return OmsiO3dGeometry.Error("missingFile");
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

            using var reader = new BinaryReader(stream);

            if (!HasRemaining(stream, 3))
            {
                return OmsiO3dGeometry.Error("truncatedHeader");
            }

            if (reader.ReadByte() != 0x84 ||
                reader.ReadByte() != 0x19)
            {
                return OmsiO3dGeometry.Error("invalidSignature");
            }

            var version = reader.ReadByte();
            var longHeader = version > 3;
            var longTriangleIndices = false;

            if (longHeader)
            {
                if (!HasRemaining(stream, 5))
                {
                    return OmsiO3dGeometry.Error(
                        "truncatedExtendedHeader");
                }

                var options = reader.ReadByte();
                var encryptionKey = reader.ReadUInt32();

                longTriangleIndices =
                    (options & 0x01) != 0;

                if (encryptionKey != uint.MaxValue)
                {
                    return OmsiO3dGeometry.Error("encrypted");
                }
            }

            float[]? positions = null;
            float[]? normals = null;
            float[]? uvs = null;
            uint[]? indices = null;
            uint vertexCount = 0;

            while (stream.Position < stream.Length)
            {
                var section = reader.ReadByte();

                switch (section)
                {
                    case VertexSection:
                        if (!TryReadCount(
                                reader,
                                longHeader,
                                out vertexCount))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidVertexSection");
                        }

                        if (vertexCount > MaxVertices)
                        {
                            return OmsiO3dGeometry.Error(
                                "tooManyVertices");
                        }

                        positions =
                            new float[checked((int)vertexCount * 3)];
                        normals =
                            new float[checked((int)vertexCount * 3)];
                        uvs =
                            new float[checked((int)vertexCount * 2)];

                        for (var index = 0U;
                             index < vertexCount;
                             index++)
                        {
                            if (!HasRemaining(stream, 32))
                            {
                                return OmsiO3dGeometry.Error(
                                    "invalidVertexSection");
                            }

                            var x = reader.ReadSingle();
                            var y = reader.ReadSingle();
                            var z = reader.ReadSingle();
                            var nx = reader.ReadSingle();
                            var ny = reader.ReadSingle();
                            var nz = reader.ReadSingle();
                            var u = reader.ReadSingle();
                            var v = reader.ReadSingle();

                            var p = checked((int)index * 3);
                            positions[p] = x;
                            positions[p + 1] = z;
                            positions[p + 2] = y;

                            normals[p] = nx;
                            normals[p + 1] = nz;
                            normals[p + 2] = ny;

                            var t = checked((int)index * 2);
                            uvs[t] = u;
                            uvs[t + 1] = 1 - v;
                        }
                        break;

                    case TriangleSection:
                        if (!TryReadCount(
                                reader,
                                longHeader,
                                out var triangleCount))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidTriangleSection");
                        }

                        if (triangleCount > MaxTriangles)
                        {
                            return OmsiO3dGeometry.Error(
                                "tooManyTriangles");
                        }

                        indices =
                            new uint[checked((int)triangleCount * 3)];

                        for (var index = 0U;
                             index < triangleCount;
                             index++)
                        {
                            if (!HasRemaining(
                                    stream,
                                    longTriangleIndices ? 14 : 8))
                            {
                                return OmsiO3dGeometry.Error(
                                    "invalidTriangleSection");
                            }

                            uint a;
                            uint b;
                            uint c;

                            if (longTriangleIndices)
                            {
                                a = reader.ReadUInt32();
                                b = reader.ReadUInt32();
                                c = reader.ReadUInt32();
                            }
                            else
                            {
                                a = reader.ReadUInt16();
                                b = reader.ReadUInt16();
                                c = reader.ReadUInt16();
                            }

                            _ = reader.ReadUInt16();

                            var t = checked((int)index * 3);
                            indices[t] = c;
                            indices[t + 1] = b;
                            indices[t + 2] = a;
                        }
                        break;

                    case MaterialSection:
                        if (!SkipMaterials(reader, stream))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidMaterialSection");
                        }
                        break;

                    case BoneSection:
                        if (!SkipBones(
                                reader,
                                stream,
                                longHeader,
                                longTriangleIndices))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidBoneSection");
                        }
                        break;

                    case TransformSection:
                        if (!TrySkip(stream, 64))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidTransformSection");
                        }
                        break;

                    default:
                        return OmsiO3dGeometry.Error(
                            $"unexpectedSection_{section:X2}");
                }
            }

            if (positions is null ||
                normals is null ||
                uvs is null ||
                indices is null)
            {
                return OmsiO3dGeometry.Error(
                    "noRenderableGeometry");
            }

            foreach (var index in indices)
            {
                if (index >= vertexCount)
                {
                    return OmsiO3dGeometry.Error(
                        "triangleIndexOutOfRange");
                }
            }

            return new OmsiO3dGeometry(
                IsLoaded: true,
                ErrorCode: null,
                Positions: positions,
                Normals: normals,
                Uvs: uvs,
                Indices: indices);
        }
        catch (EndOfStreamException)
        {
            return OmsiO3dGeometry.Error(
                "unexpectedEndOfFile");
        }
        catch (OverflowException)
        {
            return OmsiO3dGeometry.Error(
                "geometryTooLarge");
        }
    }

    private static bool SkipMaterials(
        BinaryReader reader,
        Stream stream)
    {
        if (!HasRemaining(stream, 2))
        {
            return false;
        }

        var materialCount = reader.ReadUInt16();

        for (var index = 0;
             index < materialCount;
             index++)
        {
            if (!TrySkip(stream, 44) ||
                !HasRemaining(stream, 1))
            {
                return false;
            }

            var textureLength = reader.ReadByte();

            if (!TrySkip(stream, textureLength))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SkipBones(
        BinaryReader reader,
        Stream stream,
        bool longHeader,
        bool longTriangleIndices)
    {
        if (!TryReadCount(
                reader,
                longHeader,
                out var boneCount))
        {
            return false;
        }

        for (var index = 0U;
             index < boneCount;
             index++)
        {
            if (!HasRemaining(stream, 1))
            {
                return false;
            }

            var nameLength = reader.ReadByte();

            if (!TrySkip(stream, nameLength) ||
                !HasRemaining(stream, 2))
            {
                return false;
            }

            var weightCount = reader.ReadUInt16();
            var weightSize =
                longTriangleIndices ? 8L : 6L;

            if (!TrySkip(
                    stream,
                    checked((long)weightCount * weightSize)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadCount(
        BinaryReader reader,
        bool longHeader,
        out uint count)
    {
        count = 0;
        var bytes = longHeader ? 4 : 2;

        if (!HasRemaining(reader.BaseStream, bytes))
        {
            return false;
        }

        count = longHeader
            ? reader.ReadUInt32()
            : reader.ReadUInt16();

        return true;
    }

    private static bool TrySkip(
        Stream stream,
        long bytes)
    {
        if (!HasRemaining(stream, bytes))
        {
            return false;
        }

        stream.Seek(bytes, SeekOrigin.Current);
        return true;
    }

    private static bool HasRemaining(
        Stream stream,
        long bytes) =>
        bytes >= 0 &&
        stream.Position <= stream.Length - bytes;
}
