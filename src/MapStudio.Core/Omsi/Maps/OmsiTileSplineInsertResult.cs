namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileSplineInsertResult(
    byte[] Bytes,
    int SourceSectionOrdinal);
