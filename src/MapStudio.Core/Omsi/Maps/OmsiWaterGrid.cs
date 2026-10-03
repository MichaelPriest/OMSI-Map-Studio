namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiWaterGrid(
    IReadOnlyList<float> Heights)
{
    public const int CellCount = 1;

    public const int SampleCount = 2;

    public const int HeightCount = 4;

    public float GetHeight(
        int sampleX,
        int sampleZ)
    {
        if (
            sampleX is < 0 or >= SampleCount ||
            sampleZ is < 0 or >= SampleCount)
        {
            throw new ArgumentOutOfRangeException(
                sampleX is < 0 or >= SampleCount
                    ? nameof(sampleX)
                    : nameof(sampleZ));
        }

        if (
            Heights.Count !=
                HeightCount)
        {
            throw new InvalidDataException(
                "invalidWaterHeightCount");
        }

        return Heights[
            sampleZ *
                SampleCount +
            sampleX];
    }
}
