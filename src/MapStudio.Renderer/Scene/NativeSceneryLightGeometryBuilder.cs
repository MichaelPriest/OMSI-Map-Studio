using System.Numerics;

namespace MapStudio.Renderer.Scene;

public sealed record NativeSceneryLightGeometry(
    NativeMapVertex[] Vertices,
    int LightPointCount);

public sealed class NativeSceneryLightGeometryBuilder
{
    public NativeSceneryLightGeometry Build(
        NativeSceneSnapshot scene,
        IReadOnlyDictionary<
            string,
            NativeSceneryAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        ArgumentNullException.ThrowIfNull(
            assets);

        var vertices =
            new List<NativeMapVertex>();

        var count = 0;

        foreach (
            var entity in
                scene.Objects)
        {
            if (
                !assets.TryGetValue(
                    entity.Object
                        .SceneryObjectPath,
                    out var asset) ||
                asset.LightPoints.Count ==
                    0)
            {
                continue;
            }

            var terrainOffset =
                asset.UsesAbsoluteHeight
                    ? 0.0
                    : NativeTerrainSampler
                        .GetHeightAtObject(
                            scene,
                            entity);

            var objectTransform =
                Matrix4x4.CreateFromYawPitchRoll(
                    DegreesToRadians(
                        entity.Object
                            .Rotation),
                    DegreesToRadians(
                        entity.Object
                            .Pitch),
                    DegreesToRadians(
                        entity.Object
                            .Bank)) *
                Matrix4x4.CreateTranslation(
                    entity.WorldX,
                    entity.WorldY +
                        (float)
                            terrainOffset,
                    entity.WorldZ);

            foreach (
                var light in
                    asset.LightPoints)
            {
                if (
                    !light
                        .HasRenderableEnhancedData)
                {
                    continue;
                }

                // light_enh_2 uses X/Y/Z with Z as height.
                // Renderer world space is X/Y-up/Z-map-forward.
                var local =
                    new Vector3(
                        (float)
                            light.PositionX!
                                .Value,
                        (float)
                            light.PositionZ!
                                .Value,
                        (float)
                            light.PositionY!
                                .Value);

                var center =
                    Vector3.Transform(
                        local,
                        objectTransform);

                var size =
                    Math.Clamp(
                        (float)
                            light.Size!
                                .Value *
                        0.5f,
                        0.05f,
                        0.75f);

                var color =
                    new Vector4(
                        NormalizeColor(
                            light.Red!.Value),
                        NormalizeColor(
                            light.Green!.Value),
                        NormalizeColor(
                            light.Blue!.Value),
                        0.88f);

                AppendCube(
                    vertices,
                    center,
                    size,
                    color);

                count++;
            }
        }

        return new NativeSceneryLightGeometry(
            vertices.ToArray(),
            count);
    }

    private static void AppendCube(
        List<NativeMapVertex> output,
        Vector3 center,
        float halfSize,
        Vector4 color)
    {
        var p =
            new[]
            {
                center + new Vector3(-halfSize, -halfSize, -halfSize),
                center + new Vector3( halfSize, -halfSize, -halfSize),
                center + new Vector3( halfSize,  halfSize, -halfSize),
                center + new Vector3(-halfSize,  halfSize, -halfSize),
                center + new Vector3(-halfSize, -halfSize,  halfSize),
                center + new Vector3( halfSize, -halfSize,  halfSize),
                center + new Vector3( halfSize,  halfSize,  halfSize),
                center + new Vector3(-halfSize,  halfSize,  halfSize)
            };

        int[][] faces =
        [
            [0, 1, 2, 3],
            [5, 4, 7, 6],
            [4, 0, 3, 7],
            [1, 5, 6, 2],
            [3, 2, 6, 7],
            [4, 5, 1, 0]
        ];

        foreach (var face in faces)
        {
            AddTriangle(
                output,
                p[face[0]],
                p[face[1]],
                p[face[2]],
                color);

            AddTriangle(
                output,
                p[face[0]],
                p[face[2]],
                p[face[3]],
                color);
        }
    }

    private static void AddTriangle(
        List<NativeMapVertex> output,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector4 color)
    {
        output.Add(
            new NativeMapVertex(
                a,
                color));

        output.Add(
            new NativeMapVertex(
                b,
                color));

        output.Add(
            new NativeMapVertex(
                c,
                color));
    }

    private static float NormalizeColor(
        double value)
    {
        var normalized =
            value > 1.0
                ? value / 255.0
                : value;

        return (float)
            Math.Clamp(
                normalized,
                0.0,
                1.0);
    }

    private static float DegreesToRadians(
        double value) =>
        (float)(
            value *
            Math.PI /
            180.0);
}
