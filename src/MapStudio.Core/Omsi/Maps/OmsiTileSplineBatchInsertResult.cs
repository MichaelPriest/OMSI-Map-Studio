namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileSplineBatchInsertResult(
    byte[] Bytes,
    IReadOnlyList<int> SourceSectionOrdinals);
