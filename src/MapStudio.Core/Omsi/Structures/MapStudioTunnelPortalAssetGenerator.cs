using System.Globalization;
using System.Numerics;
using System.Text;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioTunnelPortalSpec(
    string Name,
    double RoadWidthMeters,
    double ClearHeightMeters = 4.5,
    double FrameThicknessMeters = 0.45,
    double DepthMeters = 1.2,
    double LeftWingHeightMeters = 2.5,
    double RightWingHeightMeters = 2.5,
    double LeftWingDepthMeters = 2.5,
    double RightWingDepthMeters = 2.5)
{
    public MapStudioTunnelPortalSpec Normalize() =>
        this with
        {
            Name = string.IsNullOrWhiteSpace(Name)
                ? "Tunnel Portal"
                : Name.Trim(),
            RoadWidthMeters = Math.Clamp(
                double.IsFinite(RoadWidthMeters) ? RoadWidthMeters : 7.0,
                3.0,
                30.0),
            ClearHeightMeters = Math.Clamp(
                double.IsFinite(ClearHeightMeters) ? ClearHeightMeters : 4.5,
                3.5,
                8.0),
            FrameThicknessMeters = Math.Clamp(
                double.IsFinite(FrameThicknessMeters) ? FrameThicknessMeters : 0.45,
                0.2,
                1.5),
            DepthMeters = Math.Clamp(
                double.IsFinite(DepthMeters) ? DepthMeters : 1.2,
                0.4,
                4.0),
            LeftWingHeightMeters = Math.Clamp(
                double.IsFinite(LeftWingHeightMeters) ? LeftWingHeightMeters : 2.5,
                1.5,
                7.0),
            RightWingHeightMeters = Math.Clamp(
                double.IsFinite(RightWingHeightMeters) ? RightWingHeightMeters : 2.5,
                1.5,
                7.0),
            LeftWingDepthMeters = Math.Clamp(
                double.IsFinite(LeftWingDepthMeters) ? LeftWingDepthMeters : 2.5,
                1.5,
                8.0),
            RightWingDepthMeters = Math.Clamp(
                double.IsFinite(RightWingDepthMeters) ? RightWingDepthMeters : 2.5,
                1.5,
                8.0)
        };
}

public sealed record MapStudioTunnelPortalAssetResult(
    string ObjectDirectory,
    string SceneryObjectPath,
    string MeshPath,
    string TexturePath,
    string? BackupDirectory);

public sealed class MapStudioTunnelPortalAssetGenerator
{
    public const string RootFolderName =
        MapStudioBridgePierAssetGenerator.RootFolderName;

    private const string AssetVersion = "1.1.0";

