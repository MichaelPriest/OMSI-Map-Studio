namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileObjectInsertResult(
    byte[] Bytes,
    int SourceSectionOrdinal);
