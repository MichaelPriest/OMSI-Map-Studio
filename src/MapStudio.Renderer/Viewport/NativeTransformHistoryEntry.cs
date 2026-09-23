namespace MapStudio.Renderer.Viewport;

public sealed record NativeTransformHistoryEntry(
    IReadOnlyList<NativePendingTransformEdit> BeforeEdits,
    IReadOnlyList<NativePendingTransformEdit> AfterEdits)
{
    public NativeTransformHistoryEntry(
        NativePendingTransformEdit before,
        NativePendingTransformEdit after)
        : this(
            new[]
            {
                before
            },
            new[]
            {
                after
            })
    {
    }

    public NativePendingTransformEdit Before =>
        BeforeEdits[0];

    public NativePendingTransformEdit After =>
        AfterEdits[0];

    public bool IsBatch =>
        BeforeEdits.Count >
            1 ||
        AfterEdits.Count >
            1;
}
