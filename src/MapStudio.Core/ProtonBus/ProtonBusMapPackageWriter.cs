namespace MapStudio.Core.ProtonBus;

public sealed record ProtonBusModelExport(
    string FileName,
    ProtonBusExportScene Scene);

public sealed record ProtonBusTextureExport(
    string SourcePath,
    string FileName);

public sealed record ProtonBusMapPackageRequest(
    ProtonBusMapDefinition Definition,
    IReadOnlyList<ProtonBusModelExport>
        Models,
    IReadOnlyList<ProtonBusTextureExport>?
        Textures = null)
{
    public IReadOnlyList<
        ProtonBusBusStopDefinition>
        BusStops { get; init; } =
        Array.Empty<
            ProtonBusBusStopDefinition>();

    public IReadOnlyList<
        ProtonBusEntrypointDefinition>
        Entrypoints { get; init; } =
        Array.Empty<
            ProtonBusEntrypointDefinition>();

    public IReadOnlyList<
        ProtonBusPedestrianPathDefinition>
        PedestrianPaths { get; init; } =
        Array.Empty<
            ProtonBusPedestrianPathDefinition>();

    public IReadOnlyList<
        ProtonBusVehiclePathDefinition>
        VehiclePaths { get; init; } =
        Array.Empty<
            ProtonBusVehiclePathDefinition>();

    public IReadOnlyList<
        ProtonBusTrainPathDefinition>
        TrainPaths { get; init; } =
        Array.Empty<
            ProtonBusTrainPathDefinition>();

    public IReadOnlyList<
        ProtonBusTrafficLightDefinition>
        TrafficLights { get; init; } =
        Array.Empty<
            ProtonBusTrafficLightDefinition>();

    public IReadOnlyList<
        ProtonBusStreetLightDefinition>
        StreetLights { get; init; } =
        Array.Empty<
            ProtonBusStreetLightDefinition>();
}

