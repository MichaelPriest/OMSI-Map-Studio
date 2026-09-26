using System.Numerics;
using System.Text;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Core.Omsi.Props;

public sealed class MapStudioStarterPropGenerator
{
    public const string RootFolderName =
        "MapStudio_Props";

    public async Task<bool> EnsureAsync(
        string contentRoot,
        CancellationToken cancellationToken =
            default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentRoot);

        var root =
            Path.GetFullPath(
                contentRoot);

        var propRoot =
            Path.Combine(
                root,
                "Sceneryobjects",
                RootFolderName);

        Directory.CreateDirectory(
            propRoot);

        var created =
            false;

        created =
            await EnsureAssetAsync(
                propRoot,
                "Starter_Lamp",
                "starter_lamp",
                "Map Studio Starter Lamp",
                "Street Furniture",
                BuildLampGeometry(),
                includeLampLight:
                    true,
                cancellationToken) ||
            created;

        created =
            await EnsureAssetAsync(
                propRoot,
                "Starter_Bench",
                "starter_bench",
                "Map Studio Starter Bench",
                "Street Furniture",
                BuildBenchGeometry(),
                includeLampLight:
                    false,
                cancellationToken) ||
            created;

        created =
            await EnsureAssetAsync(
                propRoot,
                "Starter_BusStop",
                "starter_busstop",
                "Map Studio Starter Bus Stop",
                "Transit",
                BuildBusStopGeometry(),
                includeLampLight:
                    false,
                cancellationToken) ||
            created;

        created =
            await EnsureAssetAsync(
                propRoot,
                "Starter_UtilityBox",
                "starter_utilitybox",
                "Map Studio Starter Utility Box",
                "Utilities",
                BuildUtilityBoxGeometry(),
                includeLampLight:
                    false,
                cancellationToken) ||
            created;

