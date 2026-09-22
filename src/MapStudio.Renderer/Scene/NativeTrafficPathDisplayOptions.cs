namespace MapStudio.Renderer.Scene;

public sealed record NativeTrafficPathDisplayOptions(
    bool Vehicles,
    bool Pedestrians,
    bool Rails,
    bool Air,
    bool ShowWidthEdges,
    bool ShowDirectionArrows,
    bool HighlightSignalControlled)
{
    public static NativeTrafficPathDisplayOptions CleanVehicles =>
        new(
            Vehicles: true,
            Pedestrians: false,
            Rails: false,
            Air: false,
            ShowWidthEdges: false,
            ShowDirectionArrows: true,
            HighlightSignalControlled: false);

    public static NativeTrafficPathDisplayOptions AllDetailed =>
        new(
            Vehicles: true,
            Pedestrians: true,
            Rails: true,
            Air: true,
            ShowWidthEdges: true,
            ShowDirectionArrows: true,
            HighlightSignalControlled: true);

    public bool IncludesType(
        int type) =>
        type switch
        {
            0 => Vehicles,
            1 => Pedestrians,
            2 => Rails,
            3 => Air,
            _ => Vehicles
        };
}
