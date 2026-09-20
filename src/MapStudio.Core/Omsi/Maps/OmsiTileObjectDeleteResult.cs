namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileObjectDeleteResult(
    byte[] Bytes,
    int DeletedObjects);
