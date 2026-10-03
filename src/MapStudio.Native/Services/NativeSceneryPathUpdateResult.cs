using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Native.Services;

public sealed record NativeSceneryPathUpdateResult(
    OmsiSceneryPathDefinition Path,
    string AssetPath,
    string BackupPath);
