namespace MapStudio.Core.Omsi.Maps;

public sealed class OmsiTerrainRenderDataReader
{
    private const byte VertexSection = 0x17;
    private const byte TriangleSection = 0x49;
    private const byte MaterialSection = 0x26;
    private const byte BoneSection = 0x54;
    private const byte TransformSection = 0x79;

    private const uint MaxVertices = 150_000;
    private const uint MaxTriangles = 300_000;
    private const ushort MaxMaterials = 4_096;

    public OmsiTerrainRenderDataSummary Read(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        if (!File.Exists(path))
        {
            return OmsiTerrainRenderDataSummary
                .Missing;
        }

        var fileSize =
            new FileInfo(path).Length;

        try
        {
            using var stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            using var reader =
                new BinaryReader(stream);

            if (!HasRemaining(stream, 8))
            {
                return Error(
                    fileSize,
                    "truncatedHeader");
            }

            if (
                reader.ReadByte() != 0x84 ||
                reader.ReadByte() != 0x19)
            {
                return Error(
                    fileSize,
                    "invalidSignature");
            }

            var version =
                reader.ReadByte();

            if (version <= 3)
            {
                return Error(
                    fileSize,
                    "unsupportedVersion");
            }

            var options =
                reader.ReadByte();

            var key =
                reader.ReadUInt32();

            if (key != 0)
            {
                return Error(
                    fileSize,
                    "unsupportedKey");
            }

            var longTriangleIndices =
                (options & 0x01) != 0;

            uint vertexCount = 0;
            uint triangleCount = 0;
            ushort materialCount = 0;
            var hasTransform = false;

            while (
                stream.Position <
                stream.Length)
            {
                var section =
                    reader.ReadByte();

                switch (section)
                {
                    case VertexSection:
                        if (
                            !TryReadCount(
                                reader,
                                out vertexCount) ||
                            vertexCount >
                                MaxVertices ||
                            !TrySkip(
                                stream,
                                checked(
                                    (long)
                                    vertexCount *
                                    32)))
                        {
                            return Error(
                                fileSize,
                                "invalidVertexSection");
                        }
                        break;

                    case TriangleSection:
                        if (
                            !TryReadCount(
                                reader,
                                out triangleCount) ||
                            triangleCount >
                                MaxTriangles)
                        {
                            return Error(
                                fileSize,
                                "invalidTriangleSection");
                        }

                        var triangleSize =
                            longTriangleIndices
                                ? 14L
                                : 8L;

                        if (
                            !TrySkip(
                                stream,
                                checked(
                                    (long)
                                    triangleCount *
                                    triangleSize)))
                        {
                            return Error(
                                fileSize,
                                "invalidTriangleSection");
                        }
                        break;

                    case MaterialSection:
                        if (
                            !TryReadMaterials(
                                reader,
                                stream,
                                out materialCount))
                        {
                            return Error(
                                fileSize,
                                "invalidMaterialSection");
                        }
                        break;

                    case BoneSection:
                        if (
                            !SkipBones(
                                reader,
                                stream,
                                longTriangleIndices))
                        {
                            return Error(
                                fileSize,
                                "invalidBoneSection");
                        }
                        break;

                    case TransformSection:
                        if (
                            !TrySkip(
                                stream,
                                64))
                        {
                            return Error(
                                fileSize,
                                "invalidTransformSection");
                        }

                        hasTransform = true;
                        break;

                    default:
                        return Error(
                            fileSize,
                            $"unexpectedSection_{section:X2}");
                }
            }

            if (
                vertexCount == 0 ||
                triangleCount == 0)
            {
                return Error(
                    fileSize,
                    "noRenderableGeometry");
            }

            return new OmsiTerrainRenderDataSummary(
                Exists: true,
                IsValid: true,
                FileSize: fileSize,
                VertexCount: vertexCount,
                TriangleCount: triangleCount,
                MaterialCount:
                    materialCount,
                HasTransform:
                    hasTransform,
                ErrorCode: null);
        }
        catch (
            EndOfStreamException)
        {
            return Error(
                fileSize,
                "unexpectedEndOfFile");
        }
        catch (OverflowException)
        {
            return Error(
                fileSize,
                "renderDataTooLarge");
        }
        catch (IOException)
        {
            return Error(
                fileSize,
                "ioError");
        }
    }

    private static bool TryReadCount(
        BinaryReader reader,
        out uint count)
    {
        count = 0;

        if (
            !HasRemaining(
                reader.BaseStream,
                4))
        {
            return false;
        }

        count =
            reader.ReadUInt32();

        return true;
    }

    private static bool TryReadMaterials(
        BinaryReader reader,
        Stream stream,
        out ushort materialCount)
    {
        materialCount = 0;

        if (!HasRemaining(stream, 2))
        {
            return false;
        }

        materialCount =
            reader.ReadUInt16();

        if (
            materialCount >
                MaxMaterials)
        {
            return false;
        }

        for (
            var index = 0;
            index < materialCount;
            index++)
        {
            if (!HasRemaining(stream, 45))
            {
                return false;
            }

            if (!TrySkip(stream, 44))
            {
                return false;
            }

            var textureLength =
                reader.ReadByte();

            if (
                !TrySkip(
                    stream,
                    textureLength))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SkipBones(
        BinaryReader reader,
        Stream stream,
        bool longTriangleIndices)
    {
        if (
            !TryReadCount(
                reader,
                out var boneCount))
        {
            return false;
        }

        for (
            var index = 0U;
            index < boneCount;
            index++)
        {
            if (!HasRemaining(stream, 1))
            {
                return false;
            }

            var nameLength =
                reader.ReadByte();

            if (
                !TrySkip(
                    stream,
                    nameLength) ||
                !HasRemaining(
                    stream,
                    2))
            {
                return false;
            }

            var weightCount =
                reader.ReadUInt16();

            var weightSize =
                longTriangleIndices
                    ? 8L
                    : 6L;

            if (
                !TrySkip(
                    stream,
                    checked(
                        (long)
                        weightCount *
                        weightSize)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TrySkip(
        Stream stream,
        long bytes)
    {
        if (
            !HasRemaining(
                stream,
                bytes))
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

    private static OmsiTerrainRenderDataSummary
        Error(
            long fileSize,
            string code) =>
        new(
            Exists: true,
            IsValid: false,
            FileSize: fileSize,
            VertexCount: 0,
            TriangleCount: 0,
            MaterialCount: 0,
            HasTransform: false,
            ErrorCode: code);
}
