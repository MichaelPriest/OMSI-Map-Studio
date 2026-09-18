namespace MapStudio.Core.Omsi.Models;

public sealed class OmsiO3dStructureReader
{
    private const byte VertexSection = 0x17;
    private const byte TriangleSection = 0x49;
    private const byte MaterialSection = 0x26;
    private const byte BoneSection = 0x54;
    private const byte TransformSection = 0x79;

    public OmsiO3dStructureSummary Read(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return OmsiO3dStructureSummary.Invalid(
                "missingFile");
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        using var reader = new BinaryReader(stream);

        if (stream.Length < 3)
        {
            return OmsiO3dStructureSummary.Invalid(
                "truncatedHeader");
        }

        if (reader.ReadByte() != 0x84 ||
            reader.ReadByte() != 0x19)
        {
            return OmsiO3dStructureSummary.Invalid(
                "invalidSignature");
        }

        var version = reader.ReadByte();
        var longHeader = version > 3;
        var longTriangleIndices = false;

        if (longHeader)
        {
            if (!HasRemaining(stream, 5))
            {
                return OmsiO3dStructureSummary.Invalid(
                    "truncatedExtendedHeader");
            }

            var options = reader.ReadByte();
            _ = reader.ReadUInt32();
            longTriangleIndices =
                (options & 0x01) != 0;
        }

        uint vertexCount = 0;
        uint triangleCount = 0;
        ushort materialCount = 0;
        uint boneCount = 0;
        var hasTransform = false;

        while (stream.Position < stream.Length)
        {
            var section = reader.ReadByte();

            switch (section)
            {
                case VertexSection:
                    if (!TryReadCount(
                            reader,
                            longHeader,
                            out vertexCount) ||
                        !TrySkip(
                            stream,
                            checked(
                                (long)vertexCount * 32)))
                    {
                        return OmsiO3dStructureSummary.Invalid(
                            "invalidVertexSection");
                    }
                    break;

                case TriangleSection:
                    if (!TryReadCount(
                            reader,
                            longHeader,
                            out triangleCount))
                    {
                        return OmsiO3dStructureSummary.Invalid(
                            "invalidTriangleSection");
                    }

                    var triangleSize =
                        longTriangleIndices
                            ? 14L
                            : 8L;

                    if (!TrySkip(
                            stream,
                            checked(
                                (long)triangleCount *
                                triangleSize)))
                    {
                        return OmsiO3dStructureSummary.Invalid(
                            "invalidTriangleSection");
                    }
                    break;

                case MaterialSection:
                    if (!HasRemaining(stream, 2))
                    {
                        return OmsiO3dStructureSummary.Invalid(
                            "invalidMaterialSection");
                    }

                    materialCount =
                        reader.ReadUInt16();

                    for (var index = 0;
                         index < materialCount;
                         index++)
                    {
                        if (!TrySkip(stream, 44) ||
                            !HasRemaining(stream, 1))
                        {
                            return OmsiO3dStructureSummary.Invalid(
                                "invalidMaterialSection");
                        }

                        var texturePathLength =
                            reader.ReadByte();

                        if (!TrySkip(
                                stream,
                                texturePathLength))
                        {
                            return OmsiO3dStructureSummary.Invalid(
                                "invalidMaterialSection");
                        }
                    }
                    break;

                case BoneSection:
                    if (!TryReadCount(
                            reader,
                            longHeader,
                            out boneCount))
                    {
                        return OmsiO3dStructureSummary.Invalid(
                            "invalidBoneSection");
                    }

                    for (uint index = 0;
                         index < boneCount;
                         index++)
                    {
                        if (!HasRemaining(stream, 1))
                        {
                            return OmsiO3dStructureSummary.Invalid(
                                "invalidBoneSection");
                        }

                        var nameLength =
                            reader.ReadByte();

                        if (!TrySkip(
                                stream,
                                nameLength) ||
                            !HasRemaining(stream, 2))
                        {
                            return OmsiO3dStructureSummary.Invalid(
                                "invalidBoneSection");
                        }

                        var weightCount =
                            reader.ReadUInt16();

                        var weightSize =
                            longTriangleIndices
                                ? 8L
                                : 6L;

                        if (!TrySkip(
                                stream,
                                checked(
                                    (long)weightCount *
                                    weightSize)))
                        {
                            return OmsiO3dStructureSummary.Invalid(
                                "invalidBoneSection");
                        }
                    }
                    break;

                case TransformSection:
                    if (!TrySkip(stream, 64))
                    {
                        return OmsiO3dStructureSummary.Invalid(
                            "invalidTransformSection");
                    }

                    hasTransform = true;
                    break;

                default:
                    return OmsiO3dStructureSummary.Invalid(
                        $"unexpectedSection_{section:X2}");
            }
        }

        return new OmsiO3dStructureSummary(
            IsParsed: true,
            VertexCount: vertexCount,
            TriangleCount: triangleCount,
            MaterialCount: materialCount,
            BoneCount: boneCount,
            HasTransform: hasTransform,
            ErrorCode: null);
    }

    private static bool TryReadCount(
        BinaryReader reader,
        bool longHeader,
        out uint count)
    {
        count = 0;
        var stream = reader.BaseStream;
        var required =
            longHeader ? 4 : 2;

        if (!HasRemaining(
                stream,
                required))
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

        stream.Seek(
            bytes,
            SeekOrigin.Current);

        return true;
    }

    private static bool HasRemaining(
        Stream stream,
        long bytes) =>
        bytes >= 0 &&
        stream.Position <=
            stream.Length - bytes;
}
