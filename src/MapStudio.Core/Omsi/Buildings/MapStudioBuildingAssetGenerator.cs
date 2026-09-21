using System.Numerics;
using System.Text;
using MapStudio.Core.AI;
using MapStudio.Core.Omsi.Models;

namespace MapStudio.Core.Omsi.Buildings;

public sealed record MapStudioBuildingSpec(
    string Name,
    double WidthMeters,
    double DepthMeters,
    double WallHeightMeters,
    int FloorCount,
    MapStudioBuildingRoofType RoofType,
    double RoofHeightMeters = 0,
    string? FacadeImagePath = null,
    int WindowsPerFloor = 0,
    int DoorCount = 0,
    double WindowWidthMeters = 1.2,
    double WindowHeightMeters = 1.2,
    double DoorWidthMeters = 1.0,
    double DoorHeightMeters = 2.1)
{
    public MapStudioBuildingSpec Normalize()
    {
        var name =
            string.IsNullOrWhiteSpace(
                Name)
                ? "Building"
                : Name.Trim();

        var width =
            Math.Clamp(
                double.IsFinite(
                    WidthMeters)
                    ? WidthMeters
                    : 10,
                1,
                500);

        var depth =
            Math.Clamp(
                double.IsFinite(
                    DepthMeters)
                    ? DepthMeters
                    : 10,
                1,
                500);

        var wallHeight =
            Math.Clamp(
                double.IsFinite(
                    WallHeightMeters)
                    ? WallHeightMeters
                    : 3,
                1,
                500);

        var floors =
            Math.Clamp(
                FloorCount,
                1,
                300);

        var roofHeight =
            RoofType is
                MapStudioBuildingRoofType.Gable or
                MapStudioBuildingRoofType.Hip or
                MapStudioBuildingRoofType.Shed
                ? Math.Clamp(
                    double.IsFinite(
                        RoofHeightMeters)
                        ? RoofHeightMeters
                        : 2,
                    0.25,
                    100)
                : 0;

        var windowsPerFloor =
            Math.Clamp(
                WindowsPerFloor,
                0,
                64);

        var doorCount =
            Math.Clamp(
                DoorCount,
                0,
                16);

        var floorHeight =
            wallHeight /
            floors;

        var windowWidth =
            Math.Clamp(
                double.IsFinite(
                    WindowWidthMeters)
                    ? WindowWidthMeters
                    : 1.2,
                0.30,
                Math.Max(
                    0.30,
                    width));

        var windowHeight =
            Math.Clamp(
                double.IsFinite(
                    WindowHeightMeters)
                    ? WindowHeightMeters
                    : 1.2,
                0.30,
                Math.Max(
                    0.30,
                    floorHeight *
                    0.85));

        var doorWidth =
            Math.Clamp(
                double.IsFinite(
                    DoorWidthMeters)
                    ? DoorWidthMeters
                    : 1.0,
                0.50,
                Math.Max(
                    0.50,
                    width));

        var doorHeight =
            Math.Clamp(
                double.IsFinite(
                    DoorHeightMeters)
                    ? DoorHeightMeters
                    : 2.1,
                1.20,
                Math.Max(
                    1.20,
                    floorHeight *
                    0.95));

        return this with
        {
            Name = name,
            WidthMeters = width,
            DepthMeters = depth,
            WallHeightMeters =
                wallHeight,
            FloorCount = floors,
            RoofHeightMeters =
                roofHeight,
            WindowsPerFloor =
                windowsPerFloor,
            DoorCount =
                doorCount,
            WindowWidthMeters =
                windowWidth,
            WindowHeightMeters =
                windowHeight,
            DoorWidthMeters =
                doorWidth,
            DoorHeightMeters =
                doorHeight
        };
    }
}

public sealed record MapStudioBuildingAssetResult(
    string ObjectDirectory,
    string SceneryObjectPath,
    string MeshPath,
    string? FacadeTexturePath,
    string? BackupDirectory);

public sealed class MapStudioBuildingAssetGenerator
{
    public const string RootFolderName =
        "MapStudio_Buildings";

    public async Task<MapStudioBuildingAssetResult>
        GenerateAsync(
            string omsiRoot,
            MapStudioBuildingSpec spec,
            CancellationToken cancellationToken =
                default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            omsiRoot);

        ArgumentNullException.ThrowIfNull(
            spec);

        var normalized =
            spec.Normalize();

        var root =
            Path.GetFullPath(
                omsiRoot);

        var sceneryRoot =
            Path.Combine(
                root,
                "Sceneryobjects",
                RootFolderName);

        Directory.CreateDirectory(
            sceneryRoot);

        var assetName =
            SanitizeName(
                normalized.Name);

