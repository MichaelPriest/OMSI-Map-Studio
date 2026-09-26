using System.IO.Compression;

namespace MapStudio.Core.ProtonBus;

public static class ProtonBusPackageArchiveWriter
{
    public static string Write(
        ProtonBusMapPackageResult package,
        string archivePath)
    {
        ArgumentNullException.ThrowIfNull(
            package);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            archivePath);

        var root =
            Path.GetFullPath(
                package.OutputRoot);

        var target =
            Path.GetFullPath(
                archivePath);

        var parent =
            Path.GetDirectoryName(
                target);

        if (
            !string.IsNullOrWhiteSpace(
                parent))
        {
            Directory.CreateDirectory(
                parent);
        }

        if (
            File.Exists(
                target))
        {
            File.Delete(
                target);
        }

        var generatedFiles =
            EnumerateGeneratedFiles(
                    package)
                .Select(
                    Path.GetFullPath)
                .Distinct(
                    StringComparer
                        .OrdinalIgnoreCase)
                .OrderBy(
                    path =>
                        path,
                    StringComparer
                        .OrdinalIgnoreCase)
                .ToArray();

        using var archive =
            ZipFile.Open(
                target,
                ZipArchiveMode.Create);

        foreach (
            var filePath
            in generatedFiles)
        {
            if (
                !File.Exists(
                    filePath))
            {
                throw new FileNotFoundException(
                    "A generated Proton Bus package file is missing.",
                    filePath);
            }

            var relative =
                Path.GetRelativePath(
                    root,
                    filePath);

            if (
                relative ==
                    ".." ||
                relative.StartsWith(
                    ".." +
                    Path.DirectorySeparatorChar,
                    StringComparison.Ordinal) ||
                Path.IsPathRooted(
                    relative))
            {
                throw new InvalidDataException(
                    $"Generated package path escapes the output root: '{filePath}'.");
            }

            var entryName =
                relative.Replace(
                    Path.DirectorySeparatorChar,
                    '/');

            archive.CreateEntryFromFile(
                filePath,
                entryName,
                CompressionLevel.Optimal);
        }

        return target;
    }

    private static IEnumerable<string>
        EnumerateGeneratedFiles(
            ProtonBusMapPackageResult package)
    {
        yield return
            package.MapDefinitionPath;

        foreach (
            var path
            in package.ModelPaths)
        {
            yield return path;
        }

        foreach (
            var path
            in package.TexturePaths)
        {
            yield return path;
        }

        foreach (
            var path
            in package.BusStopPaths)
        {
            yield return path;
        }

        if (
            !string.IsNullOrWhiteSpace(
                package.EntrypointsPath))
        {
            yield return
                package.EntrypointsPath;
        }

        if (
            !string.IsNullOrWhiteSpace(
                package.EntrypointsListPath))
        {
            yield return
                package.EntrypointsListPath;
        }

        foreach (
            var path
            in package.PedestrianPathPaths)
        {
            yield return path;
        }

        foreach (
            var path
            in package.VehiclePathPaths)
        {
            yield return path;
        }

        foreach (
            var path
            in package.TrainPathPaths)
        {
            yield return path;
        }

        foreach (
            var path
            in package.TrafficLightPaths)
        {
            yield return path;
        }

        foreach (
            var path
            in package.StreetLightPaths)
        {
            yield return path;
        }

        foreach (
            var directory
            in package
                .DestinationDirectories)
        {
            if (
                !Directory.Exists(
                    directory))
            {
                continue;
            }

            foreach (
                var file
                in Directory.EnumerateFiles(
                    directory,
                    "*",
                    SearchOption.AllDirectories))
            {
                yield return file;
            }
        }
    }
}