public sealed record ProtonBusMapPackageResult(
    string OutputRoot,
    string MapDefinitionPath,
    IReadOnlyList<string>
        ModelPaths,
    IReadOnlyList<string>
        TexturePaths)
{
    public IReadOnlyList<string>
        BusStopPaths { get; init; } =
        Array.Empty<string>();

    public string? EntrypointsPath
        { get; init; }

    public string? EntrypointsListPath
        { get; init; }

    public IReadOnlyList<string>
        DestinationDirectories
        { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string>
        PedestrianPathPaths
        { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string>
        VehiclePathPaths
        { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string>
        TrainPathPaths
        { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string>
        TrafficLightPaths
        { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string>
        StreetLightPaths
        { get; init; } =
        Array.Empty<string>();
}

public static class ProtonBusMapPackageWriter
{
    public static ProtonBusMapPackageResult Write(
        string outputRoot,
        ProtonBusMapPackageRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            outputRoot);

        ArgumentNullException.ThrowIfNull(
            request);

        ProtonBusMapDefinitionValidator
            .ThrowIfInvalid(
                request.Definition);

        var root =
            Path.GetFullPath(
                outputRoot);

        Directory.CreateDirectory(
            root);

        var layout =
            ProtonBusMapPackageLayout
                .From(
                    request.Definition);

        var mapDefinitionPath =
            ResolveOutputPath(
                root,
                layout.MapDefinitionPath);

        EnsureDirectoryForFile(
            mapDefinitionPath);

        File.WriteAllText(
            mapDefinitionPath,
            ProtonBusMapDefinitionWriter
                .Serialize(
                    request.Definition));

        foreach (
            var directory
            in EnumerateDirectories(
                layout))
        {
            Directory.CreateDirectory(
                ResolveOutputPath(
                    root,
                    directory));
        }

        var modelPaths =
            new List<string>(
                request.Models.Count);

        foreach (
            var model
            in request.Models)
        {
            ArgumentNullException.ThrowIfNull(
                model);

            var fileName =
                NormalizeModelFileName(
                    model.FileName);

            var relativePath =
                CombineRelative(
                    layout.ModelsDirectoryPath,
                    fileName);

            var targetPath =
                ResolveOutputPath(
                    root,
                    relativePath);

            File.WriteAllBytes(
                targetPath,
                ProtonBus3dsWriter
                    .Write(
                        model.Scene));

            modelPaths.Add(
                targetPath);
        }

        var texturePaths =
            new List<string>();

        foreach (
            var texture
            in request.Textures ??
               Array.Empty<
                   ProtonBusTextureExport>())
        {
            ArgumentNullException.ThrowIfNull(
                texture);

            if (
                !File.Exists(
                    texture.SourcePath))
            {
                throw new FileNotFoundException(
                    "Proton Bus texture source was not found.",
                    texture.SourcePath);
            }

            if (
                !IsPngSource(
                    texture.SourcePath))
            {
                throw new ArgumentException(
                    "Proton Bus texture sources must already be valid PNG files until the texture transcoder is enabled.",
                    nameof(request));
            }

            var fileName =
                NormalizePngFileName(
                    texture.FileName);

            var targetPath =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout.TexturesDirectoryPath,
                        fileName));

            File.Copy(
                texture.SourcePath,
                targetPath,
                overwrite: true);

            texturePaths.Add(
                targetPath);
        }

        var busStopPaths =
            WriteBusStops(
                root,
                layout,
                request.BusStops);

        var entrypointFiles =
            WriteEntrypoints(
                root,
                layout,
                request.Entrypoints);

        ValidateMovingPathPrefixes(
            request.PedestrianPaths,
            request.VehiclePaths,
            request.TrainPaths);

        var pedestrianPathPaths =
            WritePedestrianPaths(
                root,
                layout,
                request.PedestrianPaths);

        var vehiclePathPaths =
            WriteVehiclePaths(
                root,
                layout,
                request.VehiclePaths);

        var trainPathPaths =
            WriteTrainPaths(
                root,
                layout,
                request.TrainPaths);

        var trafficLightPaths =
            WriteTrafficLights(
                root,
                layout,
                request.TrafficLights);

        var streetLightPaths =
            WriteStreetLights(
                root,
                layout,
                request.StreetLights);

        return new(
            root,
            mapDefinitionPath,
            modelPaths.ToArray(),
            texturePaths.ToArray())
        {
            BusStopPaths =
                busStopPaths,
            EntrypointsPath =
                entrypointFiles
                    .DefinitionsPath,
            EntrypointsListPath =
                entrypointFiles
                    .ListPath,
            DestinationDirectories =
                entrypointFiles
                    .DestinationDirectories,
            PedestrianPathPaths =
                pedestrianPathPaths,
            VehiclePathPaths =
                vehiclePathPaths,
            TrainPathPaths =
                trainPathPaths,
            TrafficLightPaths =
                trafficLightPaths,
            StreetLightPaths =
                streetLightPaths
        };
    }

    private static IReadOnlyList<string>
        WriteStreetLights(
            string root,
            ProtonBusMapPackageLayout layout,
            IReadOnlyList<
                ProtonBusStreetLightDefinition>
                definitions)
    {
        var duplicate =
            definitions
                .GroupBy(
                    item => item.Prefix,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate Proton Bus street-light prefix '{duplicate.Key}'.",
                nameof(definitions));
        }

        var output =
            new List<string>(
                definitions.Count);

        foreach (var definition in definitions)
        {
            ProtonBusStreetLightDefinitionWriter
                .Validate(definition);

            var targetPath =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout.StreetLightsDirectoryPath,
                        definition.SuggestedFileName));

            File.WriteAllText(
                targetPath,
                ProtonBusStreetLightDefinitionWriter
                    .Serialize(definition));

            output.Add(targetPath);
        }

        return output;
    }

    private static IReadOnlyList<string>
        WriteTrafficLights(
            string root,
            ProtonBusMapPackageLayout layout,
            IReadOnlyList<ProtonBusTrafficLightDefinition> definitions)
    {
        var duplicate =
            definitions
                .GroupBy(item => item.Prefix, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate Proton Bus traffic-light prefix '{duplicate.Key}'.",
                nameof(definitions));
        }

        var output = new List<string>(definitions.Count);

        foreach (var definition in definitions)
        {
            ProtonBusTrafficLightDefinitionWriter.Validate(definition);

            var target =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout.TrafficLightsDirectoryPath,
                        definition.SuggestedFileName));

            File.WriteAllText(
                target,
                ProtonBusTrafficLightDefinitionWriter.Serialize(definition));

            output.Add(target);
        }

        return output;
    }

    private static void ValidateMovingPathPrefixes(
        IReadOnlyList<ProtonBusPedestrianPathDefinition> pedestrians,
        IReadOnlyList<ProtonBusVehiclePathDefinition> vehicles,
        IReadOnlyList<ProtonBusTrainPathDefinition> trains)
    {
        var names =
            pedestrians.Select(item => item.Prefix)
                .Concat(vehicles.Select(item => item.Prefix))
                .Concat(trains.Select(item => item.Prefix))
                .GroupBy(value => value, StringComparer.OrdinalIgnoreCase);

        var duplicate =
            names.FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate Proton Bus moving-path prefix '{duplicate.Key}' across AI categories.");
        }
    }

    private static IReadOnlyList<string>
        WriteVehiclePaths(
            string root,
            ProtonBusMapPackageLayout layout,
            IReadOnlyList<ProtonBusVehiclePathDefinition> paths)
    {
        var output = new List<string>(paths.Count);

        foreach (var path in paths)
        {
            ProtonBusVehiclePathDefinitionWriter.Validate(path);

            var target =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout.AiVehiclesDirectoryPath,
                        path.SuggestedFileName));

            File.WriteAllText(
                target,
                ProtonBusVehiclePathDefinitionWriter.Serialize(path));

            output.Add(target);
        }

        return output;
    }

    private static IReadOnlyList<string>
        WriteTrainPaths(
            string root,
            ProtonBusMapPackageLayout layout,
            IReadOnlyList<ProtonBusTrainPathDefinition> paths)
    {
        var output = new List<string>(paths.Count);

        foreach (var path in paths)
        {
            ProtonBusTrainPathDefinitionWriter.Validate(path);

            var target =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout.AiTrainsDirectoryPath,
                        path.SuggestedFileName));

            File.WriteAllText(
                target,
                ProtonBusTrainPathDefinitionWriter.Serialize(path));

            output.Add(target);
        }

        return output;
    }

    private static IReadOnlyList<string>
        WritePedestrianPaths(
            string root,
            ProtonBusMapPackageLayout layout,
            IReadOnlyList<
                ProtonBusPedestrianPathDefinition>
                paths)
    {
        var duplicate =
            paths
                .GroupBy(
                    path =>
                        path.Prefix,
                    StringComparer
                        .OrdinalIgnoreCase)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicate is
                not null)
        {
            throw new ArgumentException(
                $"Duplicate Proton Bus pedestrian-path prefix '{duplicate.Key}'.",
                nameof(paths));
        }

        var output =
            new List<string>(
                paths.Count);

        foreach (
            var path
            in paths)
        {
            ProtonBusPedestrianPathDefinitionWriter
                .Validate(
                    path);

            var target =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout
                            .AiPeopleDirectoryPath,
                        path
                            .SuggestedFileName));

            File.WriteAllText(
                target,
                ProtonBusPedestrianPathDefinitionWriter
                    .Serialize(
                        path));

            output.Add(
                target);
        }

        return output;
    }

    private sealed record EntrypointWriteResult(
        string? DefinitionsPath,
        string? ListPath,
        IReadOnlyList<string>
            DestinationDirectories);

    private static EntrypointWriteResult
        WriteEntrypoints(
            string root,
            ProtonBusMapPackageLayout layout,
            IReadOnlyList<
                ProtonBusEntrypointDefinition>
                entrypoints)
    {
        ProtonBusEntrypointDefinitionWriter
            .ValidateAll(
                entrypoints);

        if (
            entrypoints.Count ==
            0)
        {
            return new(
                null,
                null,
                Array.Empty<string>());
        }

        var definitionsPath =
            ResolveOutputPath(
                root,
                CombineRelative(
                    layout.ModelsDirectoryPath,
                    "entrypoints.txt"));

        var listPath =
            ResolveOutputPath(
                root,
                CombineRelative(
                    layout.ModelsDirectoryPath,
                    "entrypoints_list.txt"));

        File.WriteAllText(
            definitionsPath,
            ProtonBusEntrypointDefinitionWriter
                .SerializeDefinitions(
                    entrypoints));

        File.WriteAllText(
            listPath,
            ProtonBusEntrypointDefinitionWriter
                .SerializeList(
                    entrypoints));

        var destinations =
            new List<string>(
                entrypoints.Count);

        foreach (
            var entrypoint
            in entrypoints)
        {
            var destination =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout
                            .DestinationsDirectoryPath,
                        entrypoint.Name));

            Directory.CreateDirectory(
                destination);

            if (
                entrypoint.IsIntercity)
            {
                File.WriteAllText(
                    Path.Combine(
                        destination,
                        "intercity.txt"),
                    string.Empty);
            }

            if (
                entrypoint.IsOutOfService)
            {
                File.WriteAllText(
                    Path.Combine(
                        destination,
                        "outofservice.txt"),
                    string.Empty);
            }

            destinations.Add(
                destination);
        }

        return new(
            definitionsPath,
            listPath,
            destinations.ToArray());
    }

    private static IReadOnlyList<string>
        WriteBusStops(
            string root,
            ProtonBusMapPackageLayout layout,
            IReadOnlyList<
                ProtonBusBusStopDefinition>
                busStops)
    {
        var duplicate =
            busStops
                .GroupBy(
                    stop =>
                        stop.Prefix,
                    StringComparer
                        .OrdinalIgnoreCase)
                .FirstOrDefault(
                    group =>
                        group.Count() >
                        1);

        if (
            duplicate is
                not null)
        {
            throw new ArgumentException(
                $"Duplicate Proton Bus bus-stop prefix '{duplicate.Key}'.",
                nameof(busStops));
        }

        var output =
            new List<string>(
                busStops.Count);

        foreach (
            var stop
            in busStops)
        {
            ProtonBusBusStopDefinitionWriter
                .Validate(
                    stop);

            var targetPath =
                ResolveOutputPath(
                    root,
                    CombineRelative(
                        layout
                            .BusStopsDirectoryPath,
                        stop
                            .SuggestedFileName));

            File.WriteAllText(
                targetPath,
                ProtonBusBusStopDefinitionWriter
                    .Serialize(
                        stop));

            output.Add(
                targetPath);
        }

        return output;
    }

    private static IEnumerable<string>
        EnumerateDirectories(
            ProtonBusMapPackageLayout
                layout)
    {
        yield return
            layout.BaseDirectoryPath;

        yield return
            layout.ModelsDirectoryPath;

        yield return
            layout.TexturesDirectoryPath;

        yield return
            layout.DestinationsDirectoryPath;

        yield return
            layout.SkinsDirectoryPath;

        yield return
            layout.AiPeopleDirectoryPath;

        yield return
            layout.AiTrainsDirectoryPath;

        yield return
            layout.AiVehiclesDirectoryPath;

        yield return
            layout.BusStopsDirectoryPath;

        yield return
            layout.TrafficLightsDirectoryPath;

        yield return
            layout.StreetLightsDirectoryPath;
    }

    private static bool IsPngSource(
        string path)
    {
        if (
            !string.Equals(
                Path.GetExtension(
                    path),
                ".png",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Span<byte> signature =
            stackalloc byte[8];

        using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        if (
            stream.Read(
                signature) !=
            signature.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> expected =
        [
            137,
            80,
            78,
            71,
            13,
            10,
            26,
            10
        ];

        return signature
            .SequenceEqual(
                expected);
    }

    private static string
        NormalizeModelFileName(
            string value)
    {
        var name =
            NormalizeFileName(
                value,
                "model");

        return name.EndsWith(
                ".3ds",
                StringComparison
                    .OrdinalIgnoreCase)
            ? name
            : name +
              ".3ds";
    }

    private static string
        NormalizePngFileName(
            string value)
    {
        var name =
            NormalizeFileName(
                value,
                "texture");

        if (
            !name.EndsWith(
                ".png",
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Proton Bus package textures must use the PNG extension.",
                nameof(value));
        }

        return name;
    }

    private static string NormalizeFileName(
        string value,
        string fieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        var name =
            Path.GetFileName(
                value.Trim());

        if (
            !string.Equals(
                name,
                value.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{fieldName} must be a file name, not a path.",
                nameof(value));
        }

        if (
            name.Any(
                character =>
                    character >
                        127 ||
                    !(
                        char.IsAsciiLetterOrDigit(
                            character) ||
                        character is
                            ' ' or
                            '_' or
                            '-' or
                            '.'
                    )))
        {
            throw new ArgumentException(
                $"{fieldName} contains characters that are unsafe for Proton Bus packages.",
                nameof(value));
        }

        return name;
    }

    private static string ResolveOutputPath(
        string root,
        string relativePath)
    {
        var platformRelative =
            relativePath.Replace(
                '/',
                Path.DirectorySeparatorChar);

        var fullPath =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    platformRelative));

        var prefix =
            root.EndsWith(
                Path.DirectorySeparatorChar)
                ? root
                : root +
                  Path.DirectorySeparatorChar;

        if (
            !fullPath.StartsWith(
                prefix,
                StringComparison
                    .OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Proton Bus package path escaped the selected output directory.");
        }

        return fullPath;
    }

    private static void EnsureDirectoryForFile(
        string path)
    {
        var directory =
            Path.GetDirectoryName(
                path);

        if (
            !string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }
    }

    private static string CombineRelative(
        string left,
        string right) =>
        left.TrimEnd(
            '/',
            '\\') +
        "/" +
        right.TrimStart(
            '/',
            '\\');
}