        var objectDirectory =
            Path.Combine(
                sceneryRoot,
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

            var textureDirectory =
                Path.Combine(
                    temporaryDirectory,
                    "Texture");

            Directory.CreateDirectory(
                modelDirectory);

            Directory.CreateDirectory(
                textureDirectory);

            string? facadeTextureName =
                null;

            string? facadeTexturePath =
                null;

            if (
                !string.IsNullOrWhiteSpace(
                    normalized
                        .FacadeImagePath) &&
                File.Exists(
                    normalized
                        .FacadeImagePath))
            {
                var extension =
                    Path.GetExtension(
                        normalized
                            .FacadeImagePath);

                if (
                    extension is
                        ".png" or
                        ".jpg" or
                        ".jpeg" or
                        ".bmp" or
                        ".dds" or
                        ".tga" or
                        ".webp")
                {
                    facadeTextureName =
                        "facade" +
                        extension
                            .ToLowerInvariant();

                    facadeTexturePath =
                        Path.Combine(
                            textureDirectory,
                            facadeTextureName);

                    File.Copy(
                        normalized
                            .FacadeImagePath,
                        facadeTexturePath,
                        overwrite: true);
                }
            }

            var geometry =
                BuildGeometry(
                    normalized,
                    facadeTextureName);

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

            var sco =
                BuildSceneryObject(
                    normalized,
                    assetName);

            await File.WriteAllTextAsync(
                    sceneryObjectPath,
                    sco,
                    Encoding.ASCII,
                    cancellationToken)
                .ConfigureAwait(false);

            await File.WriteAllTextAsync(
                    Path.Combine(
                        temporaryDirectory,
                        "mapstudio-building.txt"),
                    BuildManifest(
                        normalized),
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

            return new MapStudioBuildingAssetResult(
                objectDirectory,
                Path.Combine(
                    objectDirectory,
                    assetName +
                    ".sco"),
                Path.Combine(
                    objectDirectory,
                    "model",
                    "building.o3d"),
                facadeTextureName is null
                    ? null
                    : Path.Combine(
                        objectDirectory,
                        "Texture",
                        facadeTextureName),
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
        MapStudioBuildingSpec spec,
        string? facadeTextureName =
            null)
    {
        ArgumentNullException.ThrowIfNull(
            spec);

        var normalized =
            spec.Normalize();

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

        var halfWidth =
            (float)(
                normalized
                    .WidthMeters /
                2.0);

        var halfDepth =
            (float)(
                normalized
                    .DepthMeters /
                2.0);

        var wallHeight =
            (float)
                normalized
                    .WallHeightMeters;

        var roofHeight =
            (float)
                normalized
                    .RoofHeightMeters;

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                -halfWidth,
                0,
                -halfDepth),
            new Vector3(
                halfWidth,
                0,
                -halfDepth),
            new Vector3(
                halfWidth,
                wallHeight,
                -halfDepth),
            new Vector3(
                -halfWidth,
                wallHeight,
                -halfDepth),
            Vector3.UnitZ *
                -1,
            0);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                halfWidth,
                0,
                halfDepth),
            new Vector3(
                -halfWidth,
                0,
                halfDepth),
            new Vector3(
                -halfWidth,
                wallHeight,
                halfDepth),
            new Vector3(
                halfWidth,
                wallHeight,
                halfDepth),
            Vector3.UnitZ,
            0);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                -halfWidth,
                0,
                halfDepth),
            new Vector3(
                -halfWidth,
                0,
                -halfDepth),
            new Vector3(
                -halfWidth,
                wallHeight,
                -halfDepth),
            new Vector3(
                -halfWidth,
                wallHeight,
                halfDepth),
            Vector3.UnitX *
                -1,
            0);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                halfWidth,
                0,
                -halfDepth),
            new Vector3(
                halfWidth,
                0,
                halfDepth),
            new Vector3(
                halfWidth,
                wallHeight,
                halfDepth),
            new Vector3(
                halfWidth,
                wallHeight,
                -halfDepth),
            Vector3.UnitX,
            0);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                -halfWidth,
                0,
                halfDepth),
            new Vector3(
                halfWidth,
                0,
                halfDepth),
            new Vector3(
                halfWidth,
                0,
                -halfDepth),
            new Vector3(
                -halfWidth,
                0,
                -halfDepth),
            Vector3.UnitY *
                -1,
            0);

        if (
            normalized.RoofType ==
            MapStudioBuildingRoofType
                .Gable)
        {
            AddGableRoof(
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials,
                halfWidth,
                halfDepth,
                wallHeight,
                roofHeight);
        }
        else
        {
            AddQuad(
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials,
                new Vector3(
                    -halfWidth,
                    wallHeight,
                    -halfDepth),
                new Vector3(
                    halfWidth,
                    wallHeight,
                    -halfDepth),
                new Vector3(
                    halfWidth,
                    wallHeight,
                    halfDepth),
                new Vector3(
                    -halfWidth,
                    wallHeight,
                    halfDepth),
                Vector3.UnitY,
                1);
        }

        AddFacadeDetails(
            normalized,
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            halfWidth,
            halfDepth,
            wallHeight);

        var materials =
            new[]
            {
                new OmsiO3dMaterial(
                    0.86f,
                    0.86f,
                    0.86f,
                    1,
                    0.04f,
                    0.04f,
                    0.04f,
                    0,
                    0,
                    0,
                    8,
                    facadeTextureName),
                new OmsiO3dMaterial(
                    0.34f,
                    0.30f,
                    0.28f,
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
                    0.16f,
                    0.32f,
                    0.42f,
                    1,
                    0.08f,
                    0.12f,
                    0.16f,
                    0,
                    0,
                    0,
                    24,
                    null),
                new OmsiO3dMaterial(
                    0.30f,
                    0.16f,
                    0.08f,
                    1,
                    0.04f,
                    0.03f,
                    0.02f,
                    0,
                    0,
                    0,
                    8,
                    null)
            };

        return new OmsiO3dGeometry(
            true,
            null,
            positions.ToArray(),
            normals.ToArray(),
            uvs.ToArray(),
            indices.ToArray(),
            triangleMaterials.ToArray(),
            materials);
    }

    private static void AddFacadeDetails(
        MapStudioBuildingSpec spec,
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials,
        float halfWidth,
        float halfDepth,
        float wallHeight)
    {
        const ushort windowMaterial =
            2;

        const ushort doorMaterial =
            3;

        var frontZ =
            -halfDepth -
            0.02f;

        var buildingWidth =
            halfWidth *
            2;

        var floorHeight =
            wallHeight /
            Math.Max(
                1,
                spec.FloorCount);

        var windowWidth =
            (float)Math.Min(
                spec.WindowWidthMeters,
                Math.Max(
                    0.30,
                    buildingWidth *
                    0.40));

        var windowHeight =
            (float)Math.Min(
                spec.WindowHeightMeters,
                Math.Max(
                    0.30,
                    floorHeight *
                    0.75));

        var windowCount =
            Math.Min(
                spec.WindowsPerFloor,
                Math.Max(
                    0,
                    (int)Math.Floor(
                        buildingWidth /
                        Math.Max(
                            0.50f,
                            windowWidth +
                            0.35f))));

        if (windowCount > 0)
        {
            var spacing =
                buildingWidth /
                (
                    windowCount +
                    1
                );

            for (
                var floor = 0;
                floor <
                    spec.FloorCount;
                floor++)
            {
                var centerY =
                    floor *
                    floorHeight +
                    floorHeight *
                    0.55f;

                var bottom =
                    Math.Clamp(
                        centerY -
                        windowHeight /
                        2,
                        0.25f,
                        wallHeight -
                        windowHeight -
                        0.10f);

                var top =
                    bottom +
                    windowHeight;

                for (
                    var window = 0;
                    window <
                        windowCount;
                    window++)
                {
                    var centerX =
                        -halfWidth +
                        spacing *
                        (
                            window +
                            1
                        );

                    var left =
                        centerX -
                        windowWidth /
                        2;

                    var right =
                        centerX +
                        windowWidth /
                        2;

                    AddQuad(
                        positions,
                        normals,
                        uvs,
                        indices,
                        triangleMaterials,
                        new Vector3(
                            left,
                            bottom,
                            frontZ),
                        new Vector3(
                            right,
                            bottom,
                            frontZ),
                        new Vector3(
                            right,
                            top,
                            frontZ),
                        new Vector3(
                            left,
                            top,
                            frontZ),
                        Vector3.UnitZ *
                            -1,
                        windowMaterial);
                }
            }
        }

        var doorWidth =
            (float)Math.Min(
                spec.DoorWidthMeters,
                Math.Max(
                    0.50,
                    buildingWidth *
                    0.45));

        var doorHeight =
            (float)Math.Min(
                spec.DoorHeightMeters,
                Math.Max(
                    1.20,
                    floorHeight *
                    0.90));

        var doorCount =
            Math.Min(
                spec.DoorCount,
                Math.Max(
                    0,
                    (int)Math.Floor(
                        buildingWidth /
                        Math.Max(
                            0.80f,
                            doorWidth +
                            0.50f))));

        if (doorCount <= 0)
        {
            return;
        }

        var doorSpacing =
            buildingWidth /
            (
                doorCount +
                1
            );

        for (
            var door = 0;
            door <
                doorCount;
            door++)
        {
            var centerX =
                -halfWidth +
                doorSpacing *
                (
                    door +
                    1
                );

            var left =
                centerX -
                doorWidth /
                2;

            var right =
                centerX +
                doorWidth /
                2;

            AddQuad(
                positions,
                normals,
                uvs,
                indices,
                triangleMaterials,
                new Vector3(
                    left,
                    0.02f,
                    frontZ -
                    0.01f),
                new Vector3(
                    right,
                    0.02f,
                    frontZ -
                    0.01f),
                new Vector3(
                    right,
                    doorHeight,
                    frontZ -
                    0.01f),
                new Vector3(
                    left,
                    doorHeight,
                    frontZ -
                    0.01f),
                Vector3.UnitZ *
                    -1,
                doorMaterial);
        }
    }

    private static void AddGableRoof(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials,
        float halfWidth,
        float halfDepth,
        float wallHeight,
        float roofHeight)
    {
        var ridgeY =
            wallHeight +
            roofHeight;

        var leftNormal =
            Vector3.Normalize(
                new Vector3(
                    -roofHeight,
                    halfWidth,
                    0));

        var rightNormal =
            Vector3.Normalize(
                new Vector3(
                    roofHeight,
                    halfWidth,
                    0));

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                -halfWidth,
                wallHeight,
                -halfDepth),
            new Vector3(
                0,
                ridgeY,
                -halfDepth),
            new Vector3(
                0,
                ridgeY,
                halfDepth),
            new Vector3(
                -halfWidth,
                wallHeight,
                halfDepth),
            leftNormal,
            1);

        AddQuad(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                0,
                ridgeY,
                -halfDepth),
            new Vector3(
                halfWidth,
                wallHeight,
                -halfDepth),
            new Vector3(
                halfWidth,
                wallHeight,
                halfDepth),
            new Vector3(
                0,
                ridgeY,
                halfDepth),
            rightNormal,
            1);

        AddTriangle(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                -halfWidth,
                wallHeight,
                -halfDepth),
            new Vector3(
                halfWidth,
                wallHeight,
                -halfDepth),
            new Vector3(
                0,
                ridgeY,
                -halfDepth),
            Vector3.UnitZ *
                -1,
            0);

        AddTriangle(
            positions,
            normals,
            uvs,
            indices,
            triangleMaterials,
            new Vector3(
                halfWidth,
                wallHeight,
                halfDepth),
            new Vector3(
                -halfWidth,
                wallHeight,
                halfDepth),
            new Vector3(
                0,
                ridgeY,
                halfDepth),
            Vector3.UnitZ,
            0);
    }

    private static void AddQuad(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials,
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

        triangleMaterials.Add(
            material);

        triangleMaterials.Add(
            material);
    }

    private static void AddTriangle(
        List<float> positions,
        List<float> normals,
        List<float> uvs,
        List<uint> indices,
        List<ushort> triangleMaterials,
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

        triangleMaterials.Add(
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

    private static string BuildSceneryObject(
        MapStudioBuildingSpec spec,
        string assetName)
    {
        var builder =
            new StringBuilder();

        builder.AppendLine(
            "[friendlyname]");
        builder.AppendLine(
            spec.Name);
        builder.AppendLine(
            "[groups]");
        builder.AppendLine(
            "2");
        builder.AppendLine(
            "MapStudio");
        builder.AppendLine(
            "Building Studio");
        builder.AppendLine(
            "[mesh]");
        builder.AppendLine(
            "building.o3d");

        return builder
            .ToString()
            .Replace(
                "\n",
                "\r\n",
                StringComparison.Ordinal);
    }

    private static string BuildManifest(
        MapStudioBuildingSpec spec) =>
        $"OMSI Map Studio Building\n" +
        $"Name={spec.Name}\n" +
        $"Width={spec.WidthMeters:0.###}\n" +
        $"Depth={spec.DepthMeters:0.###}\n" +
        $"WallHeight={spec.WallHeightMeters:0.###}\n" +
        $"Floors={spec.FloorCount}\n" +
        $"Roof={spec.RoofType}\n" +
        $"RoofHeight={spec.RoofHeightMeters:0.###}\n" +
        $"WindowsPerFloor={spec.WindowsPerFloor}\n" +
        $"Doors={spec.DoorCount}\n" +
        $"WindowSize={spec.WindowWidthMeters:0.###}x{spec.WindowHeightMeters:0.###}\n" +
        $"DoorSize={spec.DoorWidthMeters:0.###}x{spec.DoorHeightMeters:0.###}\n";

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
            ? "Building"
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
}
