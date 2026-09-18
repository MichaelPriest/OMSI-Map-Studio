namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiMapDiscoveryProgress(
    int Completed,
    int Total,
    int Skipped,
    string? DirectoryName);
