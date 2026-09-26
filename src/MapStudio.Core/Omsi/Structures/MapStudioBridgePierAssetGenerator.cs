using System.Globalization;
using System.Numerics;
using System.Text;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioBridgePierSpec(
    string Name,
    double HeightMeters,
    double ColumnWidthMeters = 1.8,
    double ColumnDepthMeters = 1.4,
    double CapWidthMeters = 7.0)
{
    public MapStudioBridgePierSpec Normalize()
    {
        var columnWidth =
            Math.Clamp(
                double.IsFinite(ColumnWidthMeters)
                    ? ColumnWidthMeters
                    : 1.8,
                0.8,
                8.0);

        return this with
        {
            Name =
                string.IsNullOrWhiteSpace(Name)
                    ? "Bridge Pier"
                    : Name.Trim(),
            HeightMeters =
                Math.Clamp(
                    double.IsFinite(HeightMeters)
                        ? HeightMeters
                        : 4.0,
                    1.0,
                    80.0),
            ColumnWidthMeters =
                columnWidth,
            ColumnDepthMeters =
                Math.Clamp(
                    double.IsFinite(ColumnDepthMeters)
                        ? ColumnDepthMeters
                        : 1.4,
                    0.8,
                    8.0),
            CapWidthMeters =
                Math.Clamp(
                    double.IsFinite(CapWidthMeters)
                        ? CapWidthMeters
                        : 7.0,
                    Math.Max(2.5, columnWidth),
                    30.0)
        };
    }
}

public sealed record MapStudioBridgePierAssetResult(
    string ObjectDirectory,
    string SceneryObjectPath,
    string MeshPath,
    string TexturePath,
    string? BackupDirectory);

public sealed class MapStudioBridgePierAssetGenerator
{
    public const string RootFolderName =
        "MapStudio_RoadStructures";

    private const string AssetVersion =
        "1.0.0";

    public async Task<MapStudioBridgePierAssetResult>
        GenerateAsync(
            string omsiRoot,
            MapStudioBridgePierSpec spec,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);
        ArgumentNullException.ThrowIfNull(spec);

        var normalized =
            spec.Normalize();
        var root =
            Path.GetFullPath(omsiRoot);
        var sceneryRoot =
            Path.Combine(
                root,
                "Sceneryobjects",
                RootFolderName);
        Directory.CreateDirectory(
            sceneryRoot);

        var assetName =
            SanitizeName(normalized.Name);
        var objectDirectory =
            Path.Combine(
                sceneryRoot,
                assetName);
        var scoPath =
            Path.Combine(
                objectDirectory,
                assetName + ".sco");
        var meshPath =
            Path.Combine(
                objectDirectory,
                "model",
                "pier.o3d");
        var texturePath =
            Path.Combine(
                objectDirectory,
                "Texture",
                "ms_bridge_concrete.bmp");
        var manifestPath =
            Path.Combine(
                objectDirectory,
                "mapstudio-bridge-pier.txt");
        var manifest =
            BuildManifest(normalized);

        if (
            File.Exists(scoPath) &&
            File.Exists(meshPath) &&
            File.Exists(texturePath) &&
            File.Exists(manifestPath) &&
            string.Equals(
                await File.ReadAllTextAsync(
                    manifestPath,
                    cancellationToken)
                    .ConfigureAwait(false),
                manifest,
                StringComparison.Ordinal))
        {
            return new MapStudioBridgePierAssetResult(
                objectDirectory,
                scoPath,
                meshPath,
                texturePath,
                null);
        }

        string? backupDirectory =
            null;

        if (
            Directory.Exists(objectDirectory) &&
            Directory
                .EnumerateFileSystemEntries(
                    objectDirectory)
                .Any())
        {
            backupDirectory =
                Path.Combine(
                    root,
                    ".mapstudio",
                    "backups",
                    "road-structures",
                    assetName + "-" +
                    DateTime.UtcNow.ToString(
                        "yyyyMMdd-HHmmss",
                        CultureInfo.InvariantCulture));

            CopyDirectory(
                objectDirectory,
                backupDirectory);
        }

        var temporaryDirectory =
            objectDirectory +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        Directory.CreateDirectory(
            temporaryDirectory);

