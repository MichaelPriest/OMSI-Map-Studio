namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiTileAttachmentEditResult(
    byte[] Bytes,
    int AppliedCount);
