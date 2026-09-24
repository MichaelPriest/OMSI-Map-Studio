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

        var safeSegments =
            Math.Clamp(
                segments,
                1,
                64);

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
                    safeSegments *
                    safeSegments *
                    6);

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

            var sampleCount =
                terrain.CellCount +
                1;

            if (
                terrain.Heights.Count !=
                sampleCount *
                sampleCount)
            {
                continue;
            }

            var minimumTileX =
                tile.Reference.X *
                OmsiTileGrid.TileSize;

            var maximumTileX =
                (
                    tile.Reference.X +
                    1
                ) *
                OmsiTileGrid.TileSize;

            var minimumTileZ =
                tile.Reference.Y *
                OmsiTileGrid.TileSize;

            var maximumTileZ =
                (
                    tile.Reference.Y +
                    1
                ) *
                OmsiTileGrid.TileSize;

            var minimumX =
                Math.Max(
                    minimumOverlayX,
                    minimumTileX);

            var maximumX =
                Math.Min(
                    maximumOverlayX,
                    maximumTileX);

            var minimumZ =
                Math.Max(
                    minimumOverlayZ,
                    minimumTileZ);

            var maximumZ =
                Math.Min(
                    maximumOverlayZ,
                    maximumTileZ);

            var spanX =
                maximumX -
                minimumX;

            var spanZ =
                maximumZ -
                minimumZ;

            if (
                spanX <= 0.0001 ||
                spanZ <= 0.0001)
            {
                continue;
            }

            var segmentsX =
                Math.Clamp(
                    (int)Math.Ceiling(
                        safeSegments *
                        spanX /
                        widthMeters),
                    1,
                    safeSegments);

            var segmentsZ =
                Math.Clamp(
                    (int)Math.Ceiling(
                        safeSegments *
                        spanZ /
                        heightMeters),
                    1,
                    safeSegments);

            var columns =
                segmentsX +
                1;

            var rows =
                segmentsZ +
                1;

            var points =
                new Vector3[
                    columns *
                    rows];

            var uvs =
                new Vector2[
                    points.Length];

            for (
                var row = 0;
                row < rows;
                row++)
            {
                var fractionZ =
                    (double)row /
                    segmentsZ;

                var worldZ =
                    minimumZ +
                    spanZ *
                    fractionZ;

                for (
                    var column = 0;
                    column < columns;
                    column++)
                {
                    var fractionX =
                        (double)column /
                        segmentsX;

                    var worldX =
                        minimumX +
                        spanX *
                        fractionX;

                    var localX =
                        worldX -
                        minimumTileX;

                    var localZ =
                        worldZ -
                        minimumTileZ;

                    var height =
                        NativeTerrainSampler
                            .GetHeightAtLocalPoint(
                                tile,
                                localX,
                                localZ) +
                        0.08;

                    var u =
                        (
                            worldX -
                            minimumOverlayX
                        ) /
                        widthMeters;

                    var v =
                        (
                            worldZ -
                            minimumOverlayZ
                        ) /
                        heightMeters;

                    var index =
                        row *
                        columns +
                        column;

                    points[index] =
                        new Vector3(
                            (float)worldX,
                            (float)height,
                            (float)worldZ);

                    uvs[index] =
                        new Vector2(
                            (float)Math.Clamp(
                                u,
                                0.0,
                                1.0),
                            (float)(
                                1.0 -
                                Math.Clamp(
                                    v,
                                    0.0,
                                    1.0)
                            ));
                }
            }

            for (
                var row = 0;
                row < segmentsZ;
                row++)
            {
                for (
                    var column = 0;
                    column < segmentsX;
                    column++)
                {
                    var topLeft =
                        row *
                        columns +
                        column;

                    var topRight =
                        topLeft +
                        1;

                    var bottomLeft =
                        topLeft +
                        columns;

                    var bottomRight =
                        bottomLeft +
                        1;

                    AppendTriangle(
                        vertices,
                        points,
                        uvs,
                        color,
                        topLeft,
                        bottomRight,
                        topRight);

                    AppendTriangle(
                        vertices,
                        points,
                        uvs,
                        color,
                        topLeft,
                        bottomLeft,
                        bottomRight);
                }
            }
        }

        return new NativeReferenceOverlayGeometry(
            vertices.ToArray(),
            overlay.ImagePath);
    }

    private static void AppendTriangle(
        List<NativeMapVertex> output,
        IReadOnlyList<Vector3> points,
        IReadOnlyList<Vector2> uvs,
        Vector4 color,
        int a,
        int b,
        int c)
    {
        output.Add(
            new NativeMapVertex(
                points[a],
                color,
                uvs[a]));

        output.Add(
            new NativeMapVertex(
                points[b],
                color,
                uvs[b]));

        output.Add(
            new NativeMapVertex(
                points[c],
                color,
                uvs[c]));
    }
}
