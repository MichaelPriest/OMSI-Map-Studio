namespace MapStudio.Core.Omsi.Maps;

public static class OmsiObjectInsertionAnalyzer
{
    public static OmsiObjectInsertionAnalysis Analyze(
        IEnumerable<OmsiTileContent> tiles,
        string sceneryObjectPath)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sceneryObjectPath);

        var maxUsedId = 0;
        OmsiPlacedObject? template = null;

        foreach (var tile in tiles)
        {
            foreach (var placedObject in
                tile.Objects)
            {
                if (placedObject.ObjectId > maxUsedId)
                {
                    maxUsedId =
                        placedObject.ObjectId;
                }

                if (
                    template is null &&
                    string.Equals(
                        placedObject
                            .SceneryObjectPath,
                        sceneryObjectPath,
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    template =
                        placedObject;
                }
            }

            foreach (var spline in
                tile.Splines)
            {
                if (spline.SplineId > maxUsedId)
                {
                    maxUsedId =
                        spline.SplineId;
                }
            }
        }

        return new OmsiObjectInsertionAnalysis(
            maxUsedId,
            template);
    }
}
