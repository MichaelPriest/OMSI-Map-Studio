using System.Numerics;
using System.Text;
using MapStudio.Core.Generation.Buildings;
using MapStudio.Core.Generation.Roads;
using MapStudio.Core.Omsi.Models;

namespace MapStudio.Core.Omsi.Buildings;

public sealed record MapStudioFootprintBuildingAssetResult(
    string ObjectDirectory,
    string SceneryObjectPath,
    string MeshPath,
    MapStudioRoadPoint WorldCenter,
    string? BackupDirectory);

public sealed class MapStudioFootprintBuildingAssetGenerator
{
    public async Task<MapStudioFootprintBuildingAssetResult>
        GenerateAsync(
            string omsiRoot,
            MapStudioProjectedBuildingFootprint building,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentNullException.ThrowIfNull(
            building);

        var root =
            Path.GetFullPath(
                omsiRoot);

        var assetName =
            SanitizeName(
                BuildAssetName(
                    building));

        var objectDirectory =
            Path.Combine(
                root,
                "Sceneryobjects",
                MapStudioBuildingAssetGenerator
                    .RootFolderName,
                assetName);

        string? backupDirectory =
            null;

        if (
            Directory.Exists(
                objectDirectory) &&
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
                    "buildings",
                    assetName +
                    "-" +
                    DateTime.UtcNow
                        .ToString(
                            "yyyyMMdd-HHmmss",
                            System.Globalization
                                .CultureInfo
                                .InvariantCulture));

            CopyDirectory(
                objectDirectory,
                backupDirectory);
        }

        var temporaryDirectory =
            objectDirectory +
            ".tmp-" +
            Guid.NewGuid()
                .ToString("N");

        Directory.CreateDirectory(
            temporaryDirectory);

