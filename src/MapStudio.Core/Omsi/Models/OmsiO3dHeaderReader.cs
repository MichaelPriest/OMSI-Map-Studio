using System.Buffers.Binary;

namespace MapStudio.Core.Omsi.Models;

public sealed class OmsiO3dHeaderReader
{
    private const byte Signature0 = 0x84;
    private const byte Signature1 = 0x19;

    public async Task<OmsiO3dHeader> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return OmsiO3dHeader.Missing;
        }

        var buffer = new byte[8];

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var bytesRead = 0;

        while (bytesRead < buffer.Length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(
                    bytesRead,
                    buffer.Length - bytesRead),
                cancellationToken);

            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        if (bytesRead < 3 ||
            buffer[0] != Signature0 ||
            buffer[1] != Signature1)
        {
            return new OmsiO3dHeader(
                Exists: true,
                IsValid: false,
                Version: null,
                HasExtendedHeader: false,
                UsesLongTriangleIndices: false,
                UsesAlternativeEncryptionSeed: false,
                IsEncrypted: false);
        }

        var version = buffer[2];
        var hasExtendedHeader = version > 3;

        if (!hasExtendedHeader)
        {
            return new OmsiO3dHeader(
                Exists: true,
                IsValid: true,
                Version: version,
                HasExtendedHeader: false,
                UsesLongTriangleIndices: false,
                UsesAlternativeEncryptionSeed: false,
                IsEncrypted: false);
        }

        if (bytesRead < 8)
        {
            return new OmsiO3dHeader(
                Exists: true,
                IsValid: false,
                Version: version,
                HasExtendedHeader: true,
                UsesLongTriangleIndices: false,
                UsesAlternativeEncryptionSeed: false,
                IsEncrypted: false);
        }

        var options = buffer[3];
        var encryptionKey =
            BinaryPrimitives.ReadUInt32LittleEndian(
                buffer.AsSpan(4, 4));

        return new OmsiO3dHeader(
            Exists: true,
            IsValid: true,
            Version: version,
            HasExtendedHeader: true,
            UsesLongTriangleIndices:
                (options & 0x01) != 0,
            UsesAlternativeEncryptionSeed:
                (options & 0x02) != 0,
            IsEncrypted:
                encryptionKey != uint.MaxValue);
    }
}
