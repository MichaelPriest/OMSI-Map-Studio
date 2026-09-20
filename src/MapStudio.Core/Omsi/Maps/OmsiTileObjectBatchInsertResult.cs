namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileObjectBatchInsertResult(
    byte[] Bytes,
    IReadOnlyList<int> SourceSectionOrdinals);
