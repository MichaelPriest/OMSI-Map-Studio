using System.Numerics;
using System.Text;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Generation.Scene;
using MapStudio.Core.Omsi.Models;
using MapStudio.Core.Omsi.Textures;

namespace MapStudio.Core.Omsi.Structures;

public sealed record MapStudioInfrastructureAssetResult(
    string ObjectDirectory,
    string SceneryObjectPath,
    string MeshPath,
    MapStudioRoadPoint WorldCenter,
    string? BackupDirectory);

public sealed class MapStudioInfrastructureAssetGenerator
{
    public const string RootFolderName =
        "MapStudio_Infrastructure";

    public async Task<MapStudioInfrastructureAssetResult>
        GenerateAsync(
            string omsiRoot,
            MapStudioProjectedInfrastructureFeature feature,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(omsiRoot);
        ArgumentNullException.ThrowIfNull(feature);

        var root =
            Path.GetFullPath(omsiRoot);

        var assetName =
            SanitizeName(
                feature.Kind +
                "_" +
                feature.Id);

        var objectDirectory =
            Path.Combine(
                root,
                "Sceneryobjects",
                RootFolderName,
                assetName);

        string? backupDirectory = null;

        if (
            Directory.Exists(objectDirectory) &&
            Directory.EnumerateFileSystemEntries(objectDirectory).Any())
        {
            backupDirectory =
                Path.Combine(
                    root,
                    ".mapstudio",
                    "backups",
                    "infrastructure",
                    assetName +
                    "-" +
                    DateTime.UtcNow.ToString(
                        "yyyyMMdd-HHmmss",
                        System.Globalization.CultureInfo.InvariantCulture));

            CopyDirectory(
                objectDirectory,
                backupDirectory);
        }

        var temporaryDirectory =
            objectDirectory +
            ".tmp-" +
            Guid.NewGuid().ToString("N");

        Directory.CreateDirectory(temporaryDirectory);

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

            Directory.CreateDirectory(modelDirectory);
            Directory.CreateDirectory(textureDirectory);

            await EnsureGeneratedTexturesAsync(
                    textureDirectory,
                    cancellationToken)
                .ConfigureAwait(false);

            var geometry =
                BuildGeometry(feature);

            var meshPath =
                Path.Combine(
                    modelDirectory,
                    "infrastructure.o3d");

            await new OmsiO3dGeometryWriter()
                .WriteAsync(
                    meshPath,
                    geometry,
                    cancellationToken)
                .ConfigureAwait(false);

            var sceneryObjectPath =
                Path.Combine(
                    temporaryDirectory,
                    assetName + ".sco");

            await File.WriteAllTextAsync(
                    sceneryObjectPath,
                    BuildSceneryObject(feature),
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            await File.WriteAllTextAsync(
                    Path.Combine(
                        temporaryDirectory,
                        "mapstudio-osm-infrastructure.txt"),
                    BuildManifest(feature),
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);

            if (Directory.Exists(objectDirectory))
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

            return new MapStudioInfrastructureAssetResult(
                objectDirectory,
                Path.Combine(
                    objectDirectory,
                    assetName + ".sco"),
                Path.Combine(
                    objectDirectory,
                    "model",
                    "infrastructure.o3d"),
                feature.Center,
                backupDirectory);
        }
        finally
        {
            if (
                !string.IsNullOrWhiteSpace(temporaryDirectory) &&
                Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(
                    temporaryDirectory,
                    recursive: true);
            }
        }
    }

