namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiMapCatalogResult(
    IReadOnlyList<OmsiMapDescriptor> Maps,
    int SkippedMaps);
