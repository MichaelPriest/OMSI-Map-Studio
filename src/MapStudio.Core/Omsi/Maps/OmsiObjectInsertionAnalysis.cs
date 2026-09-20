namespace MapStudio.Core.Omsi.Maps;

public sealed record OmsiObjectInsertionAnalysis(
    int MaxUsedId,
    OmsiPlacedObject? MatchingObjectTemplate)
{
    public int GetNextId()
    {
        if (MaxUsedId >= int.MaxValue)
        {
            throw new InvalidDataException(
                "objectIdExhausted");
        }

        return checked(MaxUsedId + 1);
    }
}
