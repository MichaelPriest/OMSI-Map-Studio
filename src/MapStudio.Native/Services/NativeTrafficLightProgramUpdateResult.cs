using MapStudio.Core.Omsi.Scenery;

namespace MapStudio.Native.Services;

public sealed record NativeTrafficLightProgramUpdateResult(
    OmsiTrafficLightProgram Program,
    double? CycleDuration,
    string SceneryObjectPath,
    string BackupPath);