        try
        {
            var modelDirectory =
                Path.Combine(
                    temporaryDirectory,
                    "model");
            var textureDirectory =
                Path.Combine(
                    temporaryDirectory,
                    "Texture");

            Directory.CreateDirectory(
                modelDirectory);
            Directory.CreateDirectory(
                textureDirectory);

            await MapStudioGeneratedTextureFactory
                .EnsureBmpAsync(
                    textureDirectory,
                    "ms_bridge_concrete.bmp",
                    128,
                    128,
                    static (x, y) =>
                    {
                        var seam =
                            x % 32 <= 1 ||
                            y % 32 <= 1;
                        var grain =
                            Math.Abs(
                                (
                                    x * 37 ^
                                    y * 71 ^
                                    x * y * 3
                                ) % 15) -
                            7;
                        var value =
                            Math.Clamp(
                                154 +
                                grain +
                                (seam ? -12 : 0),
                                112,
                                186);

                        return new MapStudioGeneratedRgb(
                            (byte)value,
                            (byte)value,
                            (byte)Math.Min(
                                255,
                                value + 2));
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            await new OmsiO3dGeometryWriter()
                .WriteAsync(
                    Path.Combine(
                        modelDirectory,
                        "pier.o3d"),
                    BuildGeometry(normalized),
                    cancellationToken)
                .ConfigureAwait(false);

            await File.WriteAllTextAsync(
                    Path.Combine(
                        temporaryDirectory,
                        assetName + ".sco"),
                    BuildSceneryObject(
                        normalized),
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            await File.WriteAllTextAsync(
                    Path.Combine(
                        temporaryDirectory,
                        "mapstudio-bridge-pier.txt"),
                    manifest,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            if (
                Directory.Exists(
                    objectDirectory))
            {
                Directory.Delete(
                    objectDirectory,
                    recursive: true);
            }

            Directory.Move(
                temporaryDirectory,
                objectDirectory);
            temporaryDirectory =
                string.Empty;

            return new MapStudioBridgePierAssetResult(
                objectDirectory,
                Path.Combine(
                    objectDirectory,
                    assetName + ".sco"),
                Path.Combine(
                    objectDirectory,
                    "model",
                    "pier.o3d"),
                Path.Combine(
                    objectDirectory,
                    "Texture",
                    "ms_bridge_concrete.bmp"),
                backupDirectory);
        }
        finally
        {
            if (
                !string.IsNullOrWhiteSpace(
                    temporaryDirectory) &&
                Directory.Exists(
                    temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }

    public OmsiO3dGeometry BuildGeometry(
        MapStudioBridgePierSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var normalized =
            spec.Normalize();
        var height =
            (float)normalized.HeightMeters;
        var columnWidth =
            (float)normalized.ColumnWidthMeters;
        var columnDepth =
            (float)normalized.ColumnDepthMeters;
        var capWidth =
            (float)normalized.CapWidthMeters;

        var footingHeight =
            Math.Clamp(
                height * 0.08f,
                0.22f,
                0.45f);
        var capHeight =
            Math.Clamp(
                height * 0.10f,
                0.32f,
                0.65f);
        var shaftHeight =
            Math.Max(
                0.25f,
                height -
                    footingHeight -
                    capHeight);

        var builder =
            new GeometryBuilder();

        builder.AddBox(
            new Vector3(
                0,
                footingHeight / 2.0f,
                0),
            new Vector3(
                columnWidth * 1.45f,
                footingHeight,
                columnDepth * 1.55f));

        builder.AddBox(
            new Vector3(
                0,
                footingHeight +
                    shaftHeight / 2.0f,
                0),
            new Vector3(
                columnWidth,
                shaftHeight,
                columnDepth));

        builder.AddBox(
            new Vector3(
                0,
                height -
                    capHeight / 2.0f,
                0),
            new Vector3(
                capWidth,
                capHeight,
                columnDepth * 1.45f));

        return builder.Build();
    }

    private static string BuildSceneryObject(
        MapStudioBridgePierSpec spec)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine("[friendlyname]");
        builder.AppendLine(spec.Name);
        builder.AppendLine("[groups]");
        builder.AppendLine("2");
        builder.AppendLine("MapStudio");
        builder.AppendLine("Road Structures");
        builder.AppendLine("[mesh]");
        builder.AppendLine("pier.o3d");

        return builder
            .ToString()
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);
    }

    private static string BuildManifest(
        MapStudioBridgePierSpec spec) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"OMSI Map Studio Bridge Pier\nVersion={AssetVersion}\nName={spec.Name}\nHeight={spec.HeightMeters:0.00}\nColumnWidth={spec.ColumnWidthMeters:0.00}\nColumnDepth={spec.ColumnDepthMeters:0.00}\nCapWidth={spec.CapWidthMeters:0.00}\n");

    private static string SanitizeName(
        string value)
    {
        var invalid =
            Path.GetInvalidFileNameChars();
        var cleaned =
            new string(
                value
                    .Select(
                        character =>
                            invalid.Contains(character) ||
                            char.IsControl(character)
                                ? '_'
                                : character)
                    .ToArray())
                .Trim();

        return string.IsNullOrWhiteSpace(cleaned)
            ? "Bridge_Pier"
            : cleaned;
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        Directory.CreateDirectory(
            destination);

        foreach (
            var directory in
                Directory.EnumerateDirectories(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    Path.GetRelativePath(
                        source,
                        directory)));
        }

        foreach (
            var file in
                Directory.EnumerateFiles(
                    source,
                    "*",
                    SearchOption.AllDirectories))
        {
            var target =
                Path.Combine(
                    destination,
                    Path.GetRelativePath(
                        source,
                        file));
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    target)!);
            File.Copy(
                file,
                target,
                overwrite: true);
        }
    }

    private sealed class GeometryBuilder
    {
        private readonly List<float>
            _positions = [];
        private readonly List<float>
            _normals = [];
        private readonly List<float>
            _uvs = [];
        private readonly List<uint>
            _indices = [];
        private readonly List<ushort>
            _materials = [];

        public void AddBox(
            Vector3 center,
            Vector3 size)
        {
            var half =
                size * 0.5f;
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
                -Vector3.UnitZ);
            AddQuad(
                p101,
                p111,
                p011,
                p001,
                Vector3.UnitZ);
            AddQuad(
                p001,
                p011,
                p010,
                p000,
                -Vector3.UnitX);
            AddQuad(
                p100,
                p110,
                p111,
                p101,
                Vector3.UnitX);
            AddQuad(
                p010,
                p011,
                p111,
                p110,
                Vector3.UnitY);
            AddQuad(
                p001,
                p000,
                p100,
                p101,
                -Vector3.UnitY);
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
                        0.68f,
                        0.68f,
                        0.67f,
                        1.0f,
                        0.08f,
                        0.08f,
                        0.08f,
                        0,
                        0,
                        0,
                        12,
                        "ms_bridge_concrete.bmp")
                ]);

        private void AddQuad(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal)
        {
            var start =
                checked(
                    (uint)(
                        _positions.Count / 3));

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

            _materials.Add(0);
            _materials.Add(0);
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