        return created;
    }

    private static async Task<bool>
        EnsureAssetAsync(
            string propRoot,
            string folderName,
            string fileStem,
            string friendlyName,
            string groupName,
            OmsiO3dGeometry geometry,
            bool includeLampLight,
            CancellationToken cancellationToken)
    {
        var directory =
            Path.Combine(
                propRoot,
                folderName);

        var modelDirectory =
            Path.Combine(
                directory,
                "model");

        var textureDirectory =
            Path.Combine(
                directory,
                "Texture");

        Directory.CreateDirectory(
            modelDirectory);

        Directory.CreateDirectory(
            textureDirectory);

        var texturesCreated =
            await EnsureTexturesAsync(
                    textureDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

        var meshPath =
            Path.Combine(
                modelDirectory,
                "prop.o3d");

        var scoPath =
            Path.Combine(
                directory,
                fileStem +
                ".sco");

        var created =
            false;

        if (
            !File.Exists(
                meshPath) ||
            texturesCreated)
        {
            await new OmsiO3dGeometryWriter()
                .WriteAsync(
                    meshPath,
                    geometry,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        if (!File.Exists(
                scoPath))
        {
            var source =
                BuildSceneryObject(
                    friendlyName,
                    groupName,
                    includeLampLight);

            await File.WriteAllTextAsync(
                    scoPath,
                    source,
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        var manifestPath =
            Path.Combine(
                directory,
                "mapstudio-prop.txt");

        if (!File.Exists(
                manifestPath))
        {
            await File.WriteAllTextAsync(
                    manifestPath,
                    "OMSI Map Studio Prop\n" +
                    "Generated=procedural\n" +
                    "License=Original Map Studio content\n" +
                    "Name=" +
                    friendlyName +
                    "\n",
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        return created;
    }

    private static string BuildSceneryObject(
        string friendlyName,
        string groupName,
        bool includeLampLight)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "[friendlyname]");
        builder.AppendLine(
            friendlyName);

        builder.AppendLine(
            "[groups]");
        builder.AppendLine(
            "2");
        builder.AppendLine(
            "Map Studio");
        builder.AppendLine(
            groupName);

        builder.AppendLine(
            "[mesh]");
        builder.AppendLine(
            @"model\prop.o3d");

        if (includeLampLight)
        {
            // OMSI light_enh position is X/Y/Z with Z as height.
            builder.AppendLine(
                "[light_enh]");
            builder.AppendLine(
                "0");
            builder.AppendLine(
                "0.75");
            builder.AppendLine(
                "7.15");
            builder.AppendLine(
                "255");
            builder.AppendLine(
                "225");
            builder.AppendLine(
                "170");
            builder.AppendLine(
                "0.45");
            builder.AppendLine(
                "0");
            builder.AppendLine(
                "0");
            builder.AppendLine(
                "0");
            builder.AppendLine(
                "0");
            builder.AppendLine(
                "0");
            builder.AppendLine(
                "0");
        }

        return builder
            .ToString()
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);
    }

    private static OmsiO3dGeometry
        BuildLampGeometry()
    {
        var builder =
            new GeometryBuilder();

        builder.AddBox(
            new Vector3(
                0,
                3.5f,
                0),
            new Vector3(
                0.16f,
                7.0f,
                0.16f),
            0);

        builder.AddBox(
            new Vector3(
                0.38f,
                6.92f,
                0),
            new Vector3(
                0.75f,
                0.12f,
                0.12f),
            0);

        builder.AddBox(
            new Vector3(
                0.78f,
                6.82f,
                0),
            new Vector3(
                0.35f,
                0.20f,
                0.30f),
            1);

        return builder.Build(
            Materials);
    }

    private static OmsiO3dGeometry
        BuildBenchGeometry()
    {
        var builder =
            new GeometryBuilder();

        builder.AddBox(
            new Vector3(
                0,
                0.58f,
                0),
            new Vector3(
                2.0f,
                0.16f,
                0.55f),
            2);

        builder.AddBox(
            new Vector3(
                0,
                1.05f,
                0.22f),
            new Vector3(
                2.0f,
                0.65f,
                0.12f),
            2);

        foreach (
            var x in
                new[]
                {
                    -0.72f,
                    0.72f
                })
        {
            builder.AddBox(
                new Vector3(
                    x,
                    0.28f,
                    0),
                new Vector3(
                    0.12f,
                    0.56f,
                    0.12f),
                0);
        }

        return builder.Build(
            Materials);
    }

    private static OmsiO3dGeometry
        BuildBusStopGeometry()
    {
        var builder =
            new GeometryBuilder();

        builder.AddBox(
            new Vector3(
                0,
                1.35f,
                0),
            new Vector3(
                0.10f,
                2.70f,
                0.10f),
            0);

        builder.AddBox(
            new Vector3(
                0,
                2.55f,
                0),
            new Vector3(
                0.70f,
                0.50f,
                0.10f),
            3);

        builder.AddBox(
            new Vector3(
                0,
                2.55f,
                -0.065f),
            new Vector3(
                0.54f,
                0.34f,
                0.035f),
            1);

        return builder.Build(
            Materials);
    }

    private static OmsiO3dGeometry
        BuildUtilityBoxGeometry()
    {
        var builder =
            new GeometryBuilder();

        builder.AddBox(
            new Vector3(
                0,
                0.72f,
                0),
            new Vector3(
                0.82f,
                1.44f,
                0.48f),
            4);

        builder.AddBox(
            new Vector3(
                0,
                1.47f,
                0),
            new Vector3(
                0.92f,
                0.08f,
                0.56f),
            0);

        builder.AddBox(
            new Vector3(
                0,
                0.74f,
                -0.255f),
            new Vector3(
                0.60f,
                0.86f,
                0.035f),
            0);

        return builder.Build(
            Materials);
    }

    private static async Task<bool>
        EnsureTexturesAsync(
            string textureDirectory,
            CancellationToken cancellationToken)
    {
        var created =
            false;

        created =
            await MapStudioGeneratedTextureFactory
                .EnsureBmpAsync(
                    textureDirectory,
                    "ms_prop_metal.bmp",
                    64,
                    64,
                    static (x, y) =>
                    {
                        var noise =
                            (
                                x * 17 +
                                y * 29
                            ) %
                            15;

                        var value =
                            (byte)(
                                48 +
                                noise);

                        return new MapStudioGeneratedRgb(
                            value,
                            (byte)(
                                value +
                                3),
                            (byte)(
                                value +
                                6));
                    },
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await MapStudioGeneratedTextureFactory
                .EnsureBmpAsync(
                    textureDirectory,
                    "ms_prop_lamp.bmp",
                    32,
                    32,
                    static (x, y) =>
                    {
                        var glow =
                            Math.Max(
                                0,
                                18 -
                                Math.Abs(
                                    x -
                                    16) -
                                Math.Abs(
                                    y -
                                    16));

                        return new MapStudioGeneratedRgb(
                            (byte)(
                                220 +
                                glow),
                            (byte)(
                                172 +
                                glow *
                                    2),
                            (byte)(
                                72 +
                                glow *
                                    2));
                    },
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await MapStudioGeneratedTextureFactory
                .EnsureBmpAsync(
                    textureDirectory,
                    "ms_prop_wood.bmp",
                    64,
                    64,
                    static (x, y) =>
                    {
                        var grain =
                            (
                                x * 5 +
                                y * 19 +
                                (
                                    y %
                                    8 ==
                                    0
                                        ? 18
                                        : 0
                                )
                            ) %
                            28;

                        return new MapStudioGeneratedRgb(
                            (byte)(
                                104 +
                                grain),
                            (byte)(
                                62 +
                                grain /
                                    2),
                            (byte)(
                                30 +
                                grain /
                                    3));
                    },
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await MapStudioGeneratedTextureFactory
                .EnsureBmpAsync(
                    textureDirectory,
                    "ms_prop_blue.bmp",
                    64,
                    64,
                    static (x, y) =>
                    {
                        var edge =
                            x %
                                16 ==
                            0 ||
                            y %
                                16 ==
                            0;

                        return edge
                            ? new MapStudioGeneratedRgb(
                                18,
                                64,
                                112)
                            : new MapStudioGeneratedRgb(
                                34,
                                112,
                                184);
                    },
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        created =
            await MapStudioGeneratedTextureFactory
                .EnsureBmpAsync(
                    textureDirectory,
                    "ms_prop_utility.bmp",
                    64,
                    64,
                    static (x, y) =>
                    {
                        var noise =
                            (
                                x * 7 +
                                y * 11
                            ) %
                            13;

                        return new MapStudioGeneratedRgb(
                            (byte)(
                                72 +
                                noise),
                            (byte)(
                                92 +
                                noise),
                            (byte)(
                                70 +
                                noise /
                                    2));
                    },
                    cancellationToken)
                .ConfigureAwait(false) ||
            created;

        return created;
    }

    private static readonly
        IReadOnlyList<OmsiO3dMaterial>
        Materials =
        [
            new OmsiO3dMaterial(
                0.18f,
                0.20f,
                0.22f,
                1,
                0.08f,
                0.08f,
                0.08f,
                0,
                0,
                0,
                24,
                "ms_prop_metal.bmp"),
            new OmsiO3dMaterial(
                1.0f,
                0.83f,
                0.46f,
                1,
                0.20f,
                0.15f,
                0.06f,
                0,
                0,
                0,
                16,
                "ms_prop_lamp.bmp"),
            new OmsiO3dMaterial(
                0.43f,
                0.24f,
                0.10f,
                1,
                0.05f,
                0.03f,
                0.02f,
                0,
                0,
                0,
                8,
                "ms_prop_wood.bmp"),
            new OmsiO3dMaterial(
                0.10f,
                0.34f,
                0.68f,
                1,
                0.05f,
                0.09f,
                0.15f,
                0,
                0,
                0,
                20,
                "ms_prop_blue.bmp"),
            new OmsiO3dMaterial(
                0.34f,
                0.43f,
                0.32f,
                1,
                0.06f,
                0.08f,
                0.05f,
                0,
                0,
                0,
                18,
                "ms_prop_utility.bmp")
        ];

    private sealed class GeometryBuilder
    {
        private readonly List<float>
            _positions =
                [];

        private readonly List<float>
            _normals =
                [];

        private readonly List<float>
            _uvs =
                [];

        private readonly List<uint>
            _indices =
                [];

        private readonly List<ushort>
            _materials =
                [];

        public void AddBox(
            Vector3 center,
            Vector3 size,
            ushort material)
        {
            var half =
                size *
                0.5f;

            var p000 =
                center +
                new Vector3(
                    -half.X,
                    -half.Y,
                    -half.Z);

            var p100 =
                center +
                new Vector3(
                    half.X,
                    -half.Y,
                    -half.Z);

            var p110 =
                center +
                new Vector3(
                    half.X,
                    half.Y,
                    -half.Z);

            var p010 =
                center +
                new Vector3(
                    -half.X,
                    half.Y,
                    -half.Z);

            var p001 =
                center +
                new Vector3(
                    -half.X,
                    -half.Y,
                    half.Z);

            var p101 =
                center +
                new Vector3(
                    half.X,
                    -half.Y,
                    half.Z);

            var p111 =
                center +
                new Vector3(
                    half.X,
                    half.Y,
                    half.Z);

            var p011 =
                center +
                new Vector3(
                    -half.X,
                    half.Y,
                    half.Z);

            AddQuad(
                p000,
                p010,
                p110,
                p100,
                -Vector3.UnitZ,
                material);

            AddQuad(
                p101,
                p111,
                p011,
                p001,
                Vector3.UnitZ,
                material);

            AddQuad(
                p001,
                p011,
                p010,
                p000,
                -Vector3.UnitX,
                material);

            AddQuad(
                p100,
                p110,
                p111,
                p101,
                Vector3.UnitX,
                material);

            AddQuad(
                p010,
                p011,
                p111,
                p110,
                Vector3.UnitY,
                material);

            AddQuad(
                p001,
                p000,
                p100,
                p101,
                -Vector3.UnitY,
                material);
        }

        public OmsiO3dGeometry Build(
            IReadOnlyList<
                OmsiO3dMaterial> materials) =>
            new(
                true,
                null,
                _positions.ToArray(),
                _normals.ToArray(),
                _uvs.ToArray(),
                _indices.ToArray(),
                _materials.ToArray(),
                materials);

        private void AddQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal,
            ushort material)
        {
            var start =
                checked(
                    (uint)(
                        _positions.Count /
                        3));

            AddVertex(
                a,
                normal,
                0,
                1);

            AddVertex(
                b,
                normal,
                0,
                0);

            AddVertex(
                c,
                normal,
                1,
                0);

            AddVertex(
                d,
                normal,
                1,
                1);

            _indices.AddRange(
                [
                    start,
                    start + 1,
                    start + 2,
                    start,
                    start + 2,
                    start + 3
                ]);

            _materials.Add(
                material);

            _materials.Add(
                material);
        }

        private void AddVertex(
            Vector3 position,
            Vector3 normal,
            float u,
            float v)
        {
            _positions.Add(
                position.X);
            _positions.Add(
                position.Y);
            _positions.Add(
                position.Z);

            _normals.Add(
                normal.X);
            _normals.Add(
                normal.Y);
            _normals.Add(
                normal.Z);

            _uvs.Add(
                u);
            _uvs.Add(
                v);
        }
    }
}
