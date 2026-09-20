namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileSplineLinkEditResult(
    byte[] Bytes,
    int AppliedEdits);
