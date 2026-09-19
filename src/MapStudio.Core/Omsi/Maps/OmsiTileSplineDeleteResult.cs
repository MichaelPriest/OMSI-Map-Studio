namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileSplineDeleteResult(
    byte[] Bytes,
    int DeletedSplines);
