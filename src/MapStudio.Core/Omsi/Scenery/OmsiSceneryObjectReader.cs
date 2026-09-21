using System.Globalization;
using MapStudio.Core.Omsi.Config;

namespace MapStudio.Core.Omsi.Scenery;

public sealed class OmsiSceneryObjectReader
{
    public async Task<OmsiSceneryObjectMetadata> ReadMetadataAsync(
        string sceneryObjectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneryObjectPath);

        if (!File.Exists(sceneryObjectPath))
        {
            return OmsiSceneryObjectMetadata.Missing;
        }

        var document = await OmsiConfigParser.ParseFileAsync(
            sceneryObjectPath,
            cancellationToken);

        return ReadMetadata(document);
    }

    public static OmsiSceneryObjectMetadata ReadMetadata(
        OmsiConfigDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var friendlyName = document
            .FindFirstSection("friendlyname")
            ?.DataLines
            .FirstOrDefault();

        var groups = ReadGroups(document);
        var meshes = ReadMeshes(document);
        var collisionMeshes = ReadSingleValueSections(
            document,
            "collision_mesh");

        var materialOverrides =
            ReadMaterialOverrides(
                document);

        var tree =
            ReadTree(document);

        var renderType =
            document
                .FindFirstSection(
                    "rendertype")
                ?.DataLines
                .FirstOrDefault()
                ?.Trim();

        if (string.IsNullOrWhiteSpace(
                renderType))
        {
            renderType = null;
        }

        return new OmsiSceneryObjectMetadata(
            Exists: true,
            FriendlyName: friendlyName,
            Groups: groups,
            MeshPaths: meshes.Paths,
            MeshLodThresholds:
                meshes.LodThresholds,
            MeshTransforms:
                meshes.Transforms,
            CollisionMeshPaths: collisionMeshes,
            MaterialOverrides:
                materialOverrides,
            UsesAbsoluteHeight:
                document.FindFirstSection(
                    "absheight") is not null,
            Tree: tree,
            RenderType: renderType)
        {
            TrafficLightControllers =
                ReadTrafficLightControllers(
                    document),
            LightPoints =
                ReadLightPoints(
                    document),
            Paths =
                ReadPaths(
                    document),
            IsTrafficLightObject =
                document.FindFirstSection(
                    "trafficlight") is not null,
            UsesLightMapMapping =
                document.FindFirstSection(
                    "LightMapMapping") is not null
        };
    }

    private static MeshReadResult
        ReadMeshes(
            OmsiConfigDocument document)
    {
        var paths =
            new List<string>();

        var lodThresholds =
            new List<double?>();

        var transforms =
            new List<
                OmsiSceneryMeshTransform>();

        double? currentLodThreshold =
            null;

        var currentMeshOrdinal = -1;

        foreach (var section in
            document.Sections)
        {
            if (string.Equals(
                    section.Keyword,
                    "LOD",
                    StringComparison.OrdinalIgnoreCase))
            {
                currentLodThreshold =
                    null;

                if (
                    TryReadFiniteDouble(
                        section.DataLines
                            .FirstOrDefault(),
                        out var threshold) &&
                    threshold >= 0)
                {
                    currentLodThreshold =
                        threshold;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "mesh",
                    StringComparison.OrdinalIgnoreCase))
            {
                var path =
                    section.DataLines
                        .FirstOrDefault();

                if (string.IsNullOrWhiteSpace(
                        path))
                {
                    currentMeshOrdinal = -1;
                    continue;
                }

                paths.Add(path);
                lodThresholds.Add(
                    currentLodThreshold);
                transforms.Add(
                    OmsiSceneryMeshTransform
                        .Identity);

                currentMeshOrdinal =
                    transforms.Count - 1;

                continue;
            }

            if (
                currentMeshOrdinal < 0 ||
                currentMeshOrdinal >=
                    transforms.Count)
            {
                continue;
            }

            var current =
                transforms[
                    currentMeshOrdinal];

            if (string.Equals(
                    section.Keyword,
                    "new_pos",
                    StringComparison.OrdinalIgnoreCase))
            {
                var values =
                    section.DataLines
                        .ToArray();

                if (
                    values.Length >= 3 &&
                    TryReadFiniteDouble(
                        values[0],
                        out var x) &&
                    TryReadFiniteDouble(
                        values[1],
                        out var y) &&
                    TryReadFiniteDouble(
                        values[2],
                        out var z))
                {
                    transforms[
                        currentMeshOrdinal] =
                        current with
                        {
                            PositionX = x,
                            PositionY = y,
                            PositionZ = z
                        };
                }

                continue;
            }

            if (
                string.Equals(
                    section.Keyword,
                    "rot_x",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    section.Keyword,
                    "rotx",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadFiniteDouble(
                        section.DataLines
                            .FirstOrDefault(),
                        out var rotation))
                {
                    transforms[
                        currentMeshOrdinal] =
                        current with
                        {
                            RotationX =
                                rotation
                        };
                }

                continue;
            }

            if (
                string.Equals(
                    section.Keyword,
                    "rot_y",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    section.Keyword,
                    "roty",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadFiniteDouble(
                        section.DataLines
                            .FirstOrDefault(),
                        out var rotation))
                {
                    transforms[
                        currentMeshOrdinal] =
                        current with
                        {
                            RotationY =
                                rotation
                        };
                }

                continue;
            }

            if (
                string.Equals(
                    section.Keyword,
                    "rot_z",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    section.Keyword,
                    "rotz",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadFiniteDouble(
                        section.DataLines
                            .FirstOrDefault(),
                        out var rotation))
                {
                    transforms[
                        currentMeshOrdinal] =
                        current with
                        {
                            RotationZ =
                                rotation
                        };
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "scale",
                    StringComparison.OrdinalIgnoreCase))
            {
                var values =
                    section.DataLines
                        .ToArray();

                if (
                    values.Length > 0 &&
                    TryReadFiniteDouble(
                        values[0],
                        out var scaleX))
                {
                    var scaleY = scaleX;
                    var scaleZ = scaleX;

                    if (
                        values.Length >= 3 &&
                        TryReadFiniteDouble(
                            values[1],
                            out var parsedScaleY) &&
                        TryReadFiniteDouble(
                            values[2],
                            out var parsedScaleZ))
                    {
                        scaleY =
                            parsedScaleY;
                        scaleZ =
                            parsedScaleZ;
                    }

                    transforms[
                        currentMeshOrdinal] =
                        current with
                        {
                            ScaleX = scaleX,
                            ScaleY = scaleY,
                            ScaleZ = scaleZ
                        };
                }
            }
        }

        return new MeshReadResult(
            paths,
            lodThresholds,
            transforms);
    }

    private static bool
        TryReadFiniteDouble(
            string? value,
            out double result)
    {
        result = 0;

        return
            double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out result) &&
            double.IsFinite(result);
    }

    private static IReadOnlyList<
        OmsiSceneryMaterialOverride>
        ReadMaterialOverrides(
            OmsiConfigDocument document)
    {
        var result =
            new List<MaterialOverrideBuilder>();

        var meshOrdinal = -1;

        MaterialOverrideBuilder?
            current = null;

        foreach (var section in
            document.Sections)
        {
            if (string.Equals(
                    section.Keyword,
                    "mesh",
                    StringComparison.OrdinalIgnoreCase))
            {
                meshOrdinal++;
                current = null;
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_change",
                    StringComparison.OrdinalIgnoreCase))
            {
                current = null;
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl",
                    StringComparison.OrdinalIgnoreCase))
            {
                current = null;

                if (meshOrdinal < 0)
                {
                    continue;
                }

                var values =
                    section.DataLines
                        .ToArray();

                if (
                    values.Length < 2 ||
                    string.IsNullOrWhiteSpace(
                        values[0]) ||
                    !int.TryParse(
                        values[1],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var materialIndex) ||
                    materialIndex < 0)
                {
                    continue;
                }

                current =
                    new MaterialOverrideBuilder(
                        meshOrdinal,
                        values[0],
                        materialIndex);

                result.Add(current);
                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_alpha",
                    StringComparison.OrdinalIgnoreCase))
            {
                var value =
                    section.DataLines
                        .FirstOrDefault();

                if (
                    int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var alphaMode) &&
                    alphaMode is >= 0 and <= 2)
                {
                    current.AlphaMode =
                        alphaMode;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_transmap",
                    StringComparison.OrdinalIgnoreCase))
            {
                var source =
                    section.DataLines
                        .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(
                        source))
                {
                    current.TransMapSource =
                        source;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_lightmap",
                    StringComparison.OrdinalIgnoreCase))
            {
                var textureName =
                    section.DataLines
                        .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(
                        textureName))
                {
                    current.LightMapTextureName =
                        textureName;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_envmap",
                    StringComparison.OrdinalIgnoreCase))
            {
                var values =
                    section.DataLines
                        .ToArray();

                if (
                    values.Length > 0 &&
                    !string.IsNullOrWhiteSpace(
                        values[0]))
                {
                    current
                        .EnvironmentMapTextureName =
                        values[0];
                }

                if (
                    values.Length > 1 &&
                    double.TryParse(
                        values[1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var strength) &&
                    double.IsFinite(strength))
                {
                    current
                        .EnvironmentMapStrength =
                        strength;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_nightmap",
                    StringComparison.OrdinalIgnoreCase))
            {
                var textureName =
                    section.DataLines
                        .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(
                        textureName))
                {
                    current.NightMapTextureName =
                        textureName;
                }

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_bumpmap",
                    StringComparison.OrdinalIgnoreCase))
            {
                var values =
                    section.DataLines
                        .ToArray();

                if (
                    values.Length > 0 &&
                    !string.IsNullOrWhiteSpace(
                        values[0]))
                {
                    current.BumpMapTextureName =
                        values[0];
                }

                if (
                    values.Length > 1 &&
                    double.TryParse(
                        values[1],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var strength) &&
                    double.IsFinite(strength))
                {
                    current.BumpMapStrength =
                        strength;
                }

                continue;
            }

            if (
                string.Equals(
                    section.Keyword,
                    "matl_envmap_mask",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    section.Keyword,
                    "alphascale",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    section.Keyword,
                    "matl_allcolor",
                    StringComparison.OrdinalIgnoreCase))
            {
                current.UnsupportedCommands
                    .Add(
                        "[" +
                        section.Keyword +
                        "]");

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_noZwrite",
                    StringComparison.OrdinalIgnoreCase))
            {
                current.NoZWrite = true;
                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "matl_noZcheck",
                    StringComparison.OrdinalIgnoreCase))
            {
                current.NoZCheck = true;
            }
        }

        return result
            .Select(
                builder =>
                    new OmsiSceneryMaterialOverride(
                        builder.MeshOrdinal,
                        builder.TextureName,
                        builder.MaterialIndex,
                        builder.AlphaMode,
                        builder.NoZWrite,
                        builder.NoZCheck,
                        builder.BumpMapTextureName,
                        builder.BumpMapStrength,
                        builder.NightMapTextureName,
                        builder.EnvironmentMapTextureName,
                        builder.EnvironmentMapStrength,
                        builder.TransMapSource,
                        builder.LightMapTextureName,
                        builder.UnsupportedCommands
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .ToArray()))
            .ToArray();
    }

    private static IReadOnlyList<
        OmsiSceneryPathDefinition>
        ReadPaths(
            OmsiConfigDocument document)
    {
        var result =
            new List<
                OmsiSceneryPathDefinition>();

        for (
            var index = 0;
            index < document.Sections.Count;
            index++)
        {
            var section =
                document.Sections[index];

            if (!string.Equals(
                    section.Keyword,
                    "path",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values =
                section.DataLines
                    .Take(12)
                    .ToArray();

            if (
                values.Length < 12 ||
                !TryReadFiniteDouble(values[0], out var x) ||
                !TryReadFiniteDouble(values[1], out var y) ||
                !TryReadFiniteDouble(values[2], out var z) ||
                !TryReadFiniteDouble(values[3], out var rotation) ||
                !TryReadFiniteDouble(values[4], out var radius) ||
                !TryReadFiniteDouble(values[5], out var length) ||
                !TryReadFiniteDouble(values[6], out var gradientStart) ||
                !TryReadFiniteDouble(values[7], out var gradientEnd) ||
                !int.TryParse(
                    values[8],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var type) ||
                !TryReadFiniteDouble(values[9], out var width) ||
                !int.TryParse(
                    values[10],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var direction) ||
                !int.TryParse(
                    values[11],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var blinkerCode))
            {
                continue;
            }

            int? trafficLightIndex = null;
            int? switchDirection = null;
            var crossingProblem = false;

            for (
                var nextIndex = index + 1;
                nextIndex < document.Sections.Count;
                nextIndex++)
            {
                var next =
                    document.Sections[nextIndex];

                if (string.Equals(
                        next.Keyword,
                        "path",
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (
                    string.Equals(
                        next.Keyword,
                        "mesh",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        next.Keyword,
                        "tree",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        next.Keyword,
                        "traffic_lights_group",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        next.Keyword,
                        "trafficlight_group",
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (string.Equals(
                        next.Keyword,
                        "use_traffic_light",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (
                        int.TryParse(
                            next.DataLines
                                .FirstOrDefault(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                    {
                        trafficLightIndex =
                            parsed;
                    }

                    continue;
                }

                if (string.Equals(
                        next.Keyword,
                        "switchdir",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (
                        int.TryParse(
                            next.DataLines
                                .FirstOrDefault(),
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var parsed))
                    {
                        switchDirection =
                            parsed;
                    }

                    continue;
                }

                if (string.Equals(
                        next.Keyword,
                        "crossingproblem",
                        StringComparison.OrdinalIgnoreCase))
                {
                    crossingProblem =
                        true;
                }
            }

            result.Add(
                new OmsiSceneryPathDefinition(
                    x,
                    y,
                    z,
                    rotation,
                    radius,
                    Math.Max(0, length),
                    gradientStart,
                    gradientEnd,
                    type,
                    Math.Max(0, width),
                    direction,
                    blinkerCode,
                    trafficLightIndex,
                    switchDirection,
                    crossingProblem));
        }

        return result;
    }

    private static IReadOnlyList<
        OmsiTrafficLightController>
        ReadTrafficLightControllers(
            OmsiConfigDocument document)
    {
        var controllers =
            new List<
                TrafficLightControllerBuilder>();

        TrafficLightControllerBuilder?
            currentController =
                null;

        TrafficLightProgramBuilder?
            currentProgram =
                null;

        foreach (
            var section in
                document.Sections)
        {
            if (
                string.Equals(
                    section.Keyword,
                    "traffic_lights_group",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    section.Keyword,
                    "trafficlight_group",
                    StringComparison.OrdinalIgnoreCase))
            {
                double? cycleDuration =
                    null;

                if (
                    TryReadFiniteDouble(
                        section.DataLines
                            .FirstOrDefault(),
                        out var parsedCycle) &&
                    parsedCycle >= 0)
                {
                    cycleDuration =
                        parsedCycle;
                }

                currentController =
                    new TrafficLightControllerBuilder(
                        cycleDuration);

                controllers.Add(
                    currentController);

                currentProgram =
                    null;

                continue;
            }

            if (string.Equals(
                    section.Keyword,
                    "traffic_light",
                    StringComparison.OrdinalIgnoreCase))
            {
                currentController ??=
                    CreateImplicitTrafficController(
                        controllers);

                var name =
                    section.DataLines
                        .FirstOrDefault()
                        ?.Trim();

                if (string.IsNullOrWhiteSpace(
                        name))
                {
                    currentProgram =
                        null;

                    continue;
                }

                currentProgram =
                    new TrafficLightProgramBuilder(
                        name);

                currentController
                    .Programs.Add(
                        currentProgram);

                continue;
            }

            if (
                currentProgram is null ||
                !string.Equals(
                    section.Keyword,
                    "phase",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values =
                section.DataLines
                    .Take(2)
                    .ToArray();

            if (
                values.Length < 2 ||
                !int.TryParse(
                    values[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var signalCode) ||
                !TryReadFiniteDouble(
                    values[1],
                    out var duration) ||
                duration < 0)
            {
                continue;
            }

            currentProgram.Phases.Add(
                new OmsiTrafficLightPhase(
                    signalCode,
                    duration));
        }

        return controllers
            .Select(
                controller =>
                    new OmsiTrafficLightController(
                        controller.CycleDuration,
                        controller.Programs
                            .Select(
                                program =>
                                    new OmsiTrafficLightProgram(
                                        program.Name,
                                        program.Phases
                                            .ToArray()))
                            .ToArray()))
            .ToArray();
    }

    private static TrafficLightControllerBuilder
        CreateImplicitTrafficController(
            List<TrafficLightControllerBuilder>
                controllers)
    {
        var controller =
            new TrafficLightControllerBuilder(
                null);

        controllers.Add(
            controller);

        return controller;
    }

    private static IReadOnlyList<
        OmsiSceneryLightPoint>
        ReadLightPoints(
            OmsiConfigDocument document)
    {
        var result =
            new List<
                OmsiSceneryLightPoint>();

        foreach (
            var section in
                document.Sections)
        {
            var keyword =
                section.Keyword
                    .Trim();

            var values =
                section.DataLines
                    .ToArray();

            if (string.Equals(
                    keyword,
                    "light_enh_2",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    values.Length >= 24 &&
                    TryReadLightVector(
                        values,
                        0,
                        out var positionX,
                        out var positionY,
                        out var positionZ) &&
                    TryReadLightVector(
                        values,
                        3,
                        out var directionX,
                        out var directionY,
                        out var directionZ) &&
                    TryReadFiniteDouble(
                        values[11],
                        out var red) &&
                    TryReadFiniteDouble(
                        values[12],
                        out var green) &&
                    TryReadFiniteDouble(
                        values[13],
                        out var blue) &&
                    TryReadFiniteDouble(
                        values[14],
                        out var size) &&
                    TryReadFiniteDouble(
                        values[15],
                        out var innerAngle) &&
                    TryReadFiniteDouble(
                        values[16],
                        out var outerAngle))
                {
                    result.Add(
                        new OmsiSceneryLightPoint(
                            keyword,
                            positionX,
                            positionY,
                            positionZ,
                            directionX,
                            directionY,
                            directionZ,
                            red,
                            green,
                            blue,
                            size,
                            innerAngle,
                            outerAngle,
                            values.ElementAtOrDefault(17),
                            values.ElementAtOrDefault(18),
                            values.ElementAtOrDefault(19),
                            values.ElementAtOrDefault(24) ??
                            values.ElementAtOrDefault(23),
                            values));
                }

                continue;
            }

            if (string.Equals(
                    keyword,
                    "light_enh",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    values.Length >= 13 &&
                    TryReadLightVector(
                        values,
                        0,
                        out var positionX,
                        out var positionY,
                        out var positionZ) &&
                    TryReadFiniteDouble(
                        values[3],
                        out var red) &&
                    TryReadFiniteDouble(
                        values[4],
                        out var green) &&
                    TryReadFiniteDouble(
                        values[5],
                        out var blue) &&
                    TryReadFiniteDouble(
                        values[6],
                        out var size))
                {
                    result.Add(
                        new OmsiSceneryLightPoint(
                            keyword,
                            positionX,
                            positionY,
                            positionZ,
                            null,
                            null,
                            null,
                            red,
                            green,
                            blue,
                            size,
                            null,
                            null,
                            values.ElementAtOrDefault(7),
                            values.ElementAtOrDefault(8),
                            values.ElementAtOrDefault(9),
                            values.ElementAtOrDefault(12),
                            values));
                }

                continue;
            }

            if (string.Equals(
                    keyword,
                    "maplight",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (
                    values.Length >= 7 &&
                    TryReadLightVector(
                        values,
                        0,
                        out var positionX,
                        out var positionY,
                        out var positionZ) &&
                    TryReadFiniteDouble(
                        values[3],
                        out var red) &&
                    TryReadFiniteDouble(
                        values[4],
                        out var green) &&
                    TryReadFiniteDouble(
                        values[5],
                        out var blue) &&
                    TryReadFiniteDouble(
                        values[6],
                        out var range))
                {
                    result.Add(
                        new OmsiSceneryLightPoint(
                            keyword,
                            positionX,
                            positionY,
                            positionZ,
                            null,
                            null,
                            null,
                            red,
                            green,
                            blue,
                            Math.Clamp(
                                range * 0.025,
                                0.1,
                                1.25),
                            null,
                            null,
                            null,
                            null,
                            null,
                            null,
                            values,
                            Range:
                                Math.Max(
                                    0,
                                    range),
                            IsMapLight:
                                true));
                }

                continue;
            }

            if (
                string.Equals(
                    keyword,
                    "spotlight",
                    StringComparison.OrdinalIgnoreCase) &&
                values.Length >= 12 &&
                TryReadLightVector(
                    values,
                    0,
                    out var spotX,
                    out var spotY,
                    out var spotZ) &&
                TryReadLightVector(
                    values,
                    3,
                    out var spotDirectionX,
                    out var spotDirectionY,
                    out var spotDirectionZ) &&
                TryReadFiniteDouble(
                    values[6],
                    out var spotRed) &&
                TryReadFiniteDouble(
                    values[7],
                    out var spotGreen) &&
                TryReadFiniteDouble(
                    values[8],
                    out var spotBlue) &&
                TryReadFiniteDouble(
                    values[9],
                    out var spotRange) &&
                TryReadFiniteDouble(
                    values[10],
                    out var spotInner) &&
                TryReadFiniteDouble(
                    values[11],
                    out var spotOuter))
            {
                result.Add(
                    new OmsiSceneryLightPoint(
                        keyword,
                        spotX,
                        spotY,
                        spotZ,
                        spotDirectionX,
                        spotDirectionY,
                        spotDirectionZ,
                        spotRed,
                        spotGreen,
                        spotBlue,
                        Math.Clamp(
                            spotRange * 0.02,
                            0.1,
                            1.0),
                        spotInner,
                        spotOuter,
                        null,
                        null,
                        null,
                        null,
                        values,
                        Range:
                            Math.Max(
                                0,
                                spotRange)));
            }
        }

        return result;
    }

    private static bool TryReadLightVector(
        IReadOnlyList<string> values,
        int offset,
        out double x,
        out double y,
        out double z)
    {
        x = 0;
        y = 0;
        z = 0;

        return
            offset >= 0 &&
            values.Count >=
                offset + 3 &&
            TryReadFiniteDouble(
                values[offset],
                out x) &&
            TryReadFiniteDouble(
                values[offset + 1],
                out y) &&
            TryReadFiniteDouble(
                values[offset + 2],
                out z);
    }

    private static OmsiSceneryTreeDefinition?
        ReadTree(
            OmsiConfigDocument document)
    {
        var values =
            document
                .FindFirstSection("tree")
                ?.DataLines
                .ToArray();

        if (
            values is null ||
            values.Length < 5 ||
            string.IsNullOrWhiteSpace(
                values[0]) ||
            !double.TryParse(
                values[1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var minimumHeight) ||
            !double.TryParse(
                values[2],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var maximumHeight) ||
            !double.TryParse(
                values[3],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var minimumAspect) ||
            !double.TryParse(
                values[4],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var maximumAspect) ||
            !double.IsFinite(minimumHeight) ||
            !double.IsFinite(maximumHeight) ||
            !double.IsFinite(minimumAspect) ||
            !double.IsFinite(maximumAspect) ||
            minimumHeight <= 0 ||
            maximumHeight < minimumHeight ||
            minimumAspect <= 0 ||
            maximumAspect < minimumAspect)
        {
            return null;
        }

        return new OmsiSceneryTreeDefinition(
            values[0].Trim(),
            minimumHeight,
            maximumHeight,
            minimumAspect,
            maximumAspect);
    }

    private static IReadOnlyList<string> ReadGroups(
        OmsiConfigDocument document)
    {
        var values = document
            .FindFirstSection("groups")
            ?.DataLines
            .ToArray();

        if (values is null ||
            values.Length == 0 ||
            !int.TryParse(
                values[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var count) ||
            count <= 0)
        {
            return Array.Empty<string>();
        }

        return values
            .Skip(1)
            .Take(count)
            .ToArray();
    }

    private static IReadOnlyList<string> ReadSingleValueSections(
        OmsiConfigDocument document,
        string keyword) =>
        document
            .FindSections(keyword)
            .Select(section => section.DataLines.FirstOrDefault())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToArray();

    private sealed record MeshReadResult(
        IReadOnlyList<string> Paths,
        IReadOnlyList<double?> LodThresholds,
        IReadOnlyList<OmsiSceneryMeshTransform> Transforms);

    private sealed class
        TrafficLightControllerBuilder(
            double? cycleDuration)
    {
        public double? CycleDuration
        { get; } =
            cycleDuration;

        public List<
            TrafficLightProgramBuilder>
            Programs
        { get; } = [];
    }

    private sealed class
        TrafficLightProgramBuilder(
            string name)
    {
        public string Name { get; } =
            name;

        public List<
            OmsiTrafficLightPhase>
            Phases
        { get; } = [];
    }

    private sealed class MaterialOverrideBuilder(
        int meshOrdinal,
        string textureName,
        int materialIndex)
    {
        public int MeshOrdinal { get; } =
            meshOrdinal;

        public string TextureName { get; } =
            textureName;

        public int MaterialIndex { get; } =
            materialIndex;

        public int? AlphaMode { get; set; }

        public bool NoZWrite { get; set; }

        public bool NoZCheck { get; set; }

        public string?
            BumpMapTextureName
        { get; set; }

        public double?
            BumpMapStrength
        { get; set; }

        public string?
            NightMapTextureName
        { get; set; }

        public string?
            EnvironmentMapTextureName
        { get; set; }

        public double?
            EnvironmentMapStrength
        { get; set; }

        public string?
            TransMapSource
        { get; set; }

        public string?
            LightMapTextureName
        { get; set; }

        public List<string>
            UnsupportedCommands
        { get; } = [];
    }
}