    public OmsiO3dGeometry BuildGeometry(
        MapStudioProjectedInfrastructureFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        if (feature.Points.Count < 2)
        {
            throw new InvalidDataException(
                "infrastructureGeometryTooSmall");
        }

        var positions = new List<float>();
        var normals = new List<float>();
        var uvs = new List<float>();
        var indices = new List<uint>();
        var triangleMaterials = new List<ushort>();

        switch (feature.Kind)
        {
            case MapStudioOsmInfrastructureKind.Wall:
            case MapStudioOsmInfrastructureKind.Fence:
            case MapStudioOsmInfrastructureKind.GuardRail:
                AddBarrierGeometry(
                    feature,
                    positions,
                    normals,
                    uvs,
                    indices,
                    triangleMaterials);
                break;

            case MapStudioOsmInfrastructureKind.Sidewalk:
            case MapStudioOsmInfrastructureKind.Driveway:
                if (feature.IsArea && feature.Points.Count >= 3)
                {
                    AddAreaSurface(
                        feature,
                        positions,
                        normals,
                        uvs,
                        indices,
                        triangleMaterials);
                }
                else
                {
                    AddRibbonSurface(
                        feature,
                        positions,
                        normals,
                        uvs,
                        indices,
                        triangleMaterials);
                }

                break;

            case MapStudioOsmInfrastructureKind.Parking:
                if (!feature.IsArea || feature.Points.Count < 3)
                {
                    throw new InvalidDataException(
                        "parkingAreaRequired");
                }

                AddAreaSurface(
                    feature,
                    positions,
                    normals,
                    uvs,
                    indices,
                    triangleMaterials);
                break;

            default:
                throw new InvalidDataException(
                    "infrastructureKindUnsupported");
        }

        if (indices.Count == 0)
        {
            throw new InvalidDataException(
                "infrastructureGeometryEmpty");
        }

        return new OmsiO3dGeometry(
            true,
            null,
            positions.ToArray(),
            normals.ToArray(),
            uvs.ToArray(),
            indices.ToArray(),
            triangleMaterials.ToArray(),
            [
                new OmsiO3dMaterial(
                    0.66f,
                    0.64f,
                    0.60f,
                    1,
                    0.03f,
                    0.03f,
                    0.03f,
                    0,
                    0,
                    0,
                    6,
                    "ms_infra_concrete.bmp"),
                new OmsiO3dMaterial(
                    0.62f,
                    0.64f,
                    0.66f,
                    1,
                    0.18f,
                    0.18f,
                    0.18f,
                    0,
                    0,
                    0,
                    18,
                    "ms_infra_metal.bmp"),
                new OmsiO3dMaterial(
                    0.52f,
                    0.52f,
                    0.50f,
                    1,
                    0.02f,
                    0.02f,
                    0.02f,
                    0,
                    0,
                    0,
                    4,
                    "ms_infra_surface.bmp")
            ]);
    }

    private static void AddBarrierGeometry(
        MapStudioProjectedInfrastructureFeature feature,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var height =
            feature.Kind switch
            {
                MapStudioOsmInfrastructureKind.Wall => 1.80f,
                MapStudioOsmInfrastructureKind.Fence => 1.45f,
                MapStudioOsmInfrastructureKind.GuardRail => 0.82f,
                _ => 1.0f
            };

        var thickness =
            feature.Kind switch
            {
                MapStudioOsmInfrastructureKind.Wall => 0.24f,
                MapStudioOsmInfrastructureKind.Fence => 0.07f,
                MapStudioOsmInfrastructureKind.GuardRail => 0.14f,
                _ => 0.10f
            };

        var material =
            feature.Kind ==
                MapStudioOsmInfrastructureKind.Wall
                ? (ushort)0
                : (ushort)1;

        for (var index = 1; index < feature.Points.Count; index++)
        {
            AddSegmentPrism(
                ToLocal(feature.Points[index - 1], feature.Center),
                ToLocal(feature.Points[index], feature.Center),
                height,
                thickness,
                material,
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials);
        }

        if (
            feature.IsArea &&
            feature.Points.Count > 2)
        {
            AddSegmentPrism(
                ToLocal(feature.Points[^1], feature.Center),
                ToLocal(feature.Points[0], feature.Center),
                height,
                thickness,
                material,
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials);
        }
    }

