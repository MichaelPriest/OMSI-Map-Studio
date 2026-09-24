using System.Numerics;
using MapStudio.Core.Omsi.Maps;
using MapStudio.Renderer.Viewport;

namespace MapStudio.Renderer.Scene;

public sealed class NativeReferenceOverlayGeometryBuilder
{
    public NativeReferenceOverlayGeometry
        Build(
            NativeSceneSnapshot scene,
            NativeReferenceOverlayDefinition overlay,
            int segments = 24)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            overlay);

        _ = segments;

        if (
            string.IsNullOrWhiteSpace(
                overlay.ImagePath) ||
            overlay.Width <= 0 ||
            overlay.Height <= 0 ||
            !double.IsFinite(
                overlay.MetersPerPixel) ||
            overlay.MetersPerPixel <= 0 ||
            !double.IsFinite(
                overlay.AnchorWorldX) ||
            !double.IsFinite(
                overlay.AnchorWorldZ))
        {
            throw new InvalidDataException(
                "invalidReferenceOverlay");
        }

        var widthMeters =
            overlay.Width *
            overlay.MetersPerPixel;

        var heightMeters =
            overlay.Height *
            overlay.MetersPerPixel;

        var minimumOverlayX =
            overlay.AnchorWorldX -
            widthMeters *
            0.5;

        var maximumOverlayX =
            overlay.AnchorWorldX +
            widthMeters *
            0.5;

        var minimumOverlayZ =
            overlay.AnchorWorldZ -
            heightMeters *
            0.5;

        var maximumOverlayZ =
            overlay.AnchorWorldZ +
            heightMeters *
            0.5;

        var opacity =
            Math.Clamp(
                overlay.Opacity,
                0.05f,
                1.0f);

        var color =
            new Vector4(
                1,
                1,
                1,
                opacity);

        var vertices =
            new List<
                NativeMapVertex>(
                    32_768);

        foreach (
            var tile in
                scene.Tiles)
        {
            var terrain =
                tile.Content.Terrain;

            if (
                terrain is null ||
                terrain.CellCount <= 0)
            {
                continue;
            }

            var cellCount =
                terrain.CellCount;

            var sampleCount =
                cellCount +
                1;

            if (
                terrain.Heights.Count !=
                sampleCount *
                sampleCount)
            {
                continue;
            }

            var originX =
                tile.Reference.X *
                OmsiTileGrid.TileSize;

            var originZ =
                tile.Reference.Y *
                OmsiTileGrid.TileSize;

            var tileMaximumX =
                originX +
                OmsiTileGrid.TileSize;

            var tileMaximumZ =
                originZ +
                OmsiTileGrid.TileSize;

            if (
                maximumOverlayX <=
                    originX ||
                minimumOverlayX >=
                    tileMaximumX ||
                maximumOverlayZ <=
                    originZ ||
                minimumOverlayZ >=
                    tileMaximumZ)
            {
                continue;
            }

            var spacing =
                OmsiTileGrid.TileSize /
                cellCount;

            var minimumX =
                Math.Max(
                    minimumOverlayX,
                    originX);

            var maximumX =
                Math.Min(
                    maximumOverlayX,
                    tileMaximumX);

            var minimumZ =
                Math.Max(
                    minimumOverlayZ,
                    originZ);

            var maximumZ =
                Math.Min(
                    maximumOverlayZ,
                    tileMaximumZ);

            var firstColumn =
                Math.Clamp(
                    (int)Math.Floor(
                        (
                            minimumX -
                            originX
                        ) /
                        spacing),
                    0,
                    cellCount -
                        1);

            var lastColumn =
                Math.Clamp(
                    (int)Math.Ceiling(
                        (
                            maximumX -
                            originX
                        ) /
                        spacing) -
                    1,
                    0,
                    cellCount -
                        1);

            var firstRow =
                Math.Clamp(
                    (int)Math.Floor(
                        (
                            minimumZ -
                            originZ
                        ) /
                        spacing),
                    0,
                    cellCount -
                        1);

            var lastRow =
                Math.Clamp(
                    (int)Math.Ceiling(
                        (
                            maximumZ -
                            originZ
                        ) /
                        spacing) -
                    1,
                    0,
                    cellCount -
                        1);

            for (
                var row = firstRow;
                row <= lastRow;
                row++)
            {
                for (
                    var column =
                        firstColumn;
                    column <=
                        lastColumn;
                    column++)
                {
                    var topLeft =
                        row *
                        sampleCount +
                        column;

                    var topRight =
                        topLeft +
                        1;

                    var bottomLeft =
                        topLeft +
                        sampleCount;

                    var bottomRight =
                        bottomLeft +
                        1;

                    var h00 =
                        terrain.Heights[
                            topLeft];

                    var h10 =
                        terrain.Heights[
                            topRight];

                    var h01 =
                        terrain.Heights[
                            bottomLeft];

                    var h11 =
                        terrain.Heights[
                            bottomRight];

                    if (
                        !float.IsFinite(
                            h00) ||
                        !float.IsFinite(
                            h10) ||
                        !float.IsFinite(
                            h01) ||
                        !float.IsFinite(
                            h11))
                    {
                        continue;
                    }

                    var x0 =
                        originX +
                        column *
                        spacing;

                    var x1 =
                        x0 +
                        spacing;

                    var z0 =
                        originZ +
                        row *
                        spacing;

                    var z1 =
                        z0 +
                        spacing;

                    var uv00 =
                        CreateReferenceUv(
                            x0,
                            z0,
                            minimumOverlayX,
                            minimumOverlayZ,
                            widthMeters,
                            heightMeters);

                    var uv10 =
                        CreateReferenceUv(
                            x1,
                            z0,
                            minimumOverlayX,
                            minimumOverlayZ,
                            widthMeters,
                            heightMeters);

                    var uv01 =
                        CreateReferenceUv(
                            x0,
                            z1,
                            minimumOverlayX,
                            minimumOverlayZ,
                            widthMeters,
                            heightMeters);

                    var uv11 =
                        CreateReferenceUv(
                            x1,
                            z1,
                            minimumOverlayX,
                            minimumOverlayZ,
                            widthMeters,
                            heightMeters);

                    // Keep the overlay physically coplanar with the exact
                    // terrain triangles. The D3D11 reference rasterizer
                    // applies a visual depth bias, so no world-space lift
                    // is required and elevated/deformed terrain stays exact.
                    AppendTriangle(
                        vertices,
                        x0,
                        h00,
                        z0,
                        uv00,
                        x1,
                        h11,
                        z1,
                        uv11,
                        x1,
                        h10,
                        z0,
                        uv10,
                        color);

                    AppendTriangle(
                        vertices,
                        x0,
                        h00,
                        z0,
                        uv00,
                        x0,
                        h01,
                        z1,
                        uv01,
                        x1,
                        h11,
                        z1,
                        uv11,
                        color);
                }
            }
        }

        return new NativeReferenceOverlayGeometry(
            vertices.ToArray(),
            overlay.ImagePath);
    }

    private static Vector2
        CreateReferenceUv(
            double worldX,
            double worldZ,
            double minimumOverlayX,
            double minimumOverlayZ,
            double widthMeters,
            double heightMeters) =>
        new(
            (float)(
                (
                    worldX -
                    minimumOverlayX
                ) /
                widthMeters),
            (float)(
                (
                    worldZ -
                    minimumOverlayZ
                ) /
                heightMeters));

    private static void AppendTriangle(
        List<NativeMapVertex> output,
        double x0,
        float height0,
        double z0,
        Vector2 uv0,
        double x1,
        float height1,
        double z1,
        Vector2 uv1,
        double x2,
        float height2,
        double z2,
        Vector2 uv2,
        Vector4 color)
    {
        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x0,
                    height0,
                    (float)z0),
                color,
                uv0));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x1,
                    height1,
                    (float)z1),
                color,
                uv1));

        output.Add(
            new NativeMapVertex(
                new Vector3(
                    (float)x2,
                    height2,
                    (float)z2),
                color,
                uv2));
    }
}
