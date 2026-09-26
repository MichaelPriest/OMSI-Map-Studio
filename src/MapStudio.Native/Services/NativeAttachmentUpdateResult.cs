using MapStudio.Core.Omsi.Maps;

namespace MapStudio.Native.Services;

public sealed record NativeAttachmentUpdateResult(
    NativeMapSnapshot Snapshot,
    OmsiPlacedAttachment Attachment,
    string BackupDirectory);