    private static void AddRibbonSurface(
        MapStudioProjectedInfrastructureFeature feature,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var width =
            (float)(
                feature.WidthMeters ??
                (
                    feature.Kind ==
                    MapStudioOsmInfrastructureKind.Sidewalk
                        ? 1.80
                        : 3.20
                ));

        width =
            Math.Clamp(
                width,
                0.50f,
                20.0f);

        var half =
            width / 2.0f;

        for (var index = 1; index < feature.Points.Count; index++)
        {
            var a =
                ToLocal(
                    feature.Points[index - 1],
                    feature.Center);

            var b =
                ToLocal(
                    feature.Points[index],
                    feature.Center);

            var dx =
                b.X - a.X;

            var dz =
                b.Z - a.Z;

            var length =
                MathF.Sqrt(
                    dx * dx +
                    dz * dz);

            if (length < 0.05f)
            {
                continue;
            }

            var nx =
                -dz /
                length *
                half;

            var nz =
                dx /
                length *
                half;

            AddQuad(
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials,
                new Vector3(a.X + nx, 0.025f, a.Z + nz),
                new Vector3(b.X + nx, 0.025f, b.Z + nz),
                new Vector3(b.X - nx, 0.025f, b.Z - nz),
                new Vector3(a.X - nx, 0.025f, a.Z - nz),
                2);
        }
    }

    private static void AddAreaSurface(
        MapStudioProjectedInfrastructureFeature feature,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var points =
            feature.Points
                .Select(
                    point =>
                        ToLocal(
                            point,
                            feature.Center) +
                        new Vector3(
                            0,
                            0.025f,
                            0))
                .ToList();

        var triangles =
            Triangulate(points);

        if (triangles.Count == 0)
        {
            throw new InvalidDataException(
                "infrastructureAreaTriangulationFailed");
        }

        foreach (var triangle in triangles)
        {
            AddTriangle(
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials,
                points[triangle.A],
                points[triangle.B],
                points[triangle.C],
                2);
        }
    }

