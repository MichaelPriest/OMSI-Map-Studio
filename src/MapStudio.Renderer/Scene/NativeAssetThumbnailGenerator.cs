using System.Buffers.Binary;
using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed class NativeAssetThumbnailGenerator
{
    public byte[] RenderBmp(
        NativeAssetPreviewGeometry geometry,
        int width = 180,
        int height = 120)
    {
        ArgumentNullException.ThrowIfNull(
            geometry);

        if (
            !geometry.IsRenderable ||
            width is < 32 or > 1024 ||
            height is < 32 or > 1024)
        {
            return [];
        }

        var center =
            (
                geometry.Minimum +
                geometry.Maximum
            ) *
            0.5f;

        var rotation =
            Matrix4x4
                .CreateRotationY(
                    -0.65f) *
            Matrix4x4
                .CreateRotationX(
                    0.42f);

        var transformed =
            new Vector3[
                geometry.Vertices.Length];

        var minX =
            float.PositiveInfinity;

        var maxX =
            float.NegativeInfinity;

        var minY =
            float.PositiveInfinity;

        var maxY =
            float.NegativeInfinity;

        for (
            var index = 0;
            index <
                geometry.Vertices.Length;
            index++)
        {
            var point =
                Vector3.Transform(
                    geometry.Vertices[index]
                        .Position -
                    center,
                    rotation);

            transformed[index] =
                point;

            minX =
                Math.Min(
                    minX,
                    point.X);

            maxX =
                Math.Max(
                    maxX,
                    point.X);

            minY =
                Math.Min(
                    minY,
                    point.Y);

            maxY =
                Math.Max(
                    maxY,
                    point.Y);
        }

        var rangeX =
            Math.Max(
                0.001f,
                maxX -
                minX);

        var rangeY =
            Math.Max(
                0.001f,
                maxY -
                minY);

        const float padding =
            8.0f;

        var scale =
            Math.Min(
                (
                    width -
                    padding *
                    2
                ) /
                rangeX,
                (
                    height -
                    padding *
                    2
                ) /
                rangeY);

        var pixels =
            new uint[
                width *
                height];

        var depth =
            Enumerable
                .Repeat(
                    float.PositiveInfinity,
                    width *
                    height)
                .ToArray();

        var background =
            PackColor(
                8,
                19,
                29);

        Array.Fill(
            pixels,
            background);

        for (
            var triangle = 0;
            triangle <
                geometry.Vertices.Length /
                3;
            triangle++)
        {
            var offset =
                triangle *
                3;

            var a =
                Project(
                    transformed[offset],
                    minX,
                    maxY,
                    scale,
                    padding);

            var b =
                Project(
                    transformed[
                        offset + 1],
                    minX,
                    maxY,
                    scale,
                    padding);

            var c =
                Project(
                    transformed[
                        offset + 2],
                    minX,
                    maxY,
                    scale,
                    padding);

            var color =
                AverageColor(
                    geometry.Vertices[
                        offset].Color,
                    geometry.Vertices[
                        offset + 1].Color,
                    geometry.Vertices[
                        offset + 2].Color);

            RasterizeTriangle(
                a,
                b,
                c,
                color,
                width,
                height,
                pixels,
                depth);
        }

        return EncodeBmp(
            width,
            height,
            pixels);
    }

    private static Vector3 Project(
        Vector3 point,
        float minX,
        float maxY,
        float scale,
        float padding) =>
        new(
            padding +
                (
                    point.X -
                    minX
                ) *
                scale,
            padding +
                (
                    maxY -
                    point.Y
                ) *
                scale,
            point.Z);

    private static void RasterizeTriangle(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        uint color,
        int width,
        int height,
        uint[] pixels,
        float[] depth)
    {
        var minX =
            Math.Clamp(
                (int)MathF.Floor(
                    MathF.Min(
                        a.X,
                        MathF.Min(
                            b.X,
                            c.X))),
                0,
                width - 1);

        var maxX =
            Math.Clamp(
                (int)MathF.Ceiling(
                    MathF.Max(
                        a.X,
                        MathF.Max(
                            b.X,
                            c.X))),
                0,
                width - 1);

        var minY =
            Math.Clamp(
                (int)MathF.Floor(
                    MathF.Min(
                        a.Y,
                        MathF.Min(
                            b.Y,
                            c.Y))),
                0,
                height - 1);

        var maxY =
            Math.Clamp(
                (int)MathF.Ceiling(
                    MathF.Max(
                        a.Y,
                        MathF.Max(
                            b.Y,
                            c.Y))),
                0,
                height - 1);

        var area =
            Edge(
                a,
                b,
                c.X,
                c.Y);

        if (
            Math.Abs(
                area) <
            0.00001f)
        {
            return;
        }

        for (
            var y = minY;
            y <= maxY;
            y++)
        {
            for (
                var x = minX;
                x <= maxX;
                x++)
            {
                var px =
                    x +
                    0.5f;

                var py =
                    y +
                    0.5f;

                var w0 =
                    Edge(
                        b,
                        c,
                        px,
                        py) /
                    area;

                var w1 =
                    Edge(
                        c,
                        a,
                        px,
                        py) /
                    area;

                var w2 =
                    1.0f -
                    w0 -
                    w1;

                if (
                    w0 <
                        -0.0001f ||
                    w1 <
                        -0.0001f ||
                    w2 <
                        -0.0001f)
                {
                    continue;
                }

                var z =
                    a.Z *
                        w0 +
                    b.Z *
                        w1 +
                    c.Z *
                        w2;

                var index =
                    y *
                    width +
                    x;

                if (
                    z >=
                    depth[index])
                {
                    continue;
                }

                depth[index] =
                    z;

                pixels[index] =
                    color;
            }
        }
    }

    private static float Edge(
        Vector3 a,
        Vector3 b,
        float x,
        float y) =>
        (
            x -
            a.X
        ) *
        (
            b.Y -
            a.Y
        ) -
        (
            y -
            a.Y
        ) *
        (
            b.X -
            a.X
        );

    private static uint AverageColor(
        Vector4 a,
        Vector4 b,
        Vector4 c)
    {
        var color =
            (
                a +
                b +
                c
            ) /
            3.0f;

        static byte Byte(
            float value) =>
            (byte)Math.Clamp(
                (int)MathF.Round(
                    value *
                    255.0f),
                0,
                255);

        return PackColor(
            Byte(color.X),
            Byte(color.Y),
            Byte(color.Z));
    }

    private static uint PackColor(
        byte r,
        byte g,
        byte b) =>
        (uint)(
            b |
            g << 8 |
            r << 16 |
            0xFF << 24);

    private static byte[] EncodeBmp(
        int width,
        int height,
        uint[] pixels)
    {
        var stride =
            width *
            4;

        var pixelBytes =
            stride *
            height;

        var data =
            new byte[
                54 +
                pixelBytes];

        data[0] =
            (byte)'B';

        data[1] =
            (byte)'M';

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    2,
                    4),
                data.Length);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    10,
                    4),
                54);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    14,
                    4),
                40);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    18,
                    4),
                width);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    22,
                    4),
                height);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                data.AsSpan(
                    26,
                    2),
                1);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                data.AsSpan(
                    28,
                    2),
                32);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                data.AsSpan(
                    34,
                    4),
                pixelBytes);

        for (
            var y = 0;
            y < height;
            y++)
        {
            var sourceY =
                height -
                1 -
                y;

            var destination =
                54 +
                y *
                stride;

            for (
                var x = 0;
                x < width;
                x++)
            {
                var color =
                    pixels[
                        sourceY *
                        width +
                        x];

                var offset =
                    destination +
                    x *
                    4;

                data[offset] =
                    (byte)(
                        color &
                        0xFF);

                data[offset + 1] =
                    (byte)(
                        (
                            color >>
                            8
                        ) &
                        0xFF);

                data[offset + 2] =
                    (byte)(
                        (
                            color >>
                            16
                        ) &
                        0xFF);

                data[offset + 3] =
                    0xFF;
            }
        }

        return data;
    }
}