    public async Task<MapStudioTunnelPortalAssetResult> GenerateAsync(
        string omsiRoot,
        MapStudioTunnelPortalSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentNullException.ThrowIfNull(spec);

        var normalized = spec.Normalize();
        var root = Path.GetFullPath(omsiRoot);
        var sceneryRoot = Path.Combine(
            root,
            "Sceneryobjects",
            RootFolderName);
        Directory.CreateDirectory(sceneryRoot);

        var assetName = SanitizeName(normalized.Name);
        var objectDirectory = Path.Combine(sceneryRoot, assetName);
        var scoPath = Path.Combine(objectDirectory, assetName + ".sco");
        var meshPath = Path.Combine(objectDirectory, "model", "portal.o3d");
        var texturePath = Path.Combine(
            objectDirectory,
            "Texture",
            "ms_tunnel_concrete.bmp");
        var manifestPath = Path.Combine(
            objectDirectory,
            "mapstudio-tunnel-portal.txt");
        var manifest = BuildManifest(normalized);

        if (File.Exists(scoPath) &&
            File.Exists(meshPath) &&
            File.Exists(texturePath) &&
            File.Exists(manifestPath) &&
            string.Equals(
                await File.ReadAllTextAsync(
                    manifestPath,
                    cancellationToken).ConfigureAwait(false),
                manifest,
                StringComparison.Ordinal))
        {
            return new MapStudioTunnelPortalAssetResult(
                objectDirectory,
                scoPath,
                meshPath,
                texturePath,
                null);
        }

        string? backupDirectory = null;

        if (Directory.Exists(objectDirectory) &&
            Directory.EnumerateFileSystemEntries(objectDirectory).Any())
        {
            backupDirectory = Path.Combine(
                root,
                ".mapstudio",
                "backups",
                "road-structures",
                assetName + "-" +
                DateTime.UtcNow.ToString(
                    "yyyyMMdd-HHmmss",
                    CultureInfo.InvariantCulture));

            CopyDirectory(objectDirectory, backupDirectory);
        }

        var temporaryDirectory =
            objectDirectory + ".tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var modelDirectory = Path.Combine(temporaryDirectory, "model");
            var textureDirectory = Path.Combine(temporaryDirectory, "Texture");
            Directory.CreateDirectory(modelDirectory);
            Directory.CreateDirectory(textureDirectory);

            await MapStudioGeneratedTextureFactory.EnsureBmpAsync(
                textureDirectory,
                "ms_tunnel_concrete.bmp",
                128,
                128,
                static (x, y) =>
                {
                    var joint = x % 32 <= 1 || y % 24 <= 1;
                    var grain = Math.Abs((x * 29 ^ y * 53 ^ x * y) % 17) - 8;
                    var value = Math.Clamp(
                        128 + grain + (joint ? -16 : 0),
                        84,
                        164);

                    return new MapStudioGeneratedRgb(
                        (byte)value,
                        (byte)value,
                        (byte)Math.Min(255, value + 3));
                },
                cancellationToken).ConfigureAwait(false);

            await MapStudioGeneratedTextureFactory.EnsureBmpAsync(
                textureDirectory,
                "ms_tunnel_light.bmp",
                64,
                64,
                static (x, y) =>
                {
                    var edge =
                        x < 3 ||
                        y < 3 ||
                        x > 60 ||
                        y > 60;
                    var value =
                        edge
                            ? 160
                            : 246;

                    return new MapStudioGeneratedRgb(
                        (byte)value,
                        (byte)Math.Min(255, value + 5),
                        (byte)Math.Min(255, value + 9));
                },
                cancellationToken).ConfigureAwait(false);

            await MapStudioGeneratedTextureFactory.EnsureBmpAsync(
                textureDirectory,
                "ms_tunnel_drain.bmp",
                64,
                64,
                static (x, y) =>
                {
                    var groove =
                        x % 8 < 2;
                    var value =
                        groove
                            ? 42
                            : 74;

                    return new MapStudioGeneratedRgb(
                        (byte)value,
                        (byte)value,
                        (byte)Math.Min(255, value + 3));
                },
                cancellationToken).ConfigureAwait(false);

            await new OmsiO3dGeometryWriter().WriteAsync(
                Path.Combine(modelDirectory, "portal.o3d"),
                BuildGeometry(normalized),
                cancellationToken).ConfigureAwait(false);

            await File.WriteAllTextAsync(
                Path.Combine(temporaryDirectory, assetName + ".sco"),
                BuildSceneryObject(normalized),
                Encoding.ASCII,
                cancellationToken).ConfigureAwait(false);

            await File.WriteAllTextAsync(
                Path.Combine(
                    temporaryDirectory,
                    "mapstudio-tunnel-portal.txt"),
                manifest,
                Encoding.UTF8,
                cancellationToken).ConfigureAwait(false);

            if (Directory.Exists(objectDirectory))
            {
                Directory.Delete(objectDirectory, recursive: true);
            }

            Directory.Move(temporaryDirectory, objectDirectory);
            temporaryDirectory = string.Empty;

            return new MapStudioTunnelPortalAssetResult(
                objectDirectory,
                Path.Combine(objectDirectory, assetName + ".sco"),
                Path.Combine(objectDirectory, "model", "portal.o3d"),
                Path.Combine(
                    objectDirectory,
                    "Texture",
                    "ms_tunnel_concrete.bmp"),
                backupDirectory);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryDirectory) &&
                Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    public OmsiO3dGeometry BuildGeometry(
        MapStudioTunnelPortalSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var normalized = spec.Normalize();
        var clearWidth = (float)normalized.RoadWidthMeters + 1.0f;
        var clearHeight = (float)normalized.ClearHeightMeters;
        var thickness = (float)normalized.FrameThicknessMeters;
        var depth = (float)normalized.DepthMeters;
        var outerWidth = clearWidth + thickness * 2.0f;
        var topHeight = thickness * 1.25f;

        var builder = new GeometryBuilder();

        builder.AddBox(
            new Vector3(
                -(clearWidth + thickness) / 2.0f,
                clearHeight / 2.0f,
                0),
            new Vector3(
                thickness,
                clearHeight,
                depth));

        builder.AddBox(
            new Vector3(
                (clearWidth + thickness) / 2.0f,
                clearHeight / 2.0f,
                0),
            new Vector3(
                thickness,
                clearHeight,
                depth));

        builder.AddBox(
            new Vector3(
                0,
                clearHeight + topHeight / 2.0f,
                0),
            new Vector3(
                outerWidth,
                topHeight,
                depth));

        var leftWingDepth =
            Math.Max(
                depth,
                (float)normalized.LeftWingDepthMeters);
        var rightWingDepth =
            Math.Max(
                depth,
                (float)normalized.RightWingDepthMeters);
        var roofApronDepth =
            Math.Max(
                leftWingDepth,
                rightWingDepth);
        var leftWingHeight =
            (float)normalized.LeftWingHeightMeters;
        var rightWingHeight =
            (float)normalized.RightWingHeightMeters;
        var wingWidth =
            thickness * 1.75f;

        builder.AddBox(
            new Vector3(
                -outerWidth / 2.0f - wingWidth / 2.0f,
                leftWingHeight / 2.0f,
                leftWingDepth * 0.35f),
            new Vector3(
                wingWidth,
                leftWingHeight,
                leftWingDepth));

        builder.AddBox(
            new Vector3(
                outerWidth / 2.0f + wingWidth / 2.0f,
                rightWingHeight / 2.0f,
                rightWingDepth * 0.35f),
            new Vector3(
                wingWidth,
                rightWingHeight,
                rightWingDepth));

        builder.AddBox(
            new Vector3(
                0,
                clearHeight +
                    topHeight +
                    thickness * 0.35f,
                roofApronDepth * 0.32f),
            new Vector3(
                outerWidth +
                    wingWidth * 2.0f,
                thickness * 0.70f,
                roofApronDepth));

        var drainageDepth =
            Math.Max(
                roofApronDepth,
                depth);

        var drainageWidth =
            Math.Clamp(
                clearWidth * 0.035f,
                0.18f,
                0.32f);

        var drainageOffset =
            clearWidth / 2.0f -
            drainageWidth / 2.0f;

        builder.AddBox(
            new Vector3(
                -drainageOffset,
                0.035f,
                drainageDepth * 0.32f),
            new Vector3(
                drainageWidth,
                0.07f,
                drainageDepth),
            2);

        builder.AddBox(
            new Vector3(
                drainageOffset,
                0.035f,
                drainageDepth * 0.32f),
            new Vector3(
                drainageWidth,
                0.07f,
                drainageDepth),
            2);

        var lightBarLength =
            Math.Clamp(
                clearWidth * 0.32f,
                1.8f,
                4.2f);

        var lightBarDepth =
            Math.Clamp(
                roofApronDepth * 0.34f,
                0.8f,
                2.4f);

        var lightBarY =
            clearHeight -
            0.18f;

        builder.AddBox(
            new Vector3(
                -clearWidth * 0.22f,
                lightBarY,
                lightBarDepth * 0.35f),
            new Vector3(
                lightBarLength * 0.42f,
                0.08f,
                lightBarDepth),
            1);

        builder.AddBox(
            new Vector3(
                clearWidth * 0.22f,
                lightBarY,
                lightBarDepth * 0.35f),
            new Vector3(
                lightBarLength * 0.42f,
                0.08f,
                lightBarDepth),
            1);

        AddSlopeCap(
            -1.0f,
            leftWingHeight,
            leftWingDepth);

        AddSlopeCap(
            1.0f,
            rightWingHeight,
            rightWingDepth);

        void AddSlopeCap(
            float side,
            float wingHeight,
            float wingDepth)
        {
            var shoulderWidth =
                Math.Max(
                    0.60f,
                    thickness * 1.50f);

            var centerX =
                side *
                (
                    outerWidth / 2.0f +
                    wingWidth +
                    shoulderWidth / 2.0f
                );

            builder.AddBox(
                new Vector3(
                    centerX,
                    Math.Max(
                        thickness / 2.0f,
                        wingHeight -
                            thickness / 2.0f),
                    wingDepth * 0.45f),
                new Vector3(
                    shoulderWidth,
                    thickness,
                    wingDepth * 0.85f));
        }

        return builder.Build();
    }