        try
        {
            var modelDirectory =
                Path.Combine(
                    temporaryDirectory,
                    "model");

            Directory.CreateDirectory(
                modelDirectory);

            var geometry =
                BuildGeometry(
                    building);

            var meshPath =
                Path.Combine(
                    modelDirectory,
                    "building.o3d");

            await new OmsiO3dGeometryWriter()
                .WriteAsync(
                    meshPath,
                    geometry,
                    cancellationToken)
                .ConfigureAwait(false);

            var sceneryObjectPath =
                Path.Combine(
                    temporaryDirectory,
                    assetName +
                    ".sco");

            await File.WriteAllTextAsync(
                    sceneryObjectPath,
                    BuildSceneryObject(
                        building),
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            await File.WriteAllTextAsync(
                    Path.Combine(
                        temporaryDirectory,
                        "mapstudio-osm-building.txt"),
                    BuildManifest(
                        building),
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

            return new MapStudioFootprintBuildingAssetResult(
                objectDirectory,
                Path.Combine(
                    objectDirectory,
                    assetName +
                    ".sco"),
                Path.Combine(
                    objectDirectory,
                    "model",
                    "building.o3d"),
                building.Center,
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
        MapStudioProjectedBuildingFootprint building)
    {
        ArgumentNullException.ThrowIfNull(
            building);

        var footprint =
            NormalizeFootprint(
                building.Points,
                building.Center);

        if (footprint.Count < 3)
        {
            throw new InvalidDataException(
                "buildingFootprintTooSmall");
        }

        if (!IsSimplePolygon(footprint))
        {
            throw new InvalidDataException(
                "buildingFootprintSelfIntersecting");
        }

        var triangles =
            Triangulate(
                footprint);

        if (triangles.Count == 0)
        {
            throw new InvalidDataException(
                "buildingFootprintTriangulationFailed");
        }

        var positions =
            new List<float>();

        var normals =
            new List<float>();

        var uvs =
            new List<float>();

        var indices =
            new List<uint>();

        var triangleMaterials =
            new List<ushort>();

        var height =
            (float)Math.Clamp(
                building.WallHeightMeters,
                2.2,
                500);

        var signedArea =
            SignedArea(
                footprint);

        var isCounterClockwise =
            signedArea >
            0;

        for (
            var index = 0;
            index < footprint.Count;
            index++)
        {
            var next =
                (
                    index +
                    1
                ) %
                footprint.Count;

            var a =
                footprint[index];

            var b =
                footprint[next];

            var edge =
                b -
                a;

            var normal =
                isCounterClockwise
                    ? Vector3.Normalize(
                        new Vector3(
                            edge.Z,
                            0,
                            -edge.X))
                    : Vector3.Normalize(
                        new Vector3(
                            -edge.Z,
                            0,
                            edge.X));

            AddQuad(
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials,
                new Vector3(
                    a.X,
                    0,
                    a.Z),
                new Vector3(
                    b.X,
                    0,
                    b.Z),
                new Vector3(
                    b.X,
                    height,
                    b.Z),
                new Vector3(
                    a.X,
                    height,
                    a.Z),
                normal,
                0);
        }

        var shapedRoof =
            footprint.Count ==
                4 &&
            building.RoofHeightMeters >
                0.01 &&
            building.RoofType is
                MapStudioBuildingRoofType.Gable or
                MapStudioBuildingRoofType.Hip or
                MapStudioBuildingRoofType.Shed;

        if (shapedRoof)
        {
            AddQuadrilateralRoof(
                building.RoofType,
                (float)building.RoofHeightMeters,
                footprint,
                height,
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials);
        }

        foreach (
            var triangle in
                triangles)
        {
            var a =
                footprint[
                    triangle.A];

            var b =
                footprint[
                    triangle.B];

            var c =
                footprint[
                    triangle.C];

            if (!isCounterClockwise)
            {
                (
                    b,
                    c
                ) =
                (
                    c,
                    b
                );
            }

            if (!shapedRoof)
            {
                AddTriangle(
                    positions,
                    normals,
                    uvs,
                    indices,
                    triangleMaterials,
                    new Vector3(
                        a.X,
                        height,
                        a.Z),
                    new Vector3(
                        b.X,
                        height,
                        b.Z),
                    new Vector3(
                        c.X,
                        height,
                        c.Z),
                    Vector3.UnitY,
                    1);
            }

            AddTriangle(
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials,
                new Vector3(
                    c.X,
                    0,
                    c.Z),
                new Vector3(
                    b.X,
                    0,
                    b.Z),
                new Vector3(
                    a.X,
                    0,
                    a.Z),
                -Vector3.UnitY,
                0);
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
                    0.72f,
                    0.70f,
                    0.66f,
                    1,
                    0.04f,
                    0.04f,
                    0.04f,
                    0,
                    0,
                    0,
                    8,
                    null),
                new OmsiO3dMaterial(
                    0.30f,
                    0.27f,
                    0.24f,
                    1,
                    0.04f,
                    0.04f,
                    0.04f,
                    0,
                    0,
                    0,
                    8,
                    null)
            ]);
    }

    private static void AddQuadrilateralRoof(
        MapStudioBuildingRoofType roofType,
        float roofHeight,
        IReadOnlyList<Vector3> points,
        float wallHeight,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> materials)
    {
        var p =
            points.ToArray();

        if (
            roofType ==
            MapStudioBuildingRoofType.Hip)
        {
            var center =
                new Vector3(
                    p.Average(
                        item =>
                            item.X),
                    wallHeight +
                        roofHeight,
                    p.Average(
                        item =>
                            item.Z));

            for (
                var index = 0;
                index < 4;
                index++)
            {
                AddAutoNormalTriangle(
                    positions,
                    normals,
                    uvs,
                    indices,
                    materials,
                    new Vector3(
                        p[index].X,
                        wallHeight,
                        p[index].Z),
                    new Vector3(
                        p[(index + 1) % 4].X,
                        wallHeight,
                        p[(index + 1) % 4].Z),
                    center,
                    1,
                    preferUp:
                        true);
            }

            return;
        }

        if (
            roofType ==
            MapStudioBuildingRoofType.Shed)
        {
            var low0 =
                new Vector3(
                    p[0].X,
                    wallHeight,
                    p[0].Z);

            var low1 =
                new Vector3(
                    p[1].X,
                    wallHeight,
                    p[1].Z);

            var high2 =
                new Vector3(
                    p[2].X,
                    wallHeight +
                        roofHeight,
                    p[2].Z);

            var high3 =
                new Vector3(
                    p[3].X,
                    wallHeight +
                        roofHeight,
                    p[3].Z);

            AddAutoNormalQuad(
                positions,
                normals,
                uvs,
                indices,
                materials,
                low0,
                low1,
                high2,
                high3,
                1,
                preferUp:
                    true);

            AddAutoNormalQuad(
                positions,
                normals,
                uvs,
                indices,
                materials,
                new Vector3(
                    p[2].X,
                    wallHeight,
                    p[2].Z),
                new Vector3(
                    p[3].X,
                    wallHeight,
                    p[3].Z),
                high3,
                high2,
                0,
                preferUp:
                    false);

            AddAutoNormalTriangle(
                positions,
                normals,
                uvs,
                indices,
                materials,
                new Vector3(
                    p[1].X,
                    wallHeight,
                    p[1].Z),
                new Vector3(
                    p[2].X,
                    wallHeight,
                    p[2].Z),
                high2,
                0,
                preferUp:
                    false);

            AddAutoNormalTriangle(
                positions,
                normals,
                uvs,
                indices,
                materials,
                new Vector3(
                    p[3].X,
                    wallHeight,
                    p[3].Z),
                new Vector3(
                    p[0].X,
                    wallHeight,
                    p[0].Z),
                high3,
                0,
                preferUp:
                    false);

            return;
        }

        var edge0 =
            Vector3.Distance(
                p[0],
                p[1]);

        var edge1 =
            Vector3.Distance(
                p[1],
                p[2]);

        if (edge1 > edge0)
        {
            p =
            [
                p[1],
                p[2],
                p[3],
                p[0]
            ];
        }

        var ridgeA =
            new Vector3(
                (
                    p[3].X +
                    p[0].X
                ) /
                2.0f,
                wallHeight +
                    roofHeight,
                (
                    p[3].Z +
                    p[0].Z
                ) /
                2.0f);

        var ridgeB =
            new Vector3(
                (
                    p[1].X +
                    p[2].X
                ) /
                2.0f,
                wallHeight +
                    roofHeight,
                (
                    p[1].Z +
                    p[2].Z
                ) /
                2.0f);

        AddAutoNormalQuad(
            positions,
            normals,
            uvs,
            indices,
            materials,
            new Vector3(
                p[0].X,
                wallHeight,
                p[0].Z),
            new Vector3(
                p[1].X,
                wallHeight,
                p[1].Z),
            ridgeB,
            ridgeA,
            1,
            preferUp:
                true);

        AddAutoNormalQuad(
            positions,
            normals,
            uvs,
            indices,
            materials,
            new Vector3(
                p[2].X,
                wallHeight,
                p[2].Z),
            new Vector3(
                p[3].X,
                wallHeight,
                p[3].Z),
            ridgeA,
            ridgeB,
            1,
            preferUp:
                true);

        AddAutoNormalTriangle(
            positions,
            normals,
            uvs,
            indices,
            materials,
            new Vector3(
                p[1].X,
                wallHeight,
                p[1].Z),
            new Vector3(
                p[2].X,
                wallHeight,
                p[2].Z),
            ridgeB,
            0,
            preferUp:
                false);

        AddAutoNormalTriangle(
            positions,
            normals,
            uvs,
            indices,
            materials,
            new Vector3(
                p[3].X,
                wallHeight,
                p[3].Z),
            new Vector3(
                p[0].X,
                wallHeight,
                p[0].Z),
            ridgeA,
            0,
            preferUp:
                false);
    }

    private static void AddAutoNormalQuad(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> materials,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        ushort material,
        bool preferUp)
    {
        var normal =
            Vector3.Normalize(
                Vector3.Cross(
                    b - a,
                    c - a));

        if (
            preferUp &&
            normal.Y <
                0)
        {
            normal =
                -normal;

            (
                b,
                d
            ) =
            (
                d,
                b
            );
        }

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            materials,
            a,
            b,
            c,
            d,
            normal,
            material);
    }

