namespace MapStudio.Core.Omsi.Scenery;

public sealed record OmsiSceneryLightPoint(
    string Keyword,
    double? PositionX,
    double? PositionY,
    double? PositionZ,
    double? DirectionX,
    double? DirectionY,
    double? DirectionZ,
    double? Red,
    double? Green,
    double? Blue,
    double? Size,
    double? InnerAngle,
    double? OuterAngle,
    string? ActivationVariable,
    string? BrightnessVariable,
    string? MultiplicationFactor,
    string? EffectTexture,
    IReadOnlyList<string> RawValues,
    double? Range = null,
    bool IsMapLight = false)
{
    public bool HasRenderableEnhancedData =>
        PositionX.HasValue &&
        PositionY.HasValue &&
        PositionZ.HasValue &&
        Red.HasValue &&
        Green.HasValue &&
        Blue.HasValue &&
        Size.HasValue;
}
