using System.Buffers.Binary;

namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusTextureMetadata(
    int Width,
    int Height,
    string Format)
{
    public int MaxDimension =>
        Math.Max(
            Width,
            Height);
}

public static class ProtonBusTextureMetadataReader
{
    public static bool TryRead(
        string sourcePath,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        metadata =
            new(
                0,
                0,
                string.Empty);

        error =
            null;

        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                sourcePath);

            if (
                !File.Exists(
                    sourcePath))
            {
                error =
                    "Texture file was not found.";

                return false;
            }

            var extension =
                Path.GetExtension(
                    sourcePath)
                    .ToLowerInvariant();

            using var stream =
                new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

            return extension switch
            {
                ".png" =>
                    TryReadPng(
                        stream,
                        out metadata,
                        out error),
                ".dds" =>
                    TryReadDds(
                        stream,
                        out metadata,
                        out error),
                ".tga" =>
                    TryReadTga(
                        stream,
                        out metadata,
                        out error),
                ".bmp" =>
                    TryReadBmp(
                        stream,
                        out metadata,
                        out error),
                ".gif" =>
                    TryReadGif(
                        stream,
                        out metadata,
                        out error),
                ".jpg" or ".jpeg" =>
                    TryReadJpeg(
                        stream,
                        out metadata,
                        out error),
                _ =>
                    Unsupported(
                        extension,
                        out metadata,
                        out error)
            };
        }
        catch (Exception exception) when (
            exception is
                IOException or
                UnauthorizedAccessException or
                EndOfStreamException or
                ArgumentException or
                NotSupportedException)
        {
            error =
                exception.Message;

            return false;
        }
    }

    private static bool TryReadPng(
        Stream stream,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        Span<byte> header =
            stackalloc byte[24];

        if (
            stream.Read(
                header) !=
            header.Length ||
            !header[..8]
                .SequenceEqual(
                    new byte[]
                    {
                        137,
                        80,
                        78,
                        71,
                        13,
                        10,
                        26,
                        10
                    }))
        {
            return Invalid(
                "PNG header is invalid or incomplete.",
                out metadata,
                out error);
        }

        return Complete(
            BinaryPrimitives
                .ReadInt32BigEndian(
                    header[16..20]),
            BinaryPrimitives
                .ReadInt32BigEndian(
                    header[20..24]),
            "PNG",
            out metadata,
            out error);
    }

    private static bool TryReadDds(
        Stream stream,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        Span<byte> header =
            stackalloc byte[20];

        if (
            stream.Read(
                header) !=
            header.Length ||
            !header[..4]
                .SequenceEqual(
                    "DDS "u8))
        {
            return Invalid(
                "DDS header is invalid or incomplete.",
                out metadata,
                out error);
        }

        return Complete(
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    header[16..20]),
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    header[12..16]),
            "DDS",
            out metadata,
            out error);
    }

    private static bool TryReadTga(
        Stream stream,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        Span<byte> header =
            stackalloc byte[18];

        if (
            stream.Read(
                header) !=
            header.Length)
        {
            return Invalid(
                "TGA header is incomplete.",
                out metadata,
                out error);
        }

        return Complete(
            BinaryPrimitives
                .ReadUInt16LittleEndian(
                    header[12..14]),
            BinaryPrimitives
                .ReadUInt16LittleEndian(
                    header[14..16]),
            "TGA",
            out metadata,
            out error);
    }

    private static bool TryReadBmp(
        Stream stream,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        Span<byte> header =
            stackalloc byte[26];

        if (
            stream.Read(
                header) !=
            header.Length ||
            header[0] !=
                (byte)'B' ||
            header[1] !=
                (byte)'M')
        {
            return Invalid(
                "BMP header is invalid or incomplete.",
                out metadata,
                out error);
        }

        var height =
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    header[22..26]);

        return Complete(
            BinaryPrimitives
                .ReadInt32LittleEndian(
                    header[18..22]),
            Math.Abs(
                height),
            "BMP",
            out metadata,
            out error);
    }

    private static bool TryReadGif(
        Stream stream,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        Span<byte> header =
            stackalloc byte[10];

        if (
            stream.Read(
                header) !=
            header.Length ||
            !(
                header[..6]
                    .SequenceEqual(
                        "GIF87a"u8) ||
                header[..6]
                    .SequenceEqual(
                        "GIF89a"u8)
            ))
        {
            return Invalid(
                "GIF header is invalid or incomplete.",
                out metadata,
                out error);
        }

        return Complete(
            BinaryPrimitives
                .ReadUInt16LittleEndian(
                    header[6..8]),
            BinaryPrimitives
                .ReadUInt16LittleEndian(
                    header[8..10]),
            "GIF",
            out metadata,
            out error);
    }

    private static bool TryReadJpeg(
        Stream stream,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        if (
            stream.ReadByte() !=
                0xFF ||
            stream.ReadByte() !=
                0xD8)
        {
            return Invalid(
                "JPEG SOI marker is missing.",
                out metadata,
                out error);
        }

        while (
            stream.Position <
            stream.Length)
        {
            var prefix =
                stream.ReadByte();

            if (
                prefix !=
                0xFF)
            {
                continue;
            }

            int marker;

            do
            {
                marker =
                    stream.ReadByte();
            }
            while (
                marker ==
                    0xFF);

            if (
                marker <
                0)
            {
                break;
            }

            if (
                marker is
                    0xD8 or
                    0xD9)
            {
                continue;
            }

            if (
                marker ==
                0xDA)
            {
                break;
            }

            var length =
                ReadUInt16BigEndian(
                    stream);

            if (
                length <
                2)
            {
                return Invalid(
                    "JPEG segment length is invalid.",
                    out metadata,
                    out error);
            }

            if (
                IsStartOfFrame(
                    marker))
            {
                if (
                    length <
                    7)
                {
                    return Invalid(
                        "JPEG SOF segment is incomplete.",
                        out metadata,
                        out error);
                }

                _ =
                    stream.ReadByte();

                var height =
                    ReadUInt16BigEndian(
                        stream);

                var width =
                    ReadUInt16BigEndian(
                        stream);

                return Complete(
                    width,
                    height,
                    "JPEG",
                    out metadata,
                    out error);
            }

            stream.Seek(
                length -
                2,
                SeekOrigin.Current);
        }

        return Invalid(
            "JPEG dimensions could not be located.",
            out metadata,
            out error);
    }

    private static bool IsStartOfFrame(
        int marker) =>
        marker is
            0xC0 or
            0xC1 or
            0xC2 or
            0xC3 or
            0xC5 or
            0xC6 or
            0xC7 or
            0xC9 or
            0xCA or
            0xCB or
            0xCD or
            0xCE or
            0xCF;

    private static ushort ReadUInt16BigEndian(
        Stream stream)
    {
        Span<byte> buffer =
            stackalloc byte[2];

        if (
            stream.Read(
                buffer) !=
            2)
        {
            throw new EndOfStreamException();
        }

        return BinaryPrimitives
            .ReadUInt16BigEndian(
                buffer);
    }

    private static bool Complete(
        int width,
        int height,
        string format,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        if (
            width <=
                0 ||
            height <=
                0)
        {
            return Invalid(
                $"{format} dimensions are invalid.",
                out metadata,
                out error);
        }

        metadata =
            new(
                width,
                height,
                format);

        error =
            null;

        return true;
    }

    private static bool Unsupported(
        string extension,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        metadata =
            new(
                0,
                0,
                string.Empty);

        error =
            $"Texture metadata reader does not support '{extension}'.";

        return false;
    }

    private static bool Invalid(
        string message,
        out ProtonBusTextureMetadata metadata,
        out string? error)
    {
        metadata =
            new(
                0,
                0,
                string.Empty);

        error =
            message;

        return false;
    }
}