    private static void AddAutoNormalTriangle(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> materials,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        ushort material,
        bool preferUp)
    {
        var normal =
            Vector3.Normalize(
                Vector3.Cross(
                    b - a,
                    c - a));

        if (
            preferUp &&
            normal.Y <
                0)
        {
            normal =
                -normal;

            (
                b,
                c
            ) =
            (
                c,
                b
            );
        }

        AddTriangle(
            positions,
            normals,
            uvs,
            indices,
            materials,
            a,
            b,
            c,
            normal,
            material);
    }

    private static List<Vector3> NormalizeFootprint(
        IReadOnlyList<MapStudioRoadPoint> points,
        MapStudioRoadPoint center)
    {
        var result =
            new List<Vector3>(
                points.Count);

        foreach (var point in points)
        {
            var local =
                new Vector3(
                    (float)(
                        point.X -
                        center.X),
                    0,
                    (float)(
                        point.Z -
                        center.Z));

            if (
                result.Count ==
                    0 ||
                Vector3.DistanceSquared(
                    result[^1],
                    local) >
                    0.0001f)
            {
                result.Add(
                    local);
            }
        }

        if (
            result.Count >
                2 &&
            Vector3.DistanceSquared(
                result[0],
                result[^1]) <=
                0.0001f)
        {
            result.RemoveAt(
                result.Count -
                1);
        }

        return result;
    }

