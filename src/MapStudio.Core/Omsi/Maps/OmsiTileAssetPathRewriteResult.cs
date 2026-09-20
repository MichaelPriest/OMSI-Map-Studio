namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileAssetPathRewriteResult(
    byte[] Bytes,
    int ObjectReplacements,
    int SplineReplacements);
