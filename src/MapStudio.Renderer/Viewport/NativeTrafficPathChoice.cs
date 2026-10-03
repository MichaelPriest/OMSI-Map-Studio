namespace MapStudio.Renderer.Viewport;

public sealed record NativeTrafficPathChoice(
    int Index,
    int Type,
    int Direction,
    double Width,
    string DisplayText)
{
    public string KindLabel =>
        Type switch
        {
            1 => "Pedestre",
            2 => "Trilho",
            3 => "Aéreo",
            _ => "Veículo"
        };

    public string DirectionLabel =>
        Direction switch
        {
            0 => "→",
            1 => "←",
            2 => "↔",
            _ => "?"
        };
}