    private static bool IsSimplePolygon(
        IReadOnlyList<Vector3> points)
    {
        for (
            var first = 0;
            first < points.Count;
            first++)
        {
            var firstNext =
                (
                    first +
                    1
                ) %
                points.Count;

            for (
                var second =
                    first + 1;
                second < points.Count;
                second++)
            {
                var secondNext =
                    (
                        second +
                        1
                    ) %
                    points.Count;

                if (
                    first ==
                        second ||
                    firstNext ==
                        second ||
                    secondNext ==
                        first)
                {
                    continue;
                }

                if (
                    first ==
                        0 &&
                    secondNext ==
                        0)
                {
                    continue;
                }

                if (
                    SegmentsIntersect(
                        points[first],
                        points[firstNext],
                        points[second],
                        points[secondNext]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool SegmentsIntersect(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d)
    {
        static float Orientation(
            Vector3 p,
            Vector3 q,
            Vector3 r) =>
            (
                q.X -
                p.X
            ) *
            (
                r.Z -
                p.Z
            ) -
            (
                q.Z -
                p.Z
            ) *
            (
                r.X -
                p.X
            );

        var o1 =
            Orientation(
                a,
                b,
                c);

        var o2 =
            Orientation(
                a,
                b,
                d);

        var o3 =
            Orientation(
                c,
                d,
                a);

        var o4 =
            Orientation(
                c,
                d,
                b);

        const float epsilon =
            0.000001f;

        if (
            Math.Abs(o1) <= epsilon ||
            Math.Abs(o2) <= epsilon ||
            Math.Abs(o3) <= epsilon ||
            Math.Abs(o4) <= epsilon)
        {
            return false;
        }

        return
            Math.Sign(o1) !=
                Math.Sign(o2) &&
            Math.Sign(o3) !=
                Math.Sign(o4);
    }

    private static List<Triangle>
        Triangulate(
            IReadOnlyList<Vector3> points)
    {
        var result =
            new List<Triangle>();

        if (points.Count < 3)
        {
            return result;
        }

        var indices =
            Enumerable
                .Range(
                    0,
                    points.Count)
                .ToList();

        var ccw =
            SignedArea(
                points) >
            0;

        var guard =
            points.Count *
            points.Count *
            2;

        while (
            indices.Count >
                3 &&
            guard-- >
                0)
        {
            var earFound =
                false;

            for (
                var cursor = 0;
                cursor < indices.Count;
                cursor++)
            {
                var previous =
                    indices[
                        (
                            cursor -
                            1 +
                            indices.Count
                        ) %
                        indices.Count];

                var current =
                    indices[
                        cursor];

                var next =
                    indices[
                        (
                            cursor +
                            1
                        ) %
                        indices.Count];

                if (
                    !IsConvex(
                        points[
                            previous],
                        points[
                            current],
                        points[
                            next],
                        ccw))
                {
                    continue;
                }

                var containsPoint =
                    false;

                foreach (
                    var candidate in
                        indices)
                {
                    if (
                        candidate ==
                            previous ||
                        candidate ==
                            current ||
                        candidate ==
                            next)
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
                        containsPoint =
                            true;
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

                indices.RemoveAt(
                    cursor);

                earFound =
                    true;

                break;
            }

            if (!earFound)
            {
                return [];
            }
        }

        if (indices.Count == 3)
        {
            result.Add(
                new Triangle(
                    indices[0],
                    indices[1],
                    indices[2]));
        }

        return result;
    }

    private static bool IsConvex(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        bool ccw)
    {
        var cross =
            (
                b.X -
                a.X
            ) *
            (
                c.Z -
                b.Z
            ) -
            (
                b.Z -
                a.Z
            ) *
            (
                c.X -
                b.X
            );

        return ccw
            ? cross >
                0.000001f
            : cross <
                -0.000001f;
    }

    private static bool PointInsideTriangle(
        Vector3 p,
        Vector3 a,
        Vector3 b,
        Vector3 c)
    {
        var d1 =
            Sign(
                p,
                a,
                b);

        var d2 =
            Sign(
                p,
                b,
                c);

        var d3 =
            Sign(
                p,
                c,
                a);

        var hasNegative =
            d1 <
                -0.000001f ||
            d2 <
                -0.000001f ||
            d3 <
                -0.000001f;

        var hasPositive =
            d1 >
                0.000001f ||
            d2 >
                0.000001f ||
            d3 >
                0.000001f;

        return !(
            hasNegative &&
            hasPositive);
    }

    private static float Sign(
        Vector3 p1,
        Vector3 p2,
        Vector3 p3) =>
        (
            p1.X -
            p3.X
        ) *
        (
            p2.Z -
            p3.Z
        ) -
        (
            p2.X -
            p3.X
        ) *
        (
            p1.Z -
            p3.Z
        );

    private static double SignedArea(
        IReadOnlyList<Vector3> points)
    {
        double area =
            0;

        for (
            var index = 0;
            index < points.Count;
            index++)
        {
            var next =
                (
                    index +
                    1
                ) %
                points.Count;

            area +=
                points[index].X *
                    points[next].Z -
                points[next].X *
                    points[index].Z;
        }

        return area /
            2.0;
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
        Vector3 normal,
        ushort material)
    {
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

        indices.AddRange(
            [
                start,
                start + 1,
                start + 2,
                start,
                start + 2,
                start + 3
            ]);

        materials.Add(
            material);

        materials.Add(
            material);
    }

    private static void AddTriangle(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> materials,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 normal,
        ushort material)
    {
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
            0.5f,
            0);

        indices.AddRange(
            [
                start,
                start + 1,
                start + 2
            ]);

        materials.Add(
            material);
    }

    private static void AddVertex(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        Vector3 position,
        Vector3 normal,
        float u,
        float v)
    {
        positions.Add(
            position.X);
        positions.Add(
            position.Y);
        positions.Add(
            position.Z);

        normals.Add(
            normal.X);
        normals.Add(
            normal.Y);
        normals.Add(
            normal.Z);

        uvs.Add(
            u);
        uvs.Add(
            v);
    }

    private static string BuildAssetName(
        MapStudioProjectedBuildingFootprint building) =>
        string.IsNullOrWhiteSpace(
            building.Name)
            ? building.Id
            : building.Id +
              "-" +
              building.Name;

    private static string BuildSceneryObject(
        MapStudioProjectedBuildingFootprint building) =>
        "[friendlyname]\r\n" +
        (
            string.IsNullOrWhiteSpace(
                building.Name)
                ? building.Id
                : building.Name
        ) +
        "\r\n[groups]\r\n2\r\nMapStudio\r\nOSM Buildings\r\n" +
        "[mesh]\r\nbuilding.o3d\r\n";

    private static string BuildManifest(
        MapStudioProjectedBuildingFootprint building) =>
        $"OMSI Map Studio OSM Building\n" +
        $"Id={building.Id}\n" +
        $"Name={building.Name}\n" +
        $"Type={building.BuildingType}\n" +
        $"Floors={building.FloorCount}\n" +
        $"WallHeight={building.WallHeightMeters:0.###}\n" +
        $"Roof={building.RoofType}\n" +
        $"RoofHeight={building.RoofHeightMeters:0.###}\n" +
        $"FootprintPoints={building.Points.Count}\n" +
        $"Street={building.Street}\n" +
        $"HouseNumber={building.HouseNumber}\n";

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
                            invalid.Contains(
                                character) ||
                            char.IsControl(
                                character)
                                ? '_'
                                : character)
                    .ToArray())
                .Trim();

        cleaned =
            string.Join(
                "_",
                cleaned
                    .Split(
                        ' ',
                        StringSplitOptions
                            .RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(
            cleaned)
            ? "OSM_Building"
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

    private readonly record struct Triangle(
        int A,
        int B,
        int C);
}