    private static void AddSegmentPrism(
        Vector3 a,
        Vector3 b,
        float height,
        float thickness,
        ushort material,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials)
    {
        var dx = b.X - a.X;
        var dz = b.Z - a.Z;

        var length =
            MathF.Sqrt(
                dx * dx +
                dz * dz);

        if (length < 0.05f)
        {
            return;
        }

        var half =
            thickness /
            2.0f;

        var nx =
            -dz /
            length *
            half;

        var nz =
            dx /
            length *
            half;

        var aLeft =
            new Vector3(
                a.X + nx,
                0,
                a.Z + nz);

        var aRight =
            new Vector3(
                a.X - nx,
                0,
                a.Z - nz);

        var bLeft =
            new Vector3(
                b.X + nx,
                0,
                b.Z + nz);

        var bRight =
            new Vector3(
                b.X - nx,
                0,
                b.Z - nz);

        var up =
            new Vector3(
                0,
                height,
                0);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            aLeft,
            bLeft,
            bLeft + up,
            aLeft + up,
            material);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            bRight,
            aRight,
            aRight + up,
            bRight + up,
            material);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            aLeft + up,
            bLeft + up,
            bRight + up,
            aRight + up,
            material);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            aRight,
            aLeft,
            aLeft + up,
            aRight + up,
            material);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            bLeft,
            bRight,
            bRight + up,
            bLeft + up,
            material);
    }

    private static List<Triangle> Triangulate(
        IReadOnlyList<Vector3> source)
    {
        if (source.Count < 3)
        {
            return [];
        }

        var points =
            source.ToArray();

        var order =
            Enumerable.Range(
                    0,
                    points.Length)
                .ToList();

        if (SignedArea(points) < 0)
        {
            order.Reverse();
        }

        var result =
            new List<Triangle>();

        var guard =
            points.Length *
            points.Length;

        while (
            order.Count > 3 &&
            guard-- > 0)
        {
            var clipped = false;

            for (var cursor = 0; cursor < order.Count; cursor++)
            {
                var previous =
                    order[
                        (cursor - 1 + order.Count) %
                        order.Count];

                var current =
                    order[cursor];

                var next =
                    order[
                        (cursor + 1) %
                        order.Count];

                if (
                    Cross(
                        points[previous],
                        points[current],
                        points[next]) <= 0.000001f)
                {
                    continue;
                }

                var containsPoint = false;

                foreach (var candidate in order)
                {
                    if (
                        candidate == previous ||
                        candidate == current ||
                        candidate == next)
                    {
                        continue;
                    }

                    if (
                        PointInsideTriangle(
                            points[candidate],
                            points[previous],
                            points[current],
                            points[next]))
                    {
                        containsPoint = true;
                        break;
                    }
                }

                if (containsPoint)
                {
                    continue;
                }

                result.Add(
                    new Triangle(
                        previous,
                        current,
                        next));

                order.RemoveAt(cursor);
                clipped = true;
                break;
            }

            if (!clipped)
            {
                return [];
            }
        }

        if (order.Count == 3)
        {
            result.Add(
                new Triangle(
                    order[0],
                    order[1],
                    order[2]));
        }

        return result;
    }

    private static double SignedArea(
        IReadOnlyList<Vector3> points)
    {
        double area = 0;

        for (var index = 0; index < points.Count; index++)
        {
            var next =
                (index + 1) %
                points.Count;

            area +=
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;
        }

        return area / 2.0;
    }

    private static float Cross(
        Vector3 a,
        Vector3 b,
        Vector3 c) =>
        (b.X - a.X) *
            (c.Z - b.Z) -
        (b.Z - a.Z) *
            (c.X - b.X);

    private static bool PointInsideTriangle(
        Vector3 p,
        Vector3 a,
        Vector3 b,
        Vector3 c)
    {
        var d1 = Sign(p, a, b);
        var d2 = Sign(p, b, c);
        var d3 = Sign(p, c, a);

        var hasNegative =
            d1 < -0.000001f ||
            d2 < -0.000001f ||
            d3 < -0.000001f;

        var hasPositive =
            d1 > 0.000001f ||
            d2 > 0.000001f ||
            d3 > 0.000001f;

        return !(
            hasNegative &&
            hasPositive);
    }

    private static float Sign(
        Vector3 p1,
        Vector3 p2,
        Vector3 p3) =>
        (p1.X - p3.X) *
            (p2.Z - p3.Z) -
        (p2.X - p3.X) *
            (p1.Z - p3.Z);

    private static void AddTriangle(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> materials,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        ushort material)
    {
        var cross =
            Vector3.Cross(
                b - a,
                c - a);

        if (cross.LengthSquared() < 0.000001f)
        {
            return;
        }

        var normal =
            Vector3.Normalize(cross);

        if (normal.Y < 0)
        {
            (b, c) = (c, b);
            normal = -normal;
        }

        var start =
            checked(
                (uint)(
                    positions.Count /
                    3));

        AddVertex(
            positions,
            normals,
            uvs,
            a,
            normal,
            0,
            0);

        AddVertex(
            positions,
            normals,
            uvs,
            b,
            normal,
            1,
            0);

        AddVertex(
            positions,
            normals,
            uvs,
            c,
            normal,
            0,
            1);

        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);

        materials.Add(material);
    }

    private static void AddQuad(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> materials,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        ushort material)
    {
        var cross =
            Vector3.Cross(
                b - a,
                c - a);

        if (cross.LengthSquared() < 0.000001f)
        {
            return;
        }

        var normal =
            Vector3.Normalize(cross);

        var start =
            checked(
                (uint)(
                    positions.Count /
                    3));

        AddVertex(
            positions,
            normals,
            uvs,
            a,
            normal,
            0,
            1);

        AddVertex(
            positions,
            normals,
            uvs,
            b,
            normal,
            1,
            1);

        AddVertex(
            positions,
            normals,
            uvs,
            c,
            normal,
            1,
            0);

        AddVertex(
            positions,
            normals,
            uvs,
            d,
            normal,
            0,
            0);

        indices.Add(start);
        indices.Add(start + 1);
        indices.Add(start + 2);

        indices.Add(start);
        indices.Add(start + 2);
        indices.Add(start + 3);

        materials.Add(material);
        materials.Add(material);
    }

    private static void AddVertex(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        Vector3 point,
        Vector3 normal,
        float u,
        float v)
    {
        positions.Add(point.X);
        positions.Add(point.Y);
        positions.Add(point.Z);

        normals.Add(normal.X);
        normals.Add(normal.Y);
        normals.Add(normal.Z);

        uvs.Add(u);
        uvs.Add(v);
    }

    private static Vector3 ToLocal(
        MapStudioRoadPoint point,
        MapStudioRoadPoint center) =>
        new(
            (float)(point.X - center.X),
            0,
            (float)(point.Z - center.Z));

    private static async Task EnsureGeneratedTexturesAsync(
        string textureDirectory,
        CancellationToken cancellationToken)
    {
        await MapStudioGeneratedTextureFactory
            .EnsureBmpAsync(
                textureDirectory,
                "ms_infra_concrete.bmp",
                64,
                64,
                static (x, y) =>
                {
                    var noise =
                        (x * 17 + y * 31) %
                        13;

                    var value =
                        (byte)(
                            152 +
                            noise);

                    return new MapStudioGeneratedRgb(
                        value,
                        value,
                        (byte)Math.Max(
                            0,
                            value - 4));
                },
                cancellationToken)
            .ConfigureAwait(false);

        await MapStudioGeneratedTextureFactory
            .EnsureBmpAsync(
                textureDirectory,
                "ms_infra_metal.bmp",
                64,
                64,
                static (x, y) =>
                {
                    var stripe =
                        y % 16 <= 2;

                    var value =
                        (byte)(
                            stripe
                                ? 188
                                : 142);

                    return new MapStudioGeneratedRgb(
                        value,
                        value,
                        value);
                },
                cancellationToken)
            .ConfigureAwait(false);

        await MapStudioGeneratedTextureFactory
            .EnsureBmpAsync(
                textureDirectory,
                "ms_infra_surface.bmp",
                64,
                64,
                static (x, y) =>
                {
                    var seam =
                        x % 16 == 0 ||
                        y % 16 == 0;

                    return seam
                        ? new MapStudioGeneratedRgb(
                            105,
                            104,
                            101)
                        : new MapStudioGeneratedRgb(
                            128,
                            127,
                            123);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildSceneryObject(
        MapStudioProjectedInfrastructureFeature feature) =>
        "[friendlyname]\r\n" +
        (
            string.IsNullOrWhiteSpace(feature.Name)
                ? feature.Kind + " " + feature.Id
                : feature.Name
        ) +
        "\r\n[groups]\r\n2\r\nMapStudio\r\nOSM Infrastructure\r\n" +
        "[mesh]\r\ninfrastructure.o3d\r\n";

    private static string BuildManifest(
        MapStudioProjectedInfrastructureFeature feature) =>
        $"OMSI Map Studio OSM Infrastructure\n" +
        $"Id={feature.Id}\n" +
        $"Kind={feature.Kind}\n" +
        $"Name={feature.Name}\n" +
        $"Surface={feature.Surface}\n" +
        $"Width={feature.WidthMeters:0.###}\n" +
        $"Area={feature.IsArea}\n" +
        $"Points={feature.Points.Count}\n" +
        $"CenterX={feature.Center.X:0.###}\n" +
        $"CenterZ={feature.Center.Z:0.###}\n";

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

        cleaned =
            string.Join(
                "_",
                cleaned.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(cleaned)
            ? "OSM_Infrastructure"
            : cleaned;
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        Directory.CreateDirectory(destination);

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
                Path.GetDirectoryName(target)!);

            File.Copy(
                file,
                target,
                overwrite: true);
        }
    }

    private readonly record struct Triangle(
        int A,
        int B,
        int C);
}
