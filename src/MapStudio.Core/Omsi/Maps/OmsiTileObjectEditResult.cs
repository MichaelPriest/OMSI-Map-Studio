namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileObjectEditResult(
    byte[] Bytes,
    int AppliedEdits);
