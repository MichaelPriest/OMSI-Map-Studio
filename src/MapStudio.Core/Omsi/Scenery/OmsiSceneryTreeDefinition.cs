namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryTreeDefinition(
    string TextureName,
    double MinimumHeight,
    double MaximumHeight,
    double MinimumAspect,
    double MaximumAspect);