    private static string BuildSceneryObject(
        MapStudioTunnelPortalSpec spec)
    {
        var builder = new StringBuilder();
        builder.AppendLine("[friendlyname]");
        builder.AppendLine(spec.Name);
        builder.AppendLine("[groups]");
        builder.AppendLine("2");
        builder.AppendLine("MapStudio");
        builder.AppendLine("Road Structures");
        builder.AppendLine("[mesh]");
        builder.AppendLine("portal.o3d");

        return builder.ToString()
            .Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    private static string BuildManifest(
        MapStudioTunnelPortalSpec spec) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"OMSI Map Studio Tunnel Portal\nVersion={AssetVersion}\nName={spec.Name}\nRoadWidth={spec.RoadWidthMeters:0.00}\nClearHeight={spec.ClearHeightMeters:0.00}\nFrameThickness={spec.FrameThicknessMeters:0.00}\nDepth={spec.DepthMeters:0.00}\nLeftWingHeight={spec.LeftWingHeightMeters:0.00}\nRightWingHeight={spec.RightWingHeightMeters:0.00}\nLeftWingDepth={spec.LeftWingDepthMeters:0.00}\nRightWingDepth={spec.RightWingDepthMeters:0.00}\n");

    private static string SanitizeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(
            value.Select(character =>
                invalid.Contains(character) || char.IsControl(character)
                    ? '_'
                    : character)
                .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(cleaned)
            ? "Tunnel_Portal"
            : cleaned;
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(
            source,
            "*",
            SearchOption.AllDirectories))
        {
            var target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private sealed class GeometryBuilder
    {
        private readonly List<float> _positions = [];
        private readonly List<float> _normals = [];
        private readonly List<float> _uvs = [];
        private readonly List<uint> _indices = [];
        private readonly List<ushort> _materials = [];

        public void AddBox(
            Vector3 center,
            Vector3 size,
            ushort materialIndex = 0)
        {
            var half = size * 0.5f;
            var p000 = center + new Vector3(-half.X, -half.Y, -half.Z);
            var p100 = center + new Vector3(half.X, -half.Y, -half.Z);
            var p110 = center + new Vector3(half.X, half.Y, -half.Z);
            var p010 = center + new Vector3(-half.X, half.Y, -half.Z);
            var p001 = center + new Vector3(-half.X, -half.Y, half.Z);
            var p101 = center + new Vector3(half.X, -half.Y, half.Z);
            var p111 = center + new Vector3(half.X, half.Y, half.Z);
            var p011 = center + new Vector3(-half.X, half.Y, half.Z);

            AddQuad(p000, p010, p110, p100, -Vector3.UnitZ, materialIndex);
            AddQuad(p101, p111, p011, p001, Vector3.UnitZ, materialIndex);
            AddQuad(p001, p011, p010, p000, -Vector3.UnitX, materialIndex);
            AddQuad(p100, p110, p111, p101, Vector3.UnitX, materialIndex);
            AddQuad(p010, p011, p111, p110, Vector3.UnitY, materialIndex);
            AddQuad(p001, p000, p100, p101, -Vector3.UnitY, materialIndex);
        }

        public OmsiO3dGeometry Build() =>
            new(
                true,
                null,
                _positions.ToArray(),
                _normals.ToArray(),
                _uvs.ToArray(),
                _indices.ToArray(),
                _materials.ToArray(),
                [
                    new OmsiO3dMaterial(
                        0.58f,
                        0.58f,
                        0.57f,
                        1.0f,
                        0.05f,
                        0.05f,
                        0.05f,
                        0,
                        0,
                        0,
                        10,
                        "ms_tunnel_concrete.bmp"),
                    new OmsiO3dMaterial(
                        0.96f,
                        0.96f,
                        0.90f,
                        1.0f,
                        0.10f,
                        0.10f,
                        0.08f,
                        0.82f,
                        0.84f,
                        0.76f,
                        4,
                        "ms_tunnel_light.bmp"),
                    new OmsiO3dMaterial(
                        0.22f,
                        0.22f,
                        0.23f,
                        1.0f,
                        0.02f,
                        0.02f,
                        0.02f,
                        0,
                        0,
                        0,
                        4,
                        "ms_tunnel_drain.bmp")
                ]);

        private void AddQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal,
            ushort materialIndex)
        {
            var start = checked((uint)(_positions.Count / 3));
            AddVertex(a, normal, 0, 1);
            AddVertex(b, normal, 0, 0);
            AddVertex(c, normal, 1, 0);
            AddVertex(d, normal, 1, 1);

            _indices.AddRange(
                [
                    start,
                    start + 1,
                    start + 2,
                    start,
                    start + 2,
                    start + 3
                ]);

            _materials.Add(materialIndex);
            _materials.Add(materialIndex);
        }

        private void AddVertex(
            Vector3 position,
            Vector3 normal,
            float u,
            float v)
        {
            _positions.Add(position.X);
            _positions.Add(position.Y);
            _positions.Add(position.Z);
            _normals.Add(normal.X);
            _normals.Add(normal.Y);
            _normals.Add(normal.Z);
            _uvs.Add(u);
            _uvs.Add(v);
        }
    }
}
