using System.Numerics;
using System.Text;
using MapStudio.Core.Omsi.Models;

namespace MapStudio.Core.Omsi.Props;

public sealed class MapStudioStarterTrafficGenerator
{
    public const string RootFolderName =
        "MapStudio_Traffic";

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

        var objectDirectory =
            Path.Combine(
                root,
                "Sceneryobjects",
                RootFolderName,
                "Starter_TrafficLight");

        var modelDirectory =
            Path.Combine(
                objectDirectory,
                "model");

        Directory.CreateDirectory(
            modelDirectory);

        var meshPath =
            Path.Combine(
                modelDirectory,
                "trafficlight.o3d");

        var scoPath =
            Path.Combine(
                objectDirectory,
                "starter_trafficlight.sco");

        var created =
            false;

        if (!File.Exists(
                meshPath))
        {
            await new OmsiO3dGeometryWriter()
                .WriteAsync(
                    meshPath,
                    BuildGeometry(),
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        if (!File.Exists(
                scoPath))
        {
            await File.WriteAllTextAsync(
                    scoPath,
                    BuildSceneryObject(),
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        var manifestPath =
            Path.Combine(
                objectDirectory,
                "mapstudio-traffic.txt");

        if (!File.Exists(
                manifestPath))
        {
            await File.WriteAllTextAsync(
                    manifestPath,
                    "OMSI Map Studio Traffic Light\n" +
                    "Generated=procedural\n" +
                    "License=Original Map Studio content\n" +
                    "EditableTrafficProgram=true\n",
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            created =
                true;
        }

        return created;
    }

    public OmsiO3dGeometry BuildGeometry()
    {
        var builder =
            new GeometryBuilder();

        builder.AddBox(
            new Vector3(
                0,
                1.7f,
                0),
            new Vector3(
                0.12f,
                3.4f,
                0.12f),
            0);

        builder.AddBox(
            new Vector3(
                0,
                3.15f,
                0),
            new Vector3(
                0.52f,
                1.25f,
                0.32f),
            0);

        builder.AddBox(
            new Vector3(
                0,
                3.52f,
                -0.19f),
            new Vector3(
                0.25f,
                0.25f,
                0.08f),
            1);

        builder.AddBox(
            new Vector3(
                0,
                3.15f,
                -0.19f),
            new Vector3(
                0.25f,
                0.25f,
                0.08f),
            2);

        builder.AddBox(
            new Vector3(
                0,
                2.78f,
                -0.19f),
            new Vector3(
                0.25f,
                0.25f,
                0.08f),
            3);

        return builder.Build(
            Materials);
    }

    public string BuildSceneryObject() =>
        string.Join(
            "\r\n",
            [
                "[friendlyname]",
                "Map Studio Starter Traffic Light",
                "",
                "[groups]",
                "2",
                "Map Studio",
                "Traffic",
                "",
                "[trafficlight]",
                "",
                "[mesh]",
                @"model\trafficlight.o3d",
                "",
                "[traffic_lights_group]",
                "60",
                "",
                "[traffic_light]",
                "Main",
                "",
                "[phase]",
                "0",
                "25",
                "",
                "[phase]",
                "6",
                "5",
                "",
                "[phase]",
                "9",
                "25",
                "",
                "[phase]",
                "6",
                "5",
                "",
                "[path]",
                "0",
                "0",
                "0",
                "0",
                "0",
                "10",
                "0",
                "0",
                "0",
                "3",
                "0",
                "0",
                "",
                "[use_traffic_light]",
                "0",
                ""
            ]);

    private static readonly
        IReadOnlyList<OmsiO3dMaterial>
        Materials =
        [
            new OmsiO3dMaterial(
                0.10f,
                0.12f,
                0.13f,
                1,
                0.04f,
                0.04f,
                0.04f,
                0,
                0,
                0,
                16,
                null),
            new OmsiO3dMaterial(
                0.90f,
                0.08f,
                0.05f,
                1,
                0.18f,
                0.02f,
                0.01f,
                0,
                0,
                0,
                12,
                null),
            new OmsiO3dMaterial(
                0.95f,
                0.62f,
                0.06f,
                1,
                0.18f,
                0.10f,
                0.01f,
                0,
                0,
                0,
                12,
                null),
            new OmsiO3dMaterial(
                0.06f,
                0.72f,
                0.16f,
                1,
                0.01f,
                0.16f,
                0.03f,
                0,
                0,
                0,
                12,
                null)
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
