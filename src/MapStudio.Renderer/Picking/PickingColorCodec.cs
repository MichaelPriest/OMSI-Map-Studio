namespace MapStudio.Renderer.Picking;

public static class PickingColorCodec
{
    public static uint Encode(
        PickingId id)
    {
        if (id.IsNone)
        {
            return 0;
        }

        var kind =
            (uint)id.Kind & 0x0F;

        var value =
            (uint)id.Value & 0x00FF_FFFF;

        return
            (kind << 24) |
            value;
    }

    public static PickingId Decode(
        uint encoded)
    {
        if (encoded == 0)
        {
            return PickingId.None;
        }

        var kind =
            (PickingKind)(
                (encoded >> 24) &
                0x0F);

        var value =
            (int)(
                encoded &
                0x00FF_FFFF);

        return
            new PickingId(
                kind,
                value);
    }
}
