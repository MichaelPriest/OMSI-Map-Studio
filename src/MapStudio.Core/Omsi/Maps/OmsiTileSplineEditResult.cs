namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileSplineEditResult(
    byte[] Bytes,
    int AppliedEdits);
