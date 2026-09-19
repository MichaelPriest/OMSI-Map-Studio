using System.Numerics;
using System.Text;

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
    private const ushort MaxMaterials = 4_096;

    private static readonly Encoding Windows1252 =
        CreateWindows1252();

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
            ushort[]? triangleMaterialIndices = null;
            IReadOnlyList<OmsiO3dMaterial> materials =
                Array.Empty<OmsiO3dMaterial>();

            uint vertexCount = 0;
            Matrix4x4? fileTransform = null;

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

                        triangleMaterialIndices =
                            new ushort[checked((int)triangleCount)];

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

                            var materialIndex =
                                reader.ReadUInt16();

                            var t = checked((int)index * 3);
                            indices[t] = c;
                            indices[t + 1] = b;
                            indices[t + 2] = a;

                            triangleMaterialIndices[
                                checked((int)index)] =
                                materialIndex;
                        }
                        break;

                    case MaterialSection:
                        if (!TryReadMaterials(
                                reader,
                                stream,
                                out materials))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidMaterialSection");
                        }
                        break;

                    case BoneSection:
                        if (!SkipBones(
                                reader,
                                stream,
                                longTriangleIndices))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidBoneSection");
                        }
                        break;

                    case TransformSection:
                        if (!HasRemaining(stream, 64))
                        {
                            return OmsiO3dGeometry.Error(
                                "invalidTransformSection");
                        }

                        fileTransform =
                            new Matrix4x4(
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle(),
                                reader.ReadSingle());
                        break;

                    default:
                        return OmsiO3dGeometry.Error(
                            $"unexpectedSection_{section:X2}");
                }
            }

            if (positions is null ||
                normals is null ||
                uvs is null ||
                indices is null ||
                triangleMaterialIndices is null)
            {
                return OmsiO3dGeometry.Error(
                    "noRenderableGeometry");
            }

            if (fileTransform is Matrix4x4 transform)
            {
                if (!Matrix4x4.Invert(
                        transform,
                        out var inverseTransform))
                {
                    return OmsiO3dGeometry.Error(
                        "invalidTransformMatrix");
                }

                var axisSwap =
                    new Matrix4x4(
                        1, 0, 0, 0,
                        0, 0, 1, 0,
                        0, 1, 0, 0,
                        0, 0, 0, 1);

                var convertedTransform =
                    axisSwap *
                    inverseTransform *
                    axisSwap;

                for (
                    var vertexIndex = 0;
                    vertexIndex <
                        checked((int)vertexCount);
                    vertexIndex++)
                {
                    var offset =
                        vertexIndex * 3;

                    var position =
                        Vector3.Transform(
                            new Vector3(
                                positions[offset],
                                positions[offset + 1],
                                positions[offset + 2]),
                            convertedTransform);

                    positions[offset] =
                        position.X;
                    positions[offset + 1] =
                        position.Y;
                    positions[offset + 2] =
                        position.Z;

                    var normal =
                        Vector3.TransformNormal(
                            new Vector3(
                                normals[offset],
                                normals[offset + 1],
                                normals[offset + 2]),
                            convertedTransform);

                    if (
                        normal.LengthSquared() >
                        0.0000001f)
                    {
                        normal =
                            Vector3.Normalize(
                                normal);
                    }

                    normals[offset] =
                        normal.X;
                    normals[offset + 1] =
                        normal.Y;
                    normals[offset + 2] =
                        normal.Z;
                }
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
                Indices: indices,
                TriangleMaterialIndices:
                    triangleMaterialIndices,
                Materials: materials);
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

    private static bool TryReadMaterials(
        BinaryReader reader,
        Stream stream,
        out IReadOnlyList<OmsiO3dMaterial> materials)
    {
        materials = Array.Empty<OmsiO3dMaterial>();

        if (!HasRemaining(stream, 2))
        {
            return false;
        }

        var materialCount = reader.ReadUInt16();

        if (materialCount > MaxMaterials)
        {
            return false;
        }

        var result =
            new List<OmsiO3dMaterial>(materialCount);

        for (var index = 0;
             index < materialCount;
             index++)
        {
            if (!HasRemaining(stream, 45))
            {
                return false;
            }

            var diffuseR = reader.ReadSingle();
            var diffuseG = reader.ReadSingle();
            var diffuseB = reader.ReadSingle();
            var diffuseA = reader.ReadSingle();

            var specularR = reader.ReadSingle();
            var specularG = reader.ReadSingle();
            var specularB = reader.ReadSingle();

            var emissionR = reader.ReadSingle();
            var emissionG = reader.ReadSingle();
            var emissionB = reader.ReadSingle();

            var specularPower = reader.ReadSingle();

            var textureLength = reader.ReadByte();

            if (!HasRemaining(
                    stream,
                    textureLength))
            {
                return false;
            }

            var textureName =
                textureLength == 0
                    ? null
                    : Windows1252.GetString(
                        reader.ReadBytes(
                            textureLength));

            result.Add(new OmsiO3dMaterial(
                diffuseR,
                diffuseG,
                diffuseB,
                diffuseA,
                specularR,
                specularG,
                specularB,
                emissionR,
                emissionG,
                emissionB,
                specularPower,
                string.IsNullOrWhiteSpace(
                    textureName)
                    ? null
                    : textureName));
        }

        materials = result;
        return true;
    }

    private static bool SkipBones(
        BinaryReader reader,
        Stream stream,
        bool longTriangleIndices)
    {
        // OMSI keeps the bone-list count at UInt16 even when
        // vertex/triangle sections use the extended long header.
        if (!HasRemaining(stream, 2))
        {
            return false;
        }

        var boneCount =
            (uint)reader.ReadUInt16();

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

    private static Encoding CreateWindows1252()
    {
        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        return Encoding.GetEncoding(1252);
    }
}
