using MapStudio.Renderer.Viewport;
using System.Text.Json;

namespace MapStudio.Native.Services;

public sealed class NativeAssetLibraryState
{
    public List<string> Favorites { get; init; } = [];

    public List<string> Recent { get; init; } = [];

    public Dictionary<string, int> Usage { get; init; } =
        new(
            StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<string>>
        Collections { get; init; } =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                ["Minha coleção"] = []
            };

    public List<NativeConstructionSetDefinition>
        ConstructionSets { get; init; } = [];

    public double ToolPaletteOffsetX { get; init; }

    public double ToolPaletteOffsetY { get; init; }
}

public static class NativeAssetLibraryStateStore
{
    private static readonly JsonSerializerOptions
        Options =
            new(
                JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            };

    public static NativeAssetLibraryState Load()
    {
        var path =
            GetPath();

        if (!File.Exists(path))
        {
            return new NativeAssetLibraryState();
        }

        try
        {
            var raw =
                File.ReadAllText(path);

            var state =
                JsonSerializer
                    .Deserialize<
                        NativeAssetLibraryState>(
                            raw,
                            Options) ??
                new NativeAssetLibraryState();

            return Normalize(state);
        }
        catch
        {
            return new NativeAssetLibraryState();
        }
    }

    public static void Save(
        NativeAssetLibraryState state)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        var path =
            GetPath();

        var directory =
            Path.GetDirectoryName(path)!;

        Directory.CreateDirectory(
            directory);

        var temp =
            path +
            "." +
            Guid.NewGuid()
                .ToString("N") +
            ".tmp";

        File.WriteAllText(
            temp,
            JsonSerializer.Serialize(
                Normalize(state),
                Options));

        File.Move(
            temp,
            path,
            overwrite: true);
    }

    private static NativeAssetLibraryState
        Normalize(
            NativeAssetLibraryState state)
    {
        var favorites =
            state.Favorites
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        var recent =
            state.Recent
                .Where(
                    value =>
                        !string.IsNullOrWhiteSpace(
                            value))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .Take(64)
                .ToList();

        var usage =
            new Dictionary<
                string,
                int>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (
            var pair in
                state.Usage)
        {
            if (
                !string.IsNullOrWhiteSpace(
                    pair.Key) &&
                pair.Value >
                    0)
            {
                usage[
                    pair.Key] =
                    pair.Value;
            }
        }

        var collections =
            new Dictionary<
                string,
                List<string>>(
                    StringComparer.OrdinalIgnoreCase);

        foreach (
            var pair in
                state.Collections)
        {
            if (string.IsNullOrWhiteSpace(
                    pair.Key))
            {
                continue;
            }

            collections[
                pair.Key] =
                pair.Value
                    .Where(
                        value =>
                            !string.IsNullOrWhiteSpace(
                                value))
                    .Distinct(
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        if (!collections.ContainsKey(
                "Minha coleção"))
        {
            collections[
                "Minha coleção"] =
                [];
        }

        return new NativeAssetLibraryState
        {
            Favorites =
                favorites,
            Recent =
                recent,
            Usage =
                usage,
            Collections =
                collections,
            ConstructionSets =
                state.ConstructionSets
                    .Where(
                        set =>
                            !string.IsNullOrWhiteSpace(
                                set.Id) &&
                            !string.IsNullOrWhiteSpace(
                                set.Name) &&
                            set.Companions.Count >
                                0)
                    .GroupBy(
                        set =>
                            set.Id,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(
                        group =>
                            group.Last())
                    .Take(64)
                    .ToList(),
            ToolPaletteOffsetX =
                double.IsFinite(
                    state.ToolPaletteOffsetX)
                    ? Math.Clamp(
                        state.ToolPaletteOffsetX,
                        -4000,
                        4000)
                    : 0,
            ToolPaletteOffsetY =
                double.IsFinite(
                    state.ToolPaletteOffsetY)
                    ? Math.Clamp(
                        state.ToolPaletteOffsetY,
                        -4000,
                        4000)
                    : 0
        };
    }

    private static string GetPath() =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder
                    .LocalApplicationData),
            "OMSI Map Studio",
            "native-library-state.json");
}
